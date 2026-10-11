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
/// as the user would. Only on 127.0.0.1, only for callers that send the token (which also keeps web
/// pages out: they cannot send it, nor a Host other than this computer's address). What Claude changes
/// stays pending: it never saves, applies live or saves a blueprint.
/// </summary>
public sealed class ClaudeServer
{
    /// <summary>The server the settings started, null when off.</summary>
    private static ClaudeServer? _running;
    /// <summary>One <see cref="Apply"/> at a time.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The web host the server runs in.</summary>
    private readonly WebApplication _app;

    /// <summary>The port it listens on, on 127.0.0.1.</summary>
    public int Port { get; }

    /// <summary>A server already started (see <see cref="Start"/>).</summary>
    private ClaudeServer(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    /// <summary>The address Claude connects to.</summary>
    public static string Url(int port) => $"http://127.0.0.1:{port}/mcp";

    /// <summary>What to type to connect Claude Code to the editor.</summary>
    public static string ClaudeCodeCommand(int port, string token) =>
        $"claude mcp add --transport http valheim-editor {Url(port)} --header \"Authorization: Bearer {token}\"";

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
                Status = null;
            }
            if (!settings.ClaudeConnect)
            {
                return null;
            }
            if (string.IsNullOrEmpty(settings.ClaudeToken))
            {
                settings.ClaudeToken = NewToken();
                settings.Save();
            }
            try
            {
                _running = await Start(settings.ClaudePort, settings.ClaudeToken!, new ClaudeTools(window));
                Status = $"Listening on {Url(settings.ClaudePort)}";
                Console.WriteLine($"claude: {Status}");
                return null;
            }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or InvalidOperationException)
            {
                Status = $"Could not listen on port {settings.ClaudePort}: {e.Message}";
                Console.WriteLine($"claude: {Status}");
                return Status;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The server on 127.0.0.1:port with these tools (tests start their own).</summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="token">The secret callers must send as "Authorization: Bearer &lt;token&gt;".</param>
    /// <param name="tools">The tools Claude is given.</param>
    internal static async Task<ClaudeServer> Start(int port, string token, ClaudeTools tools)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        // The host's console lifetime would take Ctrl+C (SIGINT) from the editor started in a terminal
        // and stop only this server, leaving the editor open: the signals are the editor's (Program).
        builder.Services.AddSingleton<IHostLifetime, NoSignals>();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
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
            // This computer's address only (a web page reaching it under another name is refused), and
            // the token.
            string host = ctx.Request.Host.Host;
            if (host is not ("127.0.0.1" or "localhost"))
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
        await app.StartAsync();
        return new ClaudeServer(app, port);
    }

    /// <summary>Stops the server and frees its port.</summary>
    internal async Task Stop()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
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
