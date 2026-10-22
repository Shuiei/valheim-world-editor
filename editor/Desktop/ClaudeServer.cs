using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's connection (Settings: Allow Claude to connect): an MCP server inside the editor
/// (ClaudeTools), so that Claude (Claude Code, Claude Desktop) can look at the open world and change it
/// as the user would. On 127.0.0.1 unless the settings choose another address, only for callers that send the token
/// (which also keeps web pages out: they cannot send it; on 127.0.0.1, nor a Host other than this computer's
/// address). What Claude changes in a world
/// stays pending: it never saves a world or applies live. Only the Workshop's building can be kept, as a blueprint of
/// the user's library (save_blueprint, which never replaces one without being told to).
/// </summary>
public sealed class ClaudeServer
{
    /// <summary>The server the settings started, null when off.</summary>
    private static ClaudeServer? _running;
    /// <summary>One <see cref="Apply"/> at a time.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The web host the server runs in.</summary>
    private readonly WebApplication _app;

    /// <summary>The port it listens on.</summary>
    public int Port { get; }

    /// <summary>A server already started (see <see cref="Start"/>).</summary>
    private ClaudeServer(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    /// <summary>
    /// The address Claude connects to: this computer's 127.0.0.1 when the server listens there or on every address
    /// (0.0.0.0, ::), else the address it listens on.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="address">The address the server listens on (null: 127.0.0.1).</param>
    public static string Url(int port, string? address = null)
    {
        string host = ParseAddress(address) is { } ip && !IsEveryAddress(ip)
            ? (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString())
            : "127.0.0.1";
        return $"http://{host}:{port}/mcp";
    }

    /// <summary>
    /// The address another computer connects to when the server listens on every address: this computer's name, as
    /// the network's name service answers it (a name without a domain gets .local, mDNS's, which Windows, macOS and
    /// Linux answer).
    /// </summary>
    /// <param name="port">The port.</param>
    public static string RemoteUrl(int port)
    {
        string host = Dns.GetHostName();
        return $"http://{(host.Contains('.', StringComparison.Ordinal) ? host : host + ".local")}:{port}/mcp";
    }

    /// <summary>Whether the address is every address (0.0.0.0, ::): every network this computer is on.</summary>
    /// <param name="ip">The address.</param>
    public static bool IsEveryAddress(IPAddress ip) => ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any);

