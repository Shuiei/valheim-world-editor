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
}
