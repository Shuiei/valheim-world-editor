using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Claude tools' transport: the --mcp-stdio relay with the token, and the server that answers only callers holding the token.
/// </summary>
[Collection("World files")]
public class ClaudeToolsTransportTests
{
    // --mcp-stdio: messages relayed with the token, the protocol remembered; refused clearly when the
    // switch is off or the editor does not answer.
    [Fact]
    public async Task TheRelayPassesMessagesWithTheToken()
    {
        var seen = new List<HttpRequestMessage>();
        var handler = new Answer(req =>
        {
            seen.Add(req);
            string body = req.Content!.ReadAsStringAsync().Result;
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body.Contains("initialize") ? "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2025-06-18\"}}\n\n" : "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}\n\n"),
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return res;
        });
        var on = new TerrainEditor.App.AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799 };
        var output = new StringWriter();
        await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}\n\n{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}\n"), output, () => on, handler);
        string[] lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"protocolVersion\":\"2025-06-18\"", lines[0]);
        Assert.Equal("Bearer tok", seen[0].Headers.Authorization!.ToString());
        Assert.Equal("http://127.0.0.1:5799/mcp", seen[0].RequestUri!.ToString());
        Assert.False(seen[0].Headers.Contains("MCP-Protocol-Version"));
        Assert.Equal("2025-06-18", seen[1].Headers.GetValues("MCP-Protocol-Version").Single());
        var off = new StringWriter();
        await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/list\"}\n{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n"), off, () => new TerrainEditor.App.AppSettings(), handler);
        string[] refused = off.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(refused);
        Assert.Contains("Allow Claude to connect", refused[0]);
        Assert.Contains("\"id\":7", refused[0]);
        var down = new StringWriter();
        await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}\n"), down, () => on, new Answer(_ => throw new HttpRequestException("refused")));
        Assert.Contains("not running", down.ToString());
    }

    /// <summary>
    /// An HTTP handler answering every request with the given function, without a network.
    /// </summary>
    private sealed class Answer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    // The server: only for callers with the token, only under this computer's address; then MCP.
    [AvaloniaFact]
    public async Task TheServerAnswersOnlyWithTheTokenOnThisComputer()
    {
        using var r = new ClaudeRun();
        int port = LoopbackPort.Free();
        var server = await ClaudeServer.Start(port, "secret", r.T);
        try
        {
            using var http = new HttpClient();
            string url = ClaudeServer.Url(port);
            var ct = TestContext.Current.CancellationToken;
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc(url, "initialize", null), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc(url, "initialize", "wrong"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Rpc(url, "initialize", "secret", $"evil.example:{port}"), ct)).StatusCode);
            var init = await http.SendAsync(Rpc(url, "initialize", "secret"), ct);
            Assert.Equal(HttpStatusCode.OK, init.StatusCode);
            string initialized = await init.Content.ReadAsStringAsync(ct);
            Assert.Contains("valheim-world-editor", initialized);
            Assert.Contains("You are connected to Valheim World Editor", initialized);
            var prompts = Rpc(url, "prompts/list", "secret");
            prompts.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            string promptList = await (await http.SendAsync(prompts, ct)).Content.ReadAsStringAsync(ct);
            Assert.Contains("build_in_workshop", promptList);
            Assert.Contains("shape_area", promptList);
            var tools = Rpc(url, "tools/list", "secret");
            tools.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            string listed = await (await http.SendAsync(tools, ct)).Content.ReadAsStringAsync(ct);
            foreach (string name in new[] { "editor_state", "open_world", "open_area", "screenshot", "area_map", "run_script", "place_pieces", "support_check", "generate_dungeon",
                "building_guide", "get_selection", "select_objects", "flatten", "paint_area", "road", "forest", "set_contents", "build_floor", "build_walls", "build_roof",
                "move_objects", "set_transform", "replace_objects", "get_object", "set_object_data",
                "search_world", "list_locations", "copy_objects", "add_blueprint", "shape_ground", "regrow_nature", "ground_at",
                "dig_path", "reset_zones", "preview_seed", "create_world", "stamp", "import_heightmap",
                "list_dungeons", "dungeon_rooms", "add_dungeon_room", "remove_dungeon_room", "close_dungeon_ends", "set_dungeon_door",
                "list_controls", "set_control", "save_blueprint" })
            {
                Assert.Contains($"\"{name}\"", listed);
            }
            // No tool saves a world or applies live; saving a blueprint is a tool.
            Assert.DoesNotContain("\"save_world", listed);
            Assert.DoesNotContain("apply_live", listed);
            Assert.Contains("\"save_blueprint\"", listed);
        }
        finally
        {
            await server.Stop();
        }
        Assert.Contains("--header \"Authorization: Bearer abc\"", ClaudeServer.ClaudeCodeCommand(5731, "abc"));
        Assert.Equal(48, ClaudeServer.NewToken().Length);
    }

    // On every address (Settings, Address 0.0.0.0 or ::): other computers reach it under this one's names, so any Host
    // is taken; the token is still asked for. Reached here at this computer's 127.0.0.1, the address the command gives.
    [AvaloniaTheory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    public async Task OnEveryAddressAnyHostIsTakenButNotWithoutTheToken(string address)
    {
        using var r = new ClaudeRun();
        var (server, port) = await StartOrSkip(r.T, IPAddress.Parse(address));
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc(url, "initialize", null, $"my-pc.local:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Rpc(url, "initialize", "secret", $"my-pc.local:{port}"), ct)).StatusCode);
        }
        finally
        {
            await server.Stop();
        }
    }

    // On a loopback address other than 127.0.0.1 (::1, 127.0.0.2): the command's address, the server's own, is taken
    // with the token, as 127.0.0.1 and localhost are; another name is still refused.
    [AvaloniaTheory]
    [InlineData("::1")]
    [InlineData("127.0.0.2")]
    public async Task OnALoopbackAddressItsOwnNameIsTaken(string address)
    {
        using var r = new ClaudeRun();
        var (server, port) = await StartOrSkip(r.T, IPAddress.Parse(address));
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Rpc(url, "initialize", "secret"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Rpc(url, "initialize", "secret", $"localhost:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Rpc(url, "initialize", "secret", $"evil.example:{port}"), ct)).StatusCode);
        }
        finally
        {
            await server.Stop();
        }
    }

    // On one of this computer's network addresses (Settings' list): reached there under any name, with the token only.
    // This computer calling its own network address is this computer.
    [AvaloniaFact]
    public async Task OnANetworkAddressAnyHostIsTakenButNotWithoutTheToken()
    {
        var lan = ClaudeServer.LocalAddresses();
        Assert.SkipWhen(lan.Count == 0, "This computer has no network address but loopback.");
        using var r = new ClaudeRun();
        var address = lan[0].Address;
        var (server, port) = await StartOrSkip(r.T, address);
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address.ToString());
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc(url, "initialize", null, $"my-pc.local:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Rpc(url, "initialize", "secret", $"my-pc.local:{port}"), ct)).StatusCode);
        }
        finally
        {
            await server.Stop();
        }
    }

    // Who may connect on another address than loopback: this computer (loopback, or one of its own addresses, IPv4 as
    // a dual-mode socket gives it too), and computers with a local address (private IPv4, link-local, IPv6 unique local)
    // on the network listened on: on every address, the networks other computers reach this one on (not Docker's,
    // which has no gateway); on one address, that one's network, even without a gateway. Never the internet's.
    [Fact]
    public void OnlyThisComputersOwnNetworksMayConnect()
    {
        ClaudeServer.Unicast[] unicasts =
        [
            new(IPAddress.Parse("192.168.1.20"), 24, "Ethernet", true),
            new(IPAddress.Parse("172.17.0.1"), 16, "docker0", false),
            new(IPAddress.Parse("fd00::20"), 64, "Ethernet", true),
        ];
        bool Here(string caller) => ClaudeServer.IsThisComputer(IPAddress.Parse(caller), unicasts);
        bool May(string caller, string bound) => ClaudeServer.OnOwnNetwork(IPAddress.Parse(caller), IPAddress.Parse(bound), unicasts);
        Assert.True(Here("127.0.0.1"));
        Assert.True(Here("::1"));
        Assert.True(Here("192.168.1.20"));
        Assert.True(Here("::ffff:192.168.1.20"));
        Assert.False(Here("192.168.1.30"));
        Assert.True(May("192.168.1.30", "0.0.0.0"));
        Assert.True(May("::ffff:192.168.1.30", "::"));
        Assert.True(May("fd00::30", "::"));
        Assert.True(May("192.168.1.30", "192.168.1.20"));
        // Another network: a private address on none of this computer's, Docker's on every address, another than the
        // one listened on.
        Assert.False(May("192.168.2.30", "0.0.0.0"));
        Assert.False(May("172.17.0.2", "0.0.0.0"));
        Assert.True(May("172.17.0.2", "172.17.0.1"));
        Assert.False(May("172.17.0.2", "192.168.1.20"));
        // The internet: never, even inside a network this computer is on (a global IPv6 address, say).
        Assert.False(May("203.0.113.5", "0.0.0.0"));
        Assert.False(May("2001:db8::5", "::"));
        Assert.True(ClaudeServer.InNetwork(IPAddress.Parse("172.31.255.255"), IPAddress.Parse("172.16.0.0"), 12));
        Assert.False(ClaudeServer.InNetwork(IPAddress.Parse("172.32.0.0"), IPAddress.Parse("172.16.0.0"), 12));
        Assert.False(ClaudeServer.InNetwork(IPAddress.Parse("::ffff:172.16.0.1"), IPAddress.Parse("172.16.0.0"), 12));
    }

    // --mcp-stdio relays to the address the settings choose: the chosen one, or this computer's 127.0.0.1 for every
    // network.
    [Theory]
    [InlineData("192.0.2.20", "192.0.2.20:5799")]
    [InlineData("2001:db8::1", "[2001:db8::1]:5799")]
    [InlineData("0.0.0.0", "127.0.0.1:5799")]
    public async Task TheRelayGoesToTheChosenAddress(string address, string authority)
    {
        var seen = new List<Uri>();
        var handler = new Answer(req =>
        {
            seen.Add(req.RequestUri!);
            var res = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}\n\n") };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return res;
        });
        var on = new TerrainEditor.App.AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799, ClaudeAddress = address };
        await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}\n"), new StringWriter(), () => on, handler);
        Assert.Equal(McpUrl(authority), Assert.Single(seen).ToString());
    }

    /// <summary>
    /// The MCP server's URL at an authority (host:port, IPv6 in brackets), as the editor gives it: plain HTTP by design
    /// (see ClaudeServer).
    /// </summary>
    internal static string McpUrl(string authority) => $"{Uri.UriSchemeHttp}://{authority}/mcp";

    /// <summary>
    /// The server on that address, on a port free there, or the test skipped when this computer has no such address:
    /// IPv6 or a network address only (a container may have neither), never 0.0.0.0 or 127.0.0.x, whose failures are
    /// the server's.
    /// </summary>
    private static async Task<(ClaudeServer Server, int Port)> StartOrSkip(ClaudeTools tools, IPAddress address)
    {
        bool mayBeMissing = address.AddressFamily == AddressFamily.InterNetworkV6 || !(IPAddress.IsLoopback(address) || ClaudeServer.IsEveryAddress(address));
        try
        {
            int port = LoopbackPort.Free(address);
            return (await ClaudeServer.Start(port, "secret", tools, address), port);
        }
        catch (Exception e) when (mayBeMissing && Missing(e))
        {
            Assert.Skip($"This computer cannot listen on {address}: {e.Message}");
            throw new System.Diagnostics.UnreachableException();
        }
    }

    /// <summary>Whether a failure to listen is the address missing here (not the system's family, or not this computer's).</summary>
    private static bool Missing(Exception? e)
    {
        for (; e != null; e = e.InnerException)
        {
            if (e is SocketException { SocketErrorCode: SocketError.AddressFamilyNotSupported or SocketError.AddressNotAvailable })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// An MCP request to the URL (initialize with its parameters, else none), with the token and the Host given (none:
    /// the URL's).
    /// </summary>
    private static HttpRequestMessage Rpc(string url, string method, string? token, string? host = null)
    {
        var m = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"{method}\",\"params\":{(method == "initialize" ? "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}" : "{}")}}}", Encoding.UTF8, "application/json"),
        };
        m.Headers.Accept.ParseAdd("application/json");
        m.Headers.Accept.ParseAdd("text/event-stream");
        if (token != null)
        {
            m.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (host != null)
        {
            m.Headers.Host = host;
        }
        return m;
    }

    // The address Claude connects to: this computer's 127.0.0.1 for 127.0.0.1 and every address, else the one chosen.
    // Only addresses written in full are taken: "0" would be every network, a zone would be lost in the URL.
    [Fact]
    public void TheUrlFollowsTheAddress()
    {
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "0.0.0.0"));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "::"));
        Assert.Equal(McpUrl("[::1]:5731"), ClaudeServer.Url(5731, "::1"));
        Assert.Equal(McpUrl("192.0.2.20:5731"), ClaudeServer.Url(5731, " 192.0.2.20 "));
        Assert.Equal(McpUrl("[2001:db8::1]:5731"), ClaudeServer.Url(5731, "2001:db8::1"));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "not an address"));
        Assert.Contains(McpUrl("192.0.2.20:6000") + " --header", ClaudeServer.ClaudeCodeCommand(6000, "abc", "192.0.2.20"));
        Assert.Null(ClaudeServer.ParseAddress("my-pc.local"));
        Assert.Equal(IPAddress.Any, ClaudeServer.ParseAddress("0.0.0.0"));
        Assert.Equal(IPAddress.IPv6Any, ClaudeServer.ParseAddress("::"));
        Assert.Null(ClaudeServer.ParseAddress("0"));
        Assert.Null(ClaudeServer.ParseAddress("1"));
        Assert.Null(ClaudeServer.ParseAddress("10.1"));
        Assert.Null(ClaudeServer.ParseAddress("010.0.0.1"));
        Assert.Null(ClaudeServer.ParseAddress("fe80::1%2"));
        // A choice's words are not an address: Settings keeps each choice's address beside them.
        Assert.Null(ClaudeServer.ParseAddress("0.0.0.0 (every network)"));
        Assert.True(ClaudeServer.IsEveryAddress(IPAddress.IPv6Any));
        Assert.False(ClaudeServer.IsEveryAddress(IPAddress.Loopback));
        // Endpoints as the status line writes them: IPv6 in brackets.
        Assert.Equal("0.0.0.0:5731", ClaudeServer.Endpoint(IPAddress.Any, 5731));
        Assert.Equal("[::]:5731", ClaudeServer.Endpoint(IPAddress.IPv6Any, 5731));
        // Another computer: this one's name, with .local when it has no domain.
        Assert.Matches(@"^http://[^/:]+\.[^/:]+:5731/mcp$", ClaudeServer.RemoteUrl(5731));
        Assert.StartsWith($"{Uri.UriSchemeHttp}://{Dns.GetHostName()}", ClaudeServer.RemoteUrl(5731), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(McpUrl("my-pc.local:5731"), ClaudeServer.RemoteUrl(5731, "my-pc"));
        Assert.Equal(McpUrl("my-pc.home.arpa:5731"), ClaudeServer.RemoteUrl(5731, "my-pc.home.arpa"));
        // This computer's addresses for other computers: no loopback, no 169.254.x.x (no network answered), once each.
        var local = ClaudeServer.LocalAddresses();
        Assert.All(local, a => Assert.False(IPAddress.IsLoopback(a.Address)));
        Assert.All(local, a => Assert.False(ClaudeServer.InNetwork(a.Address, IPAddress.Parse("169.254.0.0"), 16)));
        Assert.Equal(local.Count, local.Select(a => a.Address).Distinct().Count());
        // And by each network address, for networks that do not answer the name.
        Assert.Equal(local.Select(a => McpUrl($"{a.Address}:5731")), ClaudeServer.RemoteAddressUrls(5731));
        Assert.Equal([McpUrl("192.0.2.20:5731")], ClaudeServer.RemoteAddressUrls(5731, [(IPAddress.Parse("192.0.2.20"), "Ethernet")]));
    }
}
