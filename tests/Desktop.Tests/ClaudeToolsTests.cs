using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Claude's connection (ClaudeServer, ClaudeTools): looking at a world and shaping it with scripts (one
// pending step each, nothing on mistakes, undo), never over unsaved changes, never while live with Auto
// on; building in the Workshop (pieces snapped as the hammer does, the support check naming what would
// fall); and the server only answering 127.0.0.1 callers with the token.
[Collection("World files")]
public class ClaudeToolsTests
{
	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public string Homestead { get; } = Path.Combine(Path.GetTempPath(), "vwe-claude-" + Guid.NewGuid().ToString("N")[..8]);
		public FakeGame? Game { get; private set; }
		public ClaudeTools T { get; }

		public Run()
		{
			W.Show();
			W.Tell = _ => Task.CompletedTask;
			W.Ask = (_, _, _, _) => Task.FromResult(false);
			W.SaveSettings = _ => { };
			W.Settings.AutoApply = W.Settings.AreaFollow = false;
			W.Blueprints.FindHomestead = () => new TerrainEditor.App.Homestead.Status(true, "1.3.2", new() { "test" }, Homestead);
			T = new ClaudeTools(W);
		}

		public async Task OpenLive()
		{
			Game = new FakeGame();
			var game = Game;
			await W.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
			W.MapPage?.Stop();
			await W.EditArea(0, 0, 1);
		}

