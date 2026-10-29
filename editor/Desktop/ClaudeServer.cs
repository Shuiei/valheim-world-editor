using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
/// address). On another address, only this computer and computers on its own network (local addresses, never the
/// internet's), and scripts for this computer's Claude only: the connection is not encrypted. What Claude changes in a world
/// stays pending: it never saves a world or applies live. Only the Workshop's building can be kept, as a blueprint of
/// the user's library (save_blueprint, which never replaces one without being told to).
/// </summary>
#pragma warning disable S3453 // Made by its static Start, the only way to get one.
public sealed class ClaudeServer
#pragma warning restore S3453
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
    public static string Url(int port, string? address = null) =>
        McpUrl(ParseAddress(address) is { } ip && !IsEveryAddress(ip) ? ip.ToString() : "127.0.0.1", port);

    /// <summary>The MCP server's URL on a host (IPv6 addresses in brackets, as URLs write them) and port.</summary>
    /// <param name="host">An address or a name.</param>
    /// <param name="port">The port.</param>
    private static string McpUrl(string host, int port) => new UriBuilder(Uri.UriSchemeHttp, host, port, "mcp").Uri.AbsoluteUri;

    /// <summary>An address and port as an endpoint is written: 0.0.0.0:5731, [::]:5731.</summary>
    /// <param name="ip">The address.</param>
    /// <param name="port">The port.</param>
    public static string Endpoint(IPAddress ip, int port) => new UriBuilder(Uri.UriSchemeHttp, ip.ToString(), port).Uri.Authority;

    /// <summary>
    /// The address another computer connects to when the server listens on every address: this computer's name, as
    /// the network's name service answers it (a name without a domain gets .local, mDNS's, which Windows, macOS and
    /// Linux usually answer; not every network does, see <see cref="RemoteAddressUrls"/>).
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="hostName">This computer's name (null: asked of the system now).</param>
    public static string RemoteUrl(int port, string? hostName = null)
    {
        string host = hostName ?? Dns.GetHostName();
        return McpUrl(host.Contains('.', StringComparison.Ordinal) ? host : host + ".local", port);
    }

    /// <summary>
    /// The addresses another computer connects to when the server listens on every address, by this computer's
    /// network addresses: they work where the name does not resolve (no mDNS), until the network gives this computer
    /// another address.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="addresses">This computer's addresses (null: <see cref="LocalAddresses"/> now).</param>
    public static IReadOnlyList<string> RemoteAddressUrls(int port, IReadOnlyList<(IPAddress Address, string Network)>? addresses = null) =>
        [.. (addresses ?? LocalAddresses()).Select(a => McpUrl(a.Address.ToString(), port))];

    /// <summary>
    /// Every address another computer connects to when the server listens on every address, said in one line: by
    /// name, then by each network address.
    /// </summary>
    /// <param name="port">The port.</param>
    private static string RemoteUrls(int port) => string.Join(" or ", RemoteAddressUrls(port).Prepend(RemoteUrl(port)));

    /// <summary>Whether the address is every address (0.0.0.0, ::): every network this computer is on.</summary>
    /// <param name="ip">The address.</param>
    public static bool IsEveryAddress(IPAddress ip) => ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any);

    /// <summary>
    /// One of this computer's addresses: the network's size (prefix length), the network's name (Ethernet, Wi-Fi…), and
    /// whether other computers reach it there (see <see cref="Unicasts"/>).
    /// </summary>
    /// <param name="Address">The address.</param>
    /// <param name="PrefixLength">The network's prefix length (24 for 255.255.255.0).</param>
    /// <param name="Network">The network's name.</param>
    /// <param name="Reachable">Whether other computers reach this computer there.</param>
    internal readonly record struct Unicast(IPAddress Address, int PrefixLength, string Network, bool Reachable);

    /// <summary>
    /// This computer's addresses on the networks that are up, loopback left out. Reachable: on a network with a
    /// gateway, not a tunnel (a VPN's); Docker's, WSL's and Hyper-V's own networks, and links to nothing, have no
    /// gateway. None when the system will not say.
    /// </summary>
    internal static IReadOnlyList<Unicast> Unicasts()
    {
        try
        {
            return [.. NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n =>
                {
                    var p = n.GetIPProperties();
                    bool reachable = n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                        && p.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any));
                    return p.UnicastAddresses
                        .Where(a => !IPAddress.IsLoopback(a.Address))
                        .Select(a => new Unicast(a.Address, a.PrefixLength, n.Name, reachable));
                })];
        }
        catch (NetworkInformationException e)
        {
            Log.Caught(e, "listing this computer's network addresses");
            return [];
        }
    }

    /// <summary>
    /// This computer's IPv4 addresses that other computers reach (<see cref="Unicasts"/>, 169.254.x.x left out: the
    /// address a computer gives itself when no network answers), once each, with each network's name, for Settings'
    /// choices.
    /// </summary>
    public static IReadOnlyList<(IPAddress Address, string Network)> LocalAddresses() =>
        [.. Unicasts()
            .Where(u => u.Reachable && u.Address.AddressFamily == AddressFamily.InterNetwork && !InNetwork(u.Address, LinkLocalV4, 16))
            .DistinctBy(u => u.Address)
            .Select(u => (u.Address, u.Network))];

    /// <summary>
    /// 100.64.0.0, IPv4's shared address space (/10): VPNs such as Tailscale give their computers addresses there, and
    /// it is never routed on the internet.
    /// </summary>
    private static readonly IPAddress SharedV4 = new([100, 64, 0, 0]);

    /// <summary>169.254.0.0, IPv4's link-local network (/16).</summary>
    private static readonly IPAddress LinkLocalV4 = new([169, 254, 0, 0]);

    /// <summary>
    /// Whether a caller is this computer: a loopback address, or one of this computer's own (a Claude here that
    /// connects to this computer's network address).
    /// </summary>
    /// <param name="caller">The caller's address.</param>
    /// <param name="unicasts">This computer's addresses (<see cref="Unicasts"/>).</param>
    internal static bool IsThisComputer(IPAddress caller, IReadOnlyList<Unicast> unicasts)
    {
        caller = Unmapped(caller);
        return IPAddress.IsLoopback(caller) || unicasts.Any(u => Unmapped(u.Address).Equals(caller));
    }

    /// <summary>
    /// Whether another computer may connect to a server listening on a non-loopback address: its address must be a
    /// local one (10/8, 172.16/12, 192.168/16, link-local; IPv6 unique local fc00::/7 or link-local fe80::/10), never
    /// one from the internet, and on the network of the address the server listens on (on every address: a network
    /// other computers reach this one on, see <see cref="Unicasts"/>). Or a VPN's (100.64/10, Tailscale's) when this
    /// computer is on that VPN too and the server listens there or on every address.
    /// </summary>
    /// <param name="caller">The caller's address.</param>
    /// <param name="bound">The address the server listens on.</param>
    /// <param name="unicasts">This computer's addresses (<see cref="Unicasts"/>).</param>
    internal static bool OnOwnNetwork(IPAddress caller, IPAddress bound, IReadOnlyList<Unicast> unicasts)
    {
        caller = Unmapped(caller);
        // A VPN gives each computer one address there (Tailscale's network is a single address, with no gateway), so
        // the network is the VPN: taken when this computer has an address on it too.
        if (InNetwork(caller, SharedV4, 10))
        {
            return (IsEveryAddress(bound) || InNetwork(Unmapped(bound), SharedV4, 10)) && unicasts.Any(u => InNetwork(Unmapped(u.Address), SharedV4, 10));
        }
        if (!IsLocalRange(caller))
        {
            return false;
        }
        var networks = IsEveryAddress(bound) ? unicasts.Where(u => u.Reachable) : unicasts.Where(u => Unmapped(u.Address).Equals(Unmapped(bound)));
        return networks.Any(u => InNetwork(caller, Unmapped(u.Address), u.PrefixLength));
    }

    /// <summary>Whether an address is in a local range (private IPv4, link-local, IPv6 unique local), never the internet's.</summary>
    /// <param name="ip">The address (IPv4-mapped IPv6 already made IPv4).</param>
    private static bool IsLocalRange(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetwork
        ? InNetwork(ip, new([10, 0, 0, 0]), 8) || InNetwork(ip, new([172, 16, 0, 0]), 12) || InNetwork(ip, new([192, 168, 0, 0]), 16)
            || InNetwork(ip, LinkLocalV4, 16)
        : ip.IsIPv6UniqueLocal || ip.IsIPv6LinkLocal;

    /// <summary>Whether an address is in a network (same family, the first prefixLength bits the same).</summary>
    /// <param name="ip">The address.</param>
    /// <param name="network">An address of the network.</param>
    /// <param name="prefixLength">The network's prefix length.</param>
    internal static bool InNetwork(IPAddress ip, IPAddress network, int prefixLength)
    {
        if (ip.AddressFamily != network.AddressFamily)
        {
            return false;
        }
        byte[] a = ip.GetAddressBytes(), b = network.GetAddressBytes();
        int whole = Math.Min(prefixLength / 8, a.Length);
        if (!a.AsSpan(0, whole).SequenceEqual(b.AsSpan(0, whole)))
        {
            return false;
        }
        int bits = prefixLength % 8;
        int mask = 0xFF << (8 - bits) & 0xFF;
        return bits == 0 || whole >= a.Length || (a[whole] & mask) == (b[whole] & mask);
    }

    /// <summary>An IPv4-mapped IPv6 address (::ffff:192.168.1.2, as a dual-mode socket gives it) as the IPv4 one.</summary>
    /// <param name="ip">The address.</param>
    private static IPAddress Unmapped(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    /// <summary>
    /// Whether the request being answered came from another computer (<see cref="IsThisComputer"/> false): Claude's
    /// scripts are refused to it (<see cref="ClaudeTools"/>). Set by the server for each request; false outside one.
    /// </summary>
    internal static readonly AsyncLocal<bool> FromAnotherComputer = new();

    /// <summary>What to type to connect Claude Code to the editor.</summary>
    /// <param name="port">The port.</param>
    /// <param name="token">The token.</param>
    /// <param name="address">The address the server listens on (null: 127.0.0.1).</param>
    public static string ClaudeCodeCommand(int port, string token, string? address = null) =>
        $"claude mcp add --transport http valheim-editor {Url(port, address)} --header \"Authorization: Bearer {token}\"";

    /// <summary>
    /// The settings' or typed address, or null when it is not one written in full: IPv4 as its four numbers (the
    /// system would also read "0" as 0.0.0.0, every network, and "10.1" as 10.0.0.1), IPv6 with its colons and without
    /// a zone (fe80::1%2: URLs drop the zone, so Claude could not connect). Names are not taken.
    /// </summary>
    /// <param name="address">The address.</param>
    public static IPAddress? ParseAddress(string? address)
    {
        string text = address?.Trim() ?? "";
        if (!IPAddress.TryParse(text, out var ip))
        {
            return null;
        }
        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork when ip.ToString() == text => ip,
            AddressFamily.InterNetworkV6 when text.Contains(':', StringComparison.Ordinal) && !text.Contains('%', StringComparison.Ordinal) && ip.ScopeId == 0 => ip,
            _ => null,
        };
    }

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
                Status = $"Could not listen on {settings.ClaudeAddress}: not an IP address written in full (127.0.0.1, 0.0.0.0, or one of this computer's)";
                return Status;
            }
            try
            {
                _running = await Start(settings.ClaudePort, settings.ClaudeToken!, new ClaudeTools(window), address);
                Status = IPAddress.IsLoopback(address)
                    ? $"Listening on {Url(settings.ClaudePort, settings.ClaudeAddress)}"
                    : IsEveryAddress(address)
                        ? $"Listening on {Endpoint(address, settings.ClaudePort)}, from other computers too ({RemoteUrls(settings.ClaudePort)}; here: {Url(settings.ClaudePort)}), unencrypted"
                        : $"Listening on {Url(settings.ClaudePort, settings.ClaudeAddress)}, from other computers too, unencrypted";
                Console.WriteLine($"claude: {Status}");
                return null;
            }
            catch (Exception e) when (e is IOException or SocketException or InvalidOperationException)
            {
                // The address as well as the port: a network address can be gone (a new one from the network, Wi-Fi
                // not up yet) as a port can be taken.
                Log.Caught(e, $"starting the server for Claude on {Endpoint(address, settings.ClaudePort)}");
                Status = $"Could not listen on {Endpoint(address, settings.ClaudePort)}: {e.Message}";
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
        // On a loopback address, the names this computer is reached by: 127.0.0.1, localhost and the address itself
        // (::1 as a Host is written [::1]). Null on another address: any name is taken there.
        HashSet<string>? localHosts = IPAddress.IsLoopback(address)
            ? new(StringComparer.OrdinalIgnoreCase) { "127.0.0.1", "localhost", address.ToString(), $"[{address}]" }
            : null;
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
            // On a loopback address, this computer's own names only (a web page reaching it under another name is
            // refused); on another address, other computers reach it by any of this one's names. The token, always.
            if (localHosts != null && !localHosts.Contains(ctx.Request.Host.Host))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            // On another address, only this computer and the computers of the network it listens on (local addresses,
            // never the internet's: :: also listens on a global IPv6 address).
            bool here = localHosts != null;
            if (!here)
            {
                var caller = ctx.Connection.RemoteIpAddress ?? IPAddress.None;
                var unicasts = Unicasts();
                here = IsThisComputer(caller, unicasts);
                if (!here && !OnOwnNetwork(caller, address, unicasts))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsync("Only this computer and computers on its own network may connect to the editor.");
                    return;
                }
            }
            byte[] given = Encoding.UTF8.GetBytes(ctx.Request.Headers.Authorization.ToString());
            if (!CryptographicOperations.FixedTimeEquals(given, expected))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("The editor's token is missing or wrong: copy it again from the editor's Settings.");
                return;
            }
            // For the tools: scripts are this computer's only (the connection from another one is not encrypted).
            FromAnotherComputer.Value = !here;
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
