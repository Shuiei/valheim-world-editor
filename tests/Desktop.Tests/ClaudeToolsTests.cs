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

	// Floors, walls and roofs over a rectangle, from the game's pieces: an 8 x 6 m house is 12 floor tiles,
	// 28 wall pieces with its door, 18 roof pieces with its gables (the 2 m under the ridge closed), and
	// it stands.
	[AvaloniaFact]
	public async Task AHouseIsBuiltFromRectanglesAndStands()
	{
		Assert.Equal(12, ClaudeBuilder.Floor(-4, -3, 4, 3, 34, "wood").Count);
		var walls = ClaudeBuilder.Walls(-4, -3, 4, 3, 34, 4, "wood", new[] { new ClaudeBuilder.Door("south", 0) });
		Assert.Equal(28, walls.Count);
		Assert.Single(walls, p => p.Prefab == "wood_door");
		var roof = ClaudeBuilder.Roof(-4, -3, 4, 3, 38, 45, "thatch", gables: true);
		Assert.Equal(18, roof.Count);
		Assert.Equal(4, roof.Count(p => p.Prefab == "wood_roof_top_45"));
		Assert.Throws<ArgumentException>(() => ClaudeBuilder.Roof(-4, -3, 4, 3, 38, 30, "thatch", true));
		Assert.Throws<ArgumentException>(() => ClaudeBuilder.Walls(-4, -3, 4, 3, 34, 4, "wood", new[] { new ClaudeBuilder.Door("up", 0) }));
		using var r = new Run();
		await r.T.OpenWorkshop();
		Assert.Contains("\"problems\":[]", await r.T.BuildFloor(-4, -3, 4, 3));
		Assert.Contains("\"problems\":[]", await r.T.BuildWalls(-4, -3, 4, 3, doors: new[] { new ClaudeTools.DoorSpec { Side = "south", At = 0 } }));
		Assert.Contains("\"problems\":[]", await r.T.BuildRoof(-4, -3, 4, 3));
		var support = J(await r.T.SupportCheck());
		Assert.Equal(58, support.GetProperty("pieces").GetInt32());
		Assert.Equal(0, support.GetProperty("wouldFall").GetInt32());
		Assert.Contains("build the walls first", await r.T.BuildRoof(20, 20, 26, 26));
	}

	// The ready tasks: one pending step each, as asked; a dry run changes nothing; what does not fit is
	// said. Selection both ways; a chest filled (and text only for signs); Claude's changes taken back,
	// the user's kept.
	[AvaloniaFact]
	public async Task ReadyTasksSelectionContentsAndTakingBack()
	{
		using var r = new Run();
		await r.T.OpenWorld(r.Dir);
		await r.T.OpenArea(0, 0, 1);
		var s = r.W.Session!;
		Assert.StartsWith("Dry run:", await r.T.RunScript("Ground.Raise(0, 0, 2);", dryRun: true));
		Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
		// The user's own change first.
		// (Shape takes the area's grid points, 0 to 64 across.)
		s.Shape(8, 56, Formula.Compile("1", new string[0]), 3, 0, "mine");
		await r.T.Flatten(-20, -20, 0, 0, height: 44);
		Assert.Equal(44, s.Scene.Heights[(int)(-10 - s.Scene.Cz + (s.Scene.H - 1) / 2f) * s.Scene.W + (int)(-10 - s.Scene.Cx + (s.Scene.W - 1) / 2f)], 1);
		Assert.Contains("painted", await r.T.Road(new[] { new[] { -28f, 20f }, new[] { 28f, 20f } }));
		Assert.Contains("The paint is", await r.T.PaintArea("lava", x: 0, z: 0, radius: 3));
		Assert.Contains("object(s) placed", await r.T.Forest(5, -30, 30, -5, new[] { "Birch1" }, spacing: 7));
		Assert.Contains("not an object", await r.T.Forest(5, -30, 30, -5, new[] { "NoSuchTree" }));
		await r.T.SelectObjects(new[] { 1, 2 }, focus: false);
		Assert.Equal(2, J(await r.T.GetSelection()).GetArrayLength());
		int chest = J(await r.T.PlacePieces(new[] { new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -10, Z = -10 } })).GetProperty("placed")[0].GetProperty("id").GetInt32();
		Assert.StartsWith("Changed", await r.T.SetContents(chest, new[] { new ClaudeTools.ItemSpec { Item = "Coins", Stack = 50 } }));
		// Stands: food lying flat, then emptied; an armour stand dressed and posed; what a stand refuses.
		int tray = J(await r.T.PlacePieces(new[] { new ClaudeTools.PieceSpec { Prefab = "itemstandh", X = -12, Z = -10 } })).GetProperty("placed")[0].GetProperty("id").GetInt32();
		string held = await r.T.SetContents(tray, new[] { new ClaudeTools.ItemSpec { Item = "CookedMeat" } });
		Assert.StartsWith("Changed", held);
		tray = int.Parse(held[(held.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);
		Assert.Equal("CookedMeat", TerrainEditor.App.StandData.ReadItemStand(TerrainEditor.Save.ZdoData.Parse(TerrainEditor.App.ObjectData.Bytes(s.Scene.World!, s.Edits, s.Scene.Things[tray].Id)!)).Item?.Item);
		Assert.Contains("cannot hang", await r.T.SetContents(tray, new[] { new ClaudeTools.ItemSpec { Item = "Wood" } }));
		Assert.Contains("holds one item", await r.T.SetContents(tray, new[] { new ClaudeTools.ItemSpec { Item = "CookedMeat" }, new ClaudeTools.ItemSpec { Item = "FishCooked" } }));
		Assert.StartsWith("Changed", await r.T.SetContents(tray, Array.Empty<ClaudeTools.ItemSpec>()));
		int dummy = J(await r.T.PlacePieces(new[] { new ClaudeTools.PieceSpec { Prefab = "ArmorStand", X = -14, Z = -10 } })).GetProperty("placed")[0].GetProperty("id").GetInt32();
		Assert.StartsWith("Changed", await r.T.SetContents(dummy, new[] { new ClaudeTools.ItemSpec { Item = "HelmetBronze" }, new ClaudeTools.ItemSpec { Item = "SwordIron", Quality = 2 } }, pose: 1));
		Assert.Contains("not an item stand", await r.T.SetContents(chest, orientation: 1));
		Assert.Contains("not a sign", await r.T.SetContents(1, text: "hello"));
		Assert.Contains("not a creature", await r.T.SetContents(1, stars: 2));
		// Then the user again; Take back removes only Claude's.
		s.Shape(56, 56, Formula.Compile("1", new string[0]), 3, 0, "mine too");
		int before = s.UndoList.Count;
		r.W.History.Toggle();
		Assert.True(r.W.History.TakeBackClaude.IsVisible);
		await r.W.History.TakeBackClaudes();
		Assert.All(s.UndoList.Where(c => c.Label.StartsWith("Claude:", StringComparison.Ordinal)), c => Assert.True(c.Removed));
		Assert.Contains(s.UndoList, c => c.Label == "mine" && !c.Removed);
		Assert.Contains(s.UndoList, c => c.Label == "mine too" && !c.Removed);
		Assert.False(r.W.History.TakeBackClaude.IsVisible);
		Assert.True(s.UndoList.Count >= before);
	}

	// --mcp-stdio: messages relayed with the token, the protocol remembered; refused clearly when the
	// switch is off or the editor does not answer.
	[Fact]
	public async Task TheRelayPassesMessagesWithTheToken()
	{
		var seen = new List<HttpRequestMessage>();
		var handler = new Answer(req =>
		{
			seen.Add(req);
			var body = req.Content!.ReadAsStringAsync().Result;
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
		var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
		Assert.Equal(2, lines.Length);
		Assert.Contains("\"protocolVersion\":\"2025-06-18\"", lines[0]);
		Assert.Equal("Bearer tok", seen[0].Headers.Authorization!.ToString());
		Assert.Equal("http://127.0.0.1:5799/mcp", seen[0].RequestUri!.ToString());
		Assert.False(seen[0].Headers.Contains("MCP-Protocol-Version"));
		Assert.Equal("2025-06-18", seen[1].Headers.GetValues("MCP-Protocol-Version").Single());
		var off = new StringWriter();
		await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/list\"}\n{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n"), off, () => new TerrainEditor.App.AppSettings(), handler);
		var refused = off.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
		Assert.Single(refused);
		Assert.Contains("Allow Claude to connect", refused[0]);
		Assert.Contains("\"id\":7", refused[0]);
		var down = new StringWriter();
		await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}\n"), down, () => on, new Answer(_ => throw new HttpRequestException("refused")));
		Assert.Contains("not running", down.ToString());
	}

	private sealed class Answer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
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
				"building_guide", "get_selection", "select_objects", "flatten", "paint_area", "road", "forest", "set_contents", "build_floor", "build_walls", "build_roof" })
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