		public void Dispose()
		{
			Game?.Dispose();
			foreach (string d in new[] { Path.GetDirectoryName(Dir)!, Homestead })
			{
				try
				{
					Directory.Delete(d, true);
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement;

	[AvaloniaFact]
	public async Task AWorldIsLookedAtAndShapedWithPendingSteps()
	{
		using var r = new Run();
		var state = J(await r.T.EditorState());
		Assert.Equal("start", state.GetProperty("page").GetString());
		Assert.Equal("map", J(await r.T.OpenWorld(r.Dir)).GetProperty("page").GetString());
		var area = J(await r.T.OpenArea(0, 0, 1)).GetProperty("area");
		Assert.Equal(-32, area.GetProperty("minX").GetSingle());
		Assert.Equal(32, area.GetProperty("maxZ").GetSingle());
		var described = J(await r.T.DescribeArea());
		Assert.Contains(described.GetProperty("objectsByKind").EnumerateArray(), k => k.GetProperty("kind").GetString() == "Trees");
		var trees = J(await r.T.ListObjects(kind: "Trees", limit: 2));
		Assert.Equal(2, trees.GetArrayLength());
		Assert.Contains("Area", ClaudeTools.ScriptReference());
		// A script: one pending step, its prints and what it changed.
		string said = await r.T.RunScript("Ground.Raise(0, 0, 3); Objects.Place(\"Beech1\", 5, 5); Print(\"done\");", "a hill");
		Assert.StartsWith("done", said);
		Assert.Contains("1 object(s) placed", said);
		Assert.Equal((1, 0, 1, 0), (r.W.World!.Pending.Zones, r.W.World.Pending.Deleted, r.W.World.Pending.Added, r.W.World.Pending.Resets));
		Assert.Equal("Claude: a hill", r.W.Session!.UndoLabel);
		// Mistakes: which line, nothing changed.
		string wrong = await r.T.RunScript("Ground.Raise(0, 0);");
		Assert.Contains("line 1", wrong);
		Assert.Equal(1, r.W.World.Pending.Added);
		// Another world is not opened over unsaved changes.
		Assert.Contains("ask the user", await r.T.OpenWorld(r.Dir));
		Assert.StartsWith("Undone. Now pending: nothing", await r.T.Undo());
		Assert.Equal("Nothing to undo.", await r.T.Undo());
		Assert.StartsWith("Redone.", await r.T.Redo());
	}

	[AvaloniaFact]
	public async Task LiveWithAutoOnClaudeChangesNothing()
	{
		using var r = new Run();
		await r.OpenLive();
		r.W.Settings.AutoApply = true;
		Assert.Contains("Auto on", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
		Assert.Contains("Auto on", await r.T.PlacePieces(new[] { new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 0 } }));
		Assert.Contains("Auto on", await r.T.RemoveObjects(new[] { 0 }));
		Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
		// Auto off: Claude's change waits for Apply live.
		r.W.Settings.AutoApply = false;
		Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
		Assert.Equal(1, r.W.World.Pending.Zones);
		Assert.Empty(r.Game!.Terrain);
	}

	[AvaloniaFact]
	public async Task PiecesSnapAsTheHammersAndTheSupportCheckNamesWhatFalls()
	{
		using var r = new Run();
		var state = J(await r.T.OpenWorkshop());
		Assert.Equal("workshop", state.GetProperty("page").GetString());
		// A wall, then one 0.3 m off where it would join: it snaps to its edge. A floor up in the air
		// (not snapped) would fall. Unknown kinds and points off the plot are said, not placed.
		var placed = J(await r.T.PlacePieces(new[]
		{
			new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 0 },
			new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 2.3f, Z = 0 },
			new ClaudeTools.PieceSpec { Prefab = "wood_floor", X = 10, Z = 10, Y = 40, Snap = false },
			new ClaudeTools.PieceSpec { Prefab = "no_such_piece", X = 0, Z = 0 },
			new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 500, Z = 0 },
		}, "walls"));
		var list = placed.GetProperty("placed").EnumerateArray().ToList();
		Assert.Equal(3, list.Count);
		var first = list[0].GetProperty("piece");
		Assert.Equal(TerrainEditor.Desktop.Workshop.Ground + 1, first.GetProperty("y").GetSingle(), 2);
		var second = list[1].GetProperty("piece");
		Assert.True(second.GetProperty("snapped").GetBoolean());
		Assert.Equal(2, second.GetProperty("x").GetSingle(), 2);
		Assert.Equal(2, placed.GetProperty("problems").GetArrayLength());
		Assert.Equal("Claude: walls", r.W.Session!.UndoLabel);
		var support = J(await r.T.SupportCheck());
		Assert.Equal(1, support.GetProperty("wouldFall").GetInt32());
		int floating = list[2].GetProperty("id").GetInt32();
		Assert.Equal(floating, support.GetProperty("falling")[0].GetProperty("id").GetInt32());
		Assert.StartsWith("Took 1 away", await r.T.RemoveObjects(new[] { floating }));
		Assert.Equal(0, J(await r.T.SupportCheck()).GetProperty("wouldFall").GetInt32());
		// The plot is not saved as a blueprint: no world is opened over it.
		Assert.Contains("blueprint", await r.T.OpenWorld(r.Dir));
		Assert.Contains("No blueprint", await r.T.AddBlueprint("Nothing like this"));
	}

	// The server: only for callers with the token, only under this computer's address; then MCP.
	[AvaloniaFact]
	public async Task TheServerAnswersOnlyWithTheTokenOnThisComputer()
	{
		using var r = new Run();
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
			Assert.Contains("valheim-world-editor", await init.Content.ReadAsStringAsync(ct));
			var tools = Rpc("tools/list", "secret");
			tools.Headers.Add("MCP-Protocol-Version", "2025-06-18");
			string listed = await (await http.SendAsync(tools, ct)).Content.ReadAsStringAsync(ct);
			foreach (string name in new[] { "editor_state", "open_world", "open_area", "screenshot", "run_script", "place_pieces", "support_check", "generate_dungeon" })
			{
				Assert.Contains($"\"{name}\"", listed);
			}
			// No tool saves, applies live or saves a blueprint.
			Assert.DoesNotContain("\"save", listed);
			Assert.DoesNotContain("apply_live", listed);
		}
		finally
		{
			await server.Stop();
		}
		Assert.Contains("--header \"Authorization: Bearer abc\"", ClaudeServer.ClaudeCodeCommand(5731, "abc"));
		Assert.Equal(48, ClaudeServer.NewToken().Length);
	}
}