    /// <summary>
    /// This computer's IPv4 addresses on the networks that are up, with each network's name (Ethernet, Wi-Fi…), for
    /// Settings' choices; none when the system will not say.
    /// </summary>
    public static IReadOnlyList<(string Address, string Network)> LocalAddresses()
    {
        try
        {
            return [.. System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                    .Select(a => (a.Address.ToString(), n.Name)))
                .Distinct()];
        }
        catch (System.Net.NetworkInformation.NetworkInformationException e)
        {
            Log.Caught(e, "listing this computer's network addresses");
            return [];
        }
    }

    /// <summary>What to type to connect Claude Code to the editor.</summary>
    /// <param name="port">The port.</param>
    /// <param name="token">The token.</param>
    /// <param name="address">The address the server listens on (null: 127.0.0.1).</param>
    public static string ClaudeCodeCommand(int port, string token, string? address = null) =>
        $"claude mcp add --transport http valheim-editor {Url(port, address)} --header \"Authorization: Bearer {token}\"";

    /// <summary>
    /// The settings' address, or null when it is not an IPv4 or IPv6 address (names are not taken). Only its first
    /// word counts, so a choice of Settings' list ("0.0.0.0 (every network)") reads as its address.
    /// </summary>
    /// <param name="address">The settings' address.</param>
    public static IPAddress? ParseAddress(string? address) =>
        IPAddress.TryParse(address?.Trim().Split(' ', 2)[0], out var ip) ? ip : null;

    /// <summary>What to add to Claude Desktop's configuration: this program, started as the relay (--mcp-stdio).</summary>
    /// <param name="program">The path of the editor's program.</param>
    public static string DesktopConfig(string program) => new System.Text.Json.Nodes.JsonObject
    {
        ["mcpServers"] = new System.Text.Json.Nodes.JsonObject
        {
            ["valheim-editor"] = new System.Text.Json.Nodes.JsonObject { ["command"] = program, ["args"] = new System.Text.Json.Nodes.JsonArray("--mcp-stdio") },
        },
    }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    /// <summary>A new secret, for the settings: 24 random bytes as lower-case hex.</summary>
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>"Listening on …", the problem when it could not start, or null when off.</summary>
    public static string? Status { get; private set; }

    /// <summary>
    /// Started or stopped as the settings say (again when they changed). The problem, or null. A token is made
    /// and saved when the settings have none.
    /// </summary>
    public static async Task<string?> Apply(AppSettings settings, MainWindow window)
    {
        await Gate.WaitAsync();
        try
        {
            if (_running != null)
            {
                await _running._app.StopAsync();
                await _running._app.DisposeAsync();
                _running = null;
            }
            // Said again below for this start; none when off (not the last start's problem).
            Status = null;
            if (!settings.ClaudeConnect)
            {
                return null;
            }
            if (string.IsNullOrEmpty(settings.ClaudeToken))
            {
                settings.ClaudeToken = NewToken();
                settings.Save();
            }
            if (ParseAddress(settings.ClaudeAddress) is not IPAddress address)
            {
                Status = $"Could not listen on {settings.ClaudeAddress}: not an IP address (127.0.0.1, 0.0.0.0, or one of this computer's)";
                return Status;
            }
            try
            {
                _running = await Start(settings.ClaudePort, settings.ClaudeToken!, new ClaudeTools(window), address);
                Status = IPAddress.IsLoopback(address)
                    ? $"Listening on {Url(settings.ClaudePort)}"
                    : IsEveryAddress(address)
                        ? $"Listening on {address}:{settings.ClaudePort}, from other computers too ({RemoteUrl(settings.ClaudePort)}; here: {Url(settings.ClaudePort)})"
                        : $"Listening on {Url(settings.ClaudePort, settings.ClaudeAddress)}, from other computers too";
                Console.WriteLine($"claude: {Status}");
                return null;
            }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or InvalidOperationException)
            {
                Log.Caught(e, $"starting the server for Claude on port {settings.ClaudePort}");
                Status = $"Could not listen on port {settings.ClaudePort}: {e.Message}";
                return Status;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The server on address:port (127.0.0.1 by default) with these tools (tests start their own).</summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="token">The secret callers must send as "Authorization: Bearer &lt;token&gt;".</param>
    /// <param name="tools">The tools Claude is given.</param>
    /// <param name="address">The address to listen on (null: 127.0.0.1).</param>
    internal static async Task<ClaudeServer> Start(int port, string token, ClaudeTools tools, IPAddress? address = null)
    {
        address ??= IPAddress.Loopback;
        bool loopback = IPAddress.IsLoopback(address);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        // A tool that throws is answered "An error occurred invoking …" by the MCP library, which logs why: the log has it.
        builder.Logging.AddProvider(new ErrorsToLog());
        // The host's console lifetime would take Ctrl+C (SIGINT) from the editor started in a terminal
        // and stop only this server, leaving the editor open: the signals are the editor's (Program).
        builder.Services.AddSingleton<IHostLifetime, NoSignals>();
        builder.WebHost.ConfigureKestrel(k => k.Listen(address, port));
        builder.Services.AddMcpServer(o =>
            {
                o.ServerInfo = new() { Name = "valheim-world-editor", Version = BuildInfo.Version };
                // How to work in the editor, given to Claude when it connects.
                o.ServerInstructions = ClaudeGuide.Instructions;
            })
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools(tools)
            .WithPrompts<ClaudePrompts>();
        var app = builder.Build();
        byte[] expected = Encoding.UTF8.GetBytes("Bearer " + token);
        app.Use(async (ctx, next) =>
        {
            // On 127.0.0.1, this computer's address only (a web page reaching it under another name is refused);
            // on another address, other computers reach it by any of this one's names. The token, always.
            string host = ctx.Request.Host.Host;
            if (loopback && host is not ("127.0.0.1" or "localhost"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            byte[] given = Encoding.UTF8.GetBytes(ctx.Request.Headers.Authorization.ToString());
            if (!CryptographicOperations.FixedTimeEquals(given, expected))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("The editor's token is missing or wrong: copy it again from the editor's Settings.");
                return;
            }
            await next();
        });
        app.MapMcp("/mcp");
        try
        {
            await app.StartAsync();
        }
        catch
        {
            // Not started (the port taken): the host is let go before the failure goes on to the caller.
            await app.DisposeAsync();
            throw;
        }
        return new ClaudeServer(app, port);
    }

    /// <summary>Stops the server and frees its port.</summary>
    internal async Task Stop()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>
    /// The server's own log entries (the MCP library's, the web host's) that carry an exception, errors and worse, into
    /// the editor's log: a tool that throws is answered with a bare "An error occurred", the reason written here.
    /// </summary>
    internal sealed class ErrorsToLog : ILoggerProvider, ILogger
    {
        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => this;

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) && exception != null)
            {
                TerrainEditor.App.Log.Caught(exception, "serving Claude: " + formatter(state, exception).TrimEnd('.'));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A host lifetime that leaves the process's signals alone: Ctrl+C is the editor's (Program); the console
    /// lifetime would take it and stop only this server.
    /// </summary>
    private sealed class NoSignals : IHostLifetime
    {
        /// <inheritdoc/>
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
