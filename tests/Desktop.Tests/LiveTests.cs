using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Live mode against a stand-in for the game (FakeGame): opening the running world, Apply live (ground,
// objects, zone resets), Reload, the players on the map, and the start page's ways in.
[Collection("World files")]
public class LiveTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	// Waits for the window's posted work and background tasks.
	internal static async Task Until(Func<bool> done, int ms = 15000)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		while (!done())
		{
			Assert.True(watch.ElapsedMilliseconds < ms, "timed out");
			Dispatcher.UIThread.RunJobs();
			await Task.Delay(20);
		}
	}

	private static string Texts(Control? c) => c == null ? "" : string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Prepend((c as TextBlock)?.Text).Where(t => !string.IsNullOrEmpty(t)));

	[Fact]
	public async Task ApplyLiveSendsTheGroundAndObjectsAndKeepsTheHistory()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), "test");
		Assert.True(world.IsLive);
		Assert.Equal(1, game.Snapshots);
		var scene = WorldScene.Load(world, 0, 0, 1);
		var s = scene.Session!;
		Assert.True(s.IsLive);
		s.Shape(32, 32, Two, 3, 0, "raise");
		int tree = scene.Things.FindIndex(t => !t.Gone && !t.Piece);
		Assert.True(tree >= 0);
		s.Delete(new[] { tree });
		Assert.Equal((1, 1, 0, 0), world.Pending);
		Assert.StartsWith("Not applied:", s.PendingText);

		var o = await s.ApplyLive();
		Assert.True(o.Done, o.Message);
		Assert.False(o.Reloaded);
		Assert.Equal(new[] { (0, 0) }, game.Terrain);
		Assert.Equal((1, 0), Assert.Single(game.ObjectCalls));
		Assert.Equal((0, 0, 0, 0), world.Pending);
		Assert.Equal("All applied", s.PendingText);
		// The history stays, every change tagged as applied.
		Assert.Equal(2, s.UndoList.Count);
		Assert.All(s.UndoList, c => Assert.True(c.Applied));

		// Undone after applying: the tree is waiting to come back; applying sends it.
		s.Undo();
		Assert.NotEqual((0, 0, 0, 0), world.Pending);
		o = await s.ApplyLive();
		Assert.True(o.Done, o.Message);
		Assert.Equal(2, game.ObjectCalls.Count);
		Assert.Equal((0, 0, 0, 0), world.Pending);
		// Applying with nothing waiting sends nothing.
		o = await s.ApplyLive();
		Assert.Equal("Nothing to apply.", o.Message);
		Assert.Equal(2, game.ObjectCalls.Count);
	}

	[Fact]
	public async Task ZoneResetsAreAppliedThenTheWorldIsReadAgain()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), "test");
		var scene = WorldScene.Load(world, 0, 0, 1);
		scene.Session!.Shape(32, 32, Two, 3, 0, "raise");
		world.Edits.SetReset(new ZoneReset(2, 3, true, false), true);
		var o = await world.ApplyLive();
		Assert.True(o.Done, o.Message);
		Assert.True(o.Reloaded);
		Assert.Equal((2, 3, true, false), Assert.Single(game.Resets));
		// The game regenerates the zones: the world is read again from it.
		Assert.Equal(2, game.Snapshots);
		Assert.Equal((0, 0, 0, 0), world.Pending);
		Assert.Null(world.History);
	}

	[Fact]
	public async Task ReloadDropsWhatIsNotApplied()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), "test");
		var scene = WorldScene.Load(world, 0, 0, 1);
		scene.Session!.Shape(32, 32, Two, 3, 0, "raise");
		Assert.Equal(1, world.Pending.Zones);
		await world.Reload();
		Assert.Equal(2, game.Snapshots);
		Assert.Equal((0, 0, 0, 0), world.Pending);
		Assert.Empty(game.Terrain);
	}

	// "I made my own tunnel": a wrong token is explained; the right one opens the live map with the
	// players, an area edited there is applied from the editor, and History tags it.
	[AvaloniaFact]
	public async Task OwnTunnelToTheMapAndApplyFromTheEditor()
	{
		using var game = new FakeGame();
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		w.ShowStart();
		var start = w.StartPage!;
		start.LiveUrl.Text = $"127.0.0.1:{game.Port}";
		start.LiveToken.Text = "wrong";
		await start.ConnectUrl();
		Assert.Contains("refused the token", start.UrlError.Text);
		Assert.Null(w.World);

		start.LiveToken.Text = game.Token;
		await start.ConnectUrl();
		await Until(() => w.World != null && w.MapPage != null);
		var map = w.MapPage!;
		Assert.True(w.World!.IsLive);
		Assert.Equal("Apply live", map.SaveButton.Content);
		Assert.Contains("live", map.Meta.Text);
		await Until(() => map.LiveText.Text?.Contains("2 player(s)") == true);
		Assert.Contains("Ada, Bjorn", map.LiveText.Text);
		Assert.Equal(2, map.Map.Players.Count);
		map.Stop();

		await w.EditArea(0, 0, 1);
		w.Session!.Shape(32, 32, Two, 3, 0, "raise");
		Assert.Equal("Apply live", w.SaveButton.Content);
		await w.Save();
		Assert.Equal(new[] { (0, 0) }, game.Terrain);
		Assert.Equal((0, 0, 0, 0), w.World.Pending);
		w.ShowRight(w.History.Card);
		Dispatcher.UIThread.RunJobs();
		Assert.Contains("applied", Texts(w.History.Rows));
	}

	// "My game": what the page says at each step of setting up the game, then Edit live.
	[AvaloniaFact]
	public async Task MyGameFindsTheRunningGame()
	{
		using var game = new FakeGame();
		string root = Path.Combine(Path.GetTempPath(), "vwe-game-" + Guid.NewGuid().ToString("N")[..10]);
		try
		{
			string bep = Path.Combine(root, "BepInEx");
			var settings = new AppSettings { BepInExFolders = { root } };
			var start = new StartPage(settings);
			Func<Task<WorldSession>>? opened = null;
			start.OpenRequested += (open, _) => opened = open;

			await start.PollGame();
			Assert.Contains("needs BepInEx and the WorldEditorBridge plugin", Texts(start.GameState));
			Directory.CreateDirectory(Path.Combine(bep, "plugins"));
			await start.PollGame();
			Assert.Contains("BepInEx is installed: only steps 2 and 3 are left.", Texts(start.GameState));
			File.WriteAllText(Path.Combine(bep, "plugins", "WorldEditorBridge.dll"), "");
			await start.PollGame();
			Assert.Contains("Start Valheim once with BepInEx", Texts(start.GameState));
			// The plugin's settings, but nothing answers: the game is not started.
			Directory.CreateDirectory(Path.Combine(bep, "config"));
			string cfg = Path.Combine(bep, "config", "Tie.WorldEditorBridge.cfg");
			File.WriteAllText(cfg, $"[Bridge]\nPort = {FakeGame.FreePort()}\nToken = {game.Token}\n");
			await start.PollGame();
			Assert.Contains("Start Valheim and load your world", Texts(start.GameState));
			// The game answers on the plugin's port.
			File.WriteAllText(cfg, $"[Bridge]\nPort = {game.Port}\nToken = {game.Token}\n");
			await start.PollGame();
			Assert.Contains("Valheim is running with the world CITest", Texts(start.GameState));
			Assert.Contains("2 player(s) connected", Texts(start.GameState));
			// An older plugin's settings file (local.worldeditorbridge.cfg) is found too.
			File.Move(cfg, Path.Combine(bep, "config", LocalGame.OldConfigName));
			Assert.Equal(game.Port, Assert.Single(LocalGame.FindBridges(settings, out _, out _)).Port);
			var edit = start.GameState.GetLogicalDescendants().OfType<Button>().Single();
			edit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
			Assert.NotNull(opened);
			var world = await opened!();
			Assert.True(world.IsLive);
			Assert.Equal("CITest", world.World.Name);
			Assert.Equal("game", settings.LastMode);
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	// A dedicated server: the form's checks before any connection, and the form folding away once a
	// server is saved. (Logging in over SSH needs a real server: not tested here.)
	[AvaloniaFact]
	public async Task ServerFormChecksAndFolds()
	{
		Assert.StartsWith(Path.GetTempPath(), ServerConfig.FilePath);
		File.Delete(ServerConfig.FilePath);
		try
		{
			var start = new StartPage(new AppSettings());
			start.FillServers();
			Assert.True(start.ServerForm.IsExpanded);
			await start.ConnectNew();
			Assert.Equal("Enter the server address.", start.ServerError.Text);
			start.SHost.Text = "my.server.example";
			await start.ConnectNew();
			Assert.StartsWith("Enter the plugin's token", start.ServerError.Text);

			ServerConfig.Remember(new ServerConfig.Server { Name = "Test server", Host = "203.0.113.10", User = "valheim", Token = "t" });
			start.FillServers();
			Assert.False(start.ServerForm.IsExpanded);
			Assert.Contains("Test server", Texts(start.SavedServers));
			var edit = start.SavedServers.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "edit");
			edit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
			Assert.True(start.ServerForm.IsExpanded);
			Assert.Equal("203.0.113.10", start.SHost.Text);
		}
		finally
		{
			File.Delete(ServerConfig.FilePath);
		}
	}

	// Settings: the Valheim folder must be the game's, folders must exist, and the choices are kept.
	[Fact]
	public void SettingsAreCheckedThenKept()
	{
		Assert.StartsWith(Path.GetTempPath(), AppSettings.PathOverride);
		string root = Path.Combine(Path.GetTempPath(), "vwe-settings-" + Guid.NewGuid().ToString("N")[..10]);
		try
		{
			string game = Path.Combine(root, "Valheim"), bep = Path.Combine(root, "profile"), worlds = Path.Combine(root, "worlds");
			Directory.CreateDirectory(game);
			Directory.CreateDirectory(bep);
			Directory.CreateDirectory(worlds);
			var settings = new AppSettings();
			Assert.Contains("valheim_Data", SettingsDialog.Apply(settings, game, Array.Empty<string>(), Array.Empty<string>()));
			Assert.Contains("BepInEx folder not found", SettingsDialog.Apply(settings, null, new[] { Path.Combine(root, "nope") }, Array.Empty<string>()));
			Assert.Contains("World folder not found", SettingsDialog.Apply(settings, null, Array.Empty<string>(), new[] { Path.Combine(root, "nope") }));
			Assert.Null(settings.ValheimPath);
			Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
			// Blank rows are skipped, the same folder twice counts once.
			Assert.Null(SettingsDialog.Apply(settings, $"\"{game}\"", new[] { bep, " ", bep }, new[] { worlds }));
			var kept = AppSettings.Load();
			Assert.Equal(game, kept.ValheimPath);
			Assert.Equal(new[] { bep }, kept.BepInExFolders);
			Assert.Equal(new[] { worlds }, kept.WorldFolders);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
