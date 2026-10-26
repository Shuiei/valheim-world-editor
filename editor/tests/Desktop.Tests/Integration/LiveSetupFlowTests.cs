using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Live mode's reports and setup: objects already gone in the game, an old plugin, a game that stops answering, the steps of
/// setting up the game and the dedicated server form, and the Settings checks.
/// </summary>
[Collection("World files")]
public class LiveSetupFlowTests
{
    // Objects someone already removed in the game are reported, not an error.
    [Fact]
    public async Task ObjectsAlreadyGoneInTheGameAreReported()
    {
        using var game = new FakeGame { Missing = 1 };
        var world = await OpenOn(game);
        var scene = WorldScene.Load(world, 0, 0, 1);
        scene.Session!.Delete([scene.Things.FindIndex(t => !t.Gone && !t.Piece)]);
        var o = await world.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Contains("1 were already gone", o.Message);
    }

    // A plugin from before live zone resets says so; the resets stay waiting.
    [Fact]
    public async Task AnOldPluginCannotResetZones()
    {
        using var game = new FakeGame();
        game.Fail["/zones/reset"] = 404;
        var world = await OpenOn(game);
        world.Edits.SetReset(new ZoneReset(2, 3, true, false), true);
        var o = await world.ApplyLive();
        Assert.False(o.Done);
        Assert.Contains("too old to reset zones live", o.Message);
        Assert.Equal(1, world.Pending.Resets);
    }

    // The game stops answering mid-session: opening the world again fails with a reason, Reload too.
    [Fact]
    public async Task AGameThatStopsAnsweringIsReported()
    {
        var game = new FakeGame();
        var live = new LiveBridge(game.Url, game.Token);
        var world = await WorldSession.OpenLive(live, FakeGame.Label());
        game.Dispose();
        await Assert.ThrowsAnyAsync<Exception>(world.Reload);
        Assert.Contains("Nothing answers", await LiveBridge.Check(game.Url, game.Token, TimeSpan.FromSeconds(2)));
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
        await TestWindows.Until(() => w.World != null && w.MapPage != null);
        var map = w.MapPage!;
        Assert.True(w.World!.IsLive);
        Assert.Equal("Apply live", map.SaveButton.Content);
        Assert.Contains("live", map.Meta.Text);
        await TestWindows.Until(() => map.LiveText.Text?.Contains("2 player(s)") == true);
        Assert.Contains("Ada, Bjorn", map.LiveText.Text);
        Assert.Equal(2, map.Map.Players.Count);
        map.Stop();

        await w.EditArea(0, 0, 1);
        Raise(w.Session!);
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
            Press(edit);
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
            Press(edit);
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
            Assert.Contains("valheim_Data", SettingsDialog.Apply(settings, game, [], []));
            Assert.Contains("BepInEx folder not found", SettingsDialog.Apply(settings, null, [Path.Combine(root, "nope")], []));
            Assert.Contains("World folder not found", SettingsDialog.Apply(settings, null, [], [Path.Combine(root, "nope")]));
            Assert.Null(settings.ValheimPath);
            Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
            // Blank rows are skipped, the same folder twice counts once.
            Assert.Null(SettingsDialog.Apply(settings, $"\"{game}\"", [bep, " ", bep], [worlds]));
            SettingsChecks.AssertKept(game, bep, worlds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
