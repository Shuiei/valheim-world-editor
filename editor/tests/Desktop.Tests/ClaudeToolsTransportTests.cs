using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Avalonia.Headless.XUnit;
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
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var server = await ClaudeServer.Start(port, "secret", r.T);
        try
        {
            using var http = new HttpClient();
            string url = ClaudeServer.Url(port);
            HttpRequestMessage Rpc(string method, string? token, string? host = null)
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
            var ct = TestContext.Current.CancellationToken;
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc("initialize", null), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Rpc("initialize", "wrong"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Rpc("initialize", "secret", $"evil.example:{port}"), ct)).StatusCode);
            var init = await http.SendAsync(Rpc("initialize", "secret"), ct);
            Assert.Equal(HttpStatusCode.OK, init.StatusCode);
            string initialized = await init.Content.ReadAsStringAsync(ct);
            Assert.Contains("valheim-world-editor", initialized);
            Assert.Contains("You are connected to Valheim World Editor", initialized);
            var prompts = Rpc("prompts/list", "secret");
            prompts.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            string promptList = await (await http.SendAsync(prompts, ct)).Content.ReadAsStringAsync(ct);
            Assert.Contains("build_in_workshop", promptList);
            Assert.Contains("shape_area", promptList);
            var tools = Rpc("tools/list", "secret");
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
        int port = FreePort();
        var server = await StartOrSkip(port, r.T, IPAddress.Parse(address));
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Init(url, null, $"my-pc.local:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Init(url, "secret", $"my-pc.local:{port}"), ct)).StatusCode);
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
        int port = FreePort();
        var server = await StartOrSkip(port, r.T, IPAddress.Parse(address));
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Init(url, "secret"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Init(url, "secret", $"localhost:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Init(url, "secret", $"evil.example:{port}"), ct)).StatusCode);
        }
        finally
        {
            await server.Stop();
        }
    }

    // On one of this computer's network addresses (Settings' list): reached there under any name, with the token only.
    [AvaloniaFact]
    public async Task OnANetworkAddressAnyHostIsTakenButNotWithoutTheToken()
    {
        var lan = ClaudeServer.LocalAddresses();
        Assert.SkipWhen(lan.Count == 0, "This computer has no network address but loopback.");
        using var r = new ClaudeRun();
        int port = FreePort();
        string address = lan[0].Address;
        var server = await StartOrSkip(port, r.T, IPAddress.Parse(address));
        try
        {
            using var http = new HttpClient();
            var ct = TestContext.Current.CancellationToken;
            string url = ClaudeServer.Url(port, address);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Init(url, null, $"my-pc.local:{port}"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Init(url, "secret", $"my-pc.local:{port}"), ct)).StatusCode);
        }
        finally
        {
            await server.Stop();
        }
    }

    // --mcp-stdio relays to the address the settings choose: the chosen one, or this computer's 127.0.0.1 for every
    // network.
    [Theory]
    [InlineData("192.0.2.20", "http://192.0.2.20:5799/mcp")]
    [InlineData("2001:db8::1", "http://[2001:db8::1]:5799/mcp")]
    [InlineData("0.0.0.0", "http://127.0.0.1:5799/mcp")]
    public async Task TheRelayGoesToTheChosenAddress(string address, string url)
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
        Assert.Equal(url, Assert.Single(seen).ToString());
    }

    /// <summary>A port free now on this computer.</summary>
    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>The server on that address, or the test skipped when this computer cannot listen there (no IPv6, say).</summary>
    private static async Task<ClaudeServer> StartOrSkip(int port, ClaudeTools tools, IPAddress address)
    {
        try
        {
            return await ClaudeServer.Start(port, "secret", tools, address);
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            Assert.Skip($"Cannot listen on {address} here: {e.Message}");
            throw;
        }
    }

    /// <summary>An MCP initialize request to the URL, with the token and the Host given (none: the URL's).</summary>
    private static HttpRequestMessage Init(string url, string? token, string? host = null)
    {
        var m = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}", Encoding.UTF8, "application/json"),
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
    [Fact]
    public void TheUrlFollowsTheAddress()
    {
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "0.0.0.0"));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "::"));
        Assert.Equal("http://[::1]:5731/mcp", ClaudeServer.Url(5731, "::1"));
        Assert.Equal("http://192.0.2.20:5731/mcp", ClaudeServer.Url(5731, " 192.0.2.20 "));
        Assert.Equal("http://[2001:db8::1]:5731/mcp", ClaudeServer.Url(5731, "2001:db8::1"));
        Assert.Equal("http://127.0.0.1:5731/mcp", ClaudeServer.Url(5731, "not an address"));
        Assert.Contains("http://192.0.2.20:6000/mcp --header", ClaudeServer.ClaudeCodeCommand(6000, "abc", "192.0.2.20"));
        Assert.Null(ClaudeServer.ParseAddress("my-pc.local"));
        Assert.Equal(IPAddress.Any, ClaudeServer.ParseAddress("0.0.0.0"));
        // A choice of Settings' list reads as its address.
        Assert.Equal(IPAddress.Any, ClaudeServer.ParseAddress("0.0.0.0 (every network)"));
        Assert.Equal("http://192.0.2.20:5731/mcp", ClaudeServer.Url(5731, "192.0.2.20 (Ethernet)"));
        Assert.True(ClaudeServer.IsEveryAddress(IPAddress.IPv6Any));
        Assert.False(ClaudeServer.IsEveryAddress(IPAddress.Loopback));
        // Endpoints as the status line writes them: IPv6 in brackets.
        Assert.Equal("0.0.0.0:5731", ClaudeServer.Endpoint(IPAddress.Any, 5731));
        Assert.Equal("[::]:5731", ClaudeServer.Endpoint(IPAddress.IPv6Any, 5731));
        // Another computer: this one's name, with .local when it has no domain.
        Assert.Matches(@"^http://[^/:]+\.[^/:]+:5731/mcp$", ClaudeServer.RemoteUrl(5731));
        Assert.StartsWith($"http://{Dns.GetHostName()}", ClaudeServer.RemoteUrl(5731), StringComparison.OrdinalIgnoreCase);
        Assert.All(ClaudeServer.LocalAddresses(), a => Assert.False(IPAddress.IsLoopback(IPAddress.Parse(a.Address))));
    }
}
