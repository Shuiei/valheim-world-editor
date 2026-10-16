using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page: connecting to a dedicated server through the form and from a saved server (the SSH tunnel replaced
/// by one that leads to a stand-in game), each way it can fail, editing and forgetting a server, the saved worlds as cards, a world
/// folder typed with ~, and "My game" when the plugin's settings cannot be read. Saved servers and settings go to the
/// tests' own files; it runs alone.
/// </summary>
[Collection("DataDir")]
public class StartPageTests
{
    /// <summary>
    /// A start page with its own settings, saved servers in a temporary file, and the worlds it was asked to open; the
    /// saved-servers path is put back when disposed.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public StartPage P { get; }
        public AppSettings Settings { get; } = new();
        public List<Tunnel.Request> Tunnels { get; } = [];
        public List<string> Opened { get; } = [];
        public Func<Task<WorldSession>>? Open;
        private readonly string? _servers = ServerConfig.PathOverride;
        public string Temp { get; } = Path.Combine(Path.GetTempPath(), "vwe-start-" + Guid.NewGuid().ToString("N")[..8]);

        public Run()
        {
            Directory.CreateDirectory(Temp);
            ServerConfig.PathOverride = Path.Combine(Temp, "servers.cfg");
            P = new StartPage(Settings);
            P.OpenRequested += (open, what) => { Open = open; Opened.Add(what); };
        }

        /// <summary>
        /// The tunnel opens on this port with this token (a stand-in game's), or fails.
        /// </summary>
        public void Tunnel(int port, string token, string? error = null) => P.StartTunnel = r =>
        {
            Tunnels.Add(r);
            return Task.FromResult(new Tunnel.Result(port, token, error, null,
                error == null ? new ServerConfig.Server { Name = r.Name ?? r.Host, Host = r.Host, SshPort = r.Port, User = r.User, Token = r.Token } : null));
        };

        /// <inheritdoc/>
        public void Dispose()
        {
            ServerConfig.PathOverride = _servers;
            try
            {
                Directory.Delete(Temp, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Every text block's text under the control, joined with " | ", for asserting on what it shows.
    /// </summary>
    private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact]
    public async Task TheServerFormConnectsRemembersTheServerAndOpensIt()
    {
        using var game = new FakeGame();
        using var r = new Run();
        r.Tunnel(game.Port, game.Token);
        r.P.SetMode("server");
        Assert.Equal("server", r.P.Mode);
        r.P.SName.Text = " Friends ";
        r.P.SHost.Text = " my.server.example ";
        r.P.SPort.Value = 2222;
        r.P.SUser.Text = "valheim";
        r.P.SPass.Text = "   ";
        r.P.SToken.Text = game.Token;
        r.P.SBridge.Value = 5190;
        r.P.SSave.IsChecked = true;
        r.P.SFolder.Text = "/home/valheim/server";
        await r.P.ConnectNew();
        var t = Assert.Single(r.Tunnels);
        Assert.Equal(("my.server.example", 2222, "valheim", null, 5190, true, "Friends", "/home/valheim/server"), (t.Host, t.Port, t.User, t.Password, t.BridgePort, t.SavePassword, t.Name, t.GameFolder));
        Assert.Null(t.KeyPath);
        Assert.Equal("", r.P.ServerError.Text);
        Assert.Equal(new[] { "Loading the world from the server…" }, r.Opened);
        Assert.Equal("Friends", Assert.Single(ServerConfig.Load()).Name);
        Assert.Equal("server", r.Settings.LastMode);
        var world = await r.Open!();
        Assert.True(world.IsLive);
    }

    [AvaloniaFact]
    public async Task EachWayConnectingCanFailIsSaid()
    {
        using var game = new FakeGame();
        using var r = new Run();
        r.P.SHost.Text = "my.server.example";
        r.P.SToken.Text = "t";
        // The tunnel itself fails.
        r.Tunnel(0, "", "Could not log in: wrong password.");
        await r.P.ConnectNew();
        Assert.Equal("Could not log in: wrong password.", r.P.ServerError.Text);
        // The plugin refuses the token.
        r.Tunnel(game.Port, "wrong token");
        await r.P.ConnectNew();
        Assert.Contains("refused the token", r.P.ServerError.Text);
        // Nothing answers through the tunnel.
        r.Tunnel(FakeGame.FreePort(), game.Token);
        await r.P.ConnectNew();
        Assert.StartsWith("The tunnel is open, but the plugin does not answer on the server", r.P.ServerError.Text);
        Assert.Empty(r.Opened);
        Assert.Empty(ServerConfig.Load());
    }

    [AvaloniaFact]
    public async Task AClickWhileConnectingStartsNoSecondConnection()
    {
        using var game = new FakeGame();
        using var r = new Run();
        var open = new TaskCompletionSource<Tunnel.Result>();
        r.P.StartTunnel = t =>
        {
            r.Tunnels.Add(t);
            return open.Task;
        };
        r.P.SHost.Text = "my.server.example";
        r.P.SToken.Text = game.Token;
        var first = r.P.ConnectNew();
        // Clicked again while the tunnel is still being made: nothing more happens.
        await r.P.ConnectNew();
        open.SetResult(new Tunnel.Result(game.Port, game.Token, null, null));
        await first;
        Assert.Single(r.Tunnels);
        Assert.Equal(["Loading the world from the server…"], r.Opened);
        // Done: connecting works again.
        await r.P.ConnectNew();
        Assert.Equal(2, r.Tunnels.Count);
    }

    [AvaloniaFact]
    public async Task ASavedServerConnectsWithWhatWasSavedAndCanBeForgotten()
    {
        using var game = new FakeGame();
        using var r = new Run();
        ServerConfig.Remember(new ServerConfig.Server { Name = "Valhalla", Host = "203.0.113.10", SshPort = 22, User = "valheim", KeyFile = "/keys/id", Token = game.Token, GameFolder = "/srv/valheim" });
        ServerConfig.Remember(new ServerConfig.Server { Name = "Bare", Host = "203.0.113.11", User = "admin" });
        r.Tunnel(game.Port, game.Token);
        r.P.FillServers();
        Assert.Contains("key file", Texts(r.P.SavedServers));
        Assert.Contains("password asked", Texts(r.P.SavedServers));
        // The server without a token or password: its boxes are typed into; the one with a key file asks
        // for its passphrase (never saved: such a key could not connect again).
        var boxes = r.P.SavedServers.GetLogicalDescendants().OfType<TextBox>().ToList();
        Assert.Equal(3, boxes.Count);
        boxes[2].Text = "my phrase";
        // Newest first: the bare server, then Valhalla.
        var connects = r.P.SavedServers.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == "Connect").ToList();
        Click(connects[1]);
        await TestWindows.Until(() => r.Opened.Count == 1);
        var first = r.Tunnels.Single();
        Assert.Equal(("Valhalla", "/keys/id", game.Token, "/srv/valheim", "my phrase"), (first.Name, first.KeyPath, first.Token, first.GameFolder, first.Passphrase));
        boxes[0].Text = game.Token;
        boxes[1].Text = "secret";
        Click(connects[0]);
        await TestWindows.Until(() => r.Opened.Count == 2);
        Assert.Equal(("Bare", game.Token, "secret"), (r.Tunnels[1].Name, r.Tunnels[1].Token, r.Tunnels[1].Password));
        // Forget asks first.
        var forgets = r.P.SavedServers.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == "forget").ToList();
        r.P.Confirm = _ => Task.FromResult(false);
        Click(forgets[0]);
        Assert.Equal(2, ServerConfig.Load().Count);
        r.P.Confirm = _ => Task.FromResult(true);
        Click(forgets[0]);
        await TestWindows.Until(() => ServerConfig.Load().Count == 1);
    }

    [AvaloniaFact]
    public async Task EditingASavedServerFillsTheFormWithItsOwnSecretsOnly()
    {
        using var game = new FakeGame();
        using var r = new Run();
        ServerConfig.Remember(new ServerConfig.Server { Name = "Valhalla", Host = "203.0.113.10", User = "valheim", Password = " pw ", Token = game.Token });
        ServerConfig.Remember(new ServerConfig.Server { Name = "Bare", Host = "203.0.113.11", User = "admin" });
        r.Tunnel(game.Port, game.Token);
        // Another server's secrets typed in the form before.
        (r.P.SPass.Text, r.P.SToken.Text, r.P.SPhrase.Text, r.P.SBridge.Value, r.P.SSave.IsChecked) = ("other", "other token", "other phrase", 5190, false);
        r.P.FillServers();
        // Newest first: the bare server, then Valhalla.
        var edits = r.P.SavedServers.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == "edit").ToList();
        Click(edits[1]);
        await r.P.ConnectNew();
        var t = Assert.Single(r.Tunnels);
        // The saved password is sent as it is saved, spaces at its ends included.
        Assert.Equal(("203.0.113.10", " pw ", game.Token, null, null, true), (t.Host, t.Password, t.Token, t.Passphrase, t.BridgePort, t.SavePassword));
        // A server with nothing saved leaves them empty to type, the password not to be saved.
        (r.P.SPass.Text, r.P.SToken.Text) = ("other", "other token");
        Click(edits[0]);
        Assert.True(string.IsNullOrEmpty(r.P.SPass.Text) && string.IsNullOrEmpty(r.P.SToken.Text));
        Assert.False(r.P.SSave.IsChecked);
    }

    [AvaloniaFact]
    public async Task SavedWorldsAreCardsAndNoneIsSaid()
    {
        using var r = new Run();
        r.P.FindWorlds = _ => [];
        r.P.FillWorlds();
        await r.P.WorldsCheck!;
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
        string dir = Fixtures.CopyFixture();
        try
        {
            r.P.FindWorlds = _ =>
            [
                new Worlds.Info("CITest", dir, DateTime.Now, 2, "a test folder", true, null),
                new Worlds.Info("Broken", "/nowhere", DateTime.Now, 0, "elsewhere", false, "No save files in it."),
            ];
            r.P.FillWorlds();
            await r.P.WorldsCheck!;
            var cards = r.P.WorldCards.Children.OfType<Button>().ToList();
            Assert.Equal(2, cards.Count);
            Assert.False(cards[1].IsEnabled);
            Assert.Contains("No save files in it.", Texts(r.P.WorldCards));
            Click(cards[0]);
            Assert.Equal(new[] { "Opening the world…" }, r.Opened);
            Assert.Equal("offline", r.Settings.LastMode);
            Assert.Equal(Path.GetFullPath(dir), r.Settings.Recent[0].Path);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }

    [AvaloniaFact]
    public async Task RefreshFindsTheWorldsAgain()
    {
        using var r = new Run();
        List<Worlds.Info> found = [];
        r.P.FindWorlds = _ => found;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
        // A world folder added while the page is open shows after Refresh, and goes once removed.
        found = [new Worlds.Info("Added", "/somewhere", DateTime.Now, 1, "a test folder", true, null)];
        Click(r.P.RefreshWorlds);
        await r.P.WorldsCheck!;
        Assert.Single(r.P.WorldCards.Children.OfType<Button>());
        Assert.Contains("Added", Texts(r.P.WorldCards));
        found = [];
        Click(r.P.RefreshWorlds);
        await r.P.WorldsCheck!;
        Assert.Empty(r.P.WorldCards.Children.OfType<Button>());
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
    }

    [AvaloniaFact]
    public async Task ALookOvertakenByALaterOneShowsNothing()
    {
        using var r = new Run();
        var older = new Worlds.Info("Older", "/older", DateTime.Now, 1, "a test folder", true, null);
        var newer = new Worlds.Info("Newer", "/newer", DateTime.Now, 1, "a test folder", true, null);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool slow = true;
        r.P.FindWorlds = _ =>
        {
            if (!slow)
            {
                return [newer];
            }
            started.Set();
            release.Wait();
            return [older];
        };
        // A slow look starts, then the page lists the worlds again before it ends.
        var check = r.P.CheckWorlds();
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        slow = false;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Contains("Newer", Texts(r.P.WorldCards));
        // The slow look ends: its older worlds do not replace the newer list.
        release.Set();
        await check;
        Assert.Contains("Newer", Texts(r.P.WorldCards));
        Assert.DoesNotContain("Older", Texts(r.P.WorldCards));
    }

    [AvaloniaFact]
    public async Task AFolderThatCannotBeReadLeavesThePageWhole()
    {
        using var r = new Run();
        r.P.FindWorlds = _ => throw new IOException("a drive that went away");
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Equal("offline", r.P.Mode);
        Assert.True(r.P.OfflinePanel.IsVisible);
    }

    [AvaloniaFact]
    public async Task AWorldAddedOrRemovedShowsByItself()
    {
        using var r = new Run();
        List<Worlds.Info> found = [new Worlds.Info("First", "/first", DateTime.Now, 1, "a test folder", true, null)];
        r.P.FindWorlds = _ => found;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        var first = Assert.Single(r.P.WorldCards.Children.OfType<Button>());
        // Nothing changed: the cards stay as they are (no redraw under the mouse).
        await r.P.CheckWorlds();
        Assert.Same(first, Assert.Single(r.P.WorldCards.Children.OfType<Button>()));
        // A world folder added, then one removed, shows without Refresh.
        found = [.. found, new Worlds.Info("Second", "/second", DateTime.Now, 1, "a test folder", true, null)];
        await r.P.CheckWorlds();
        Assert.Equal(2, r.P.WorldCards.Children.OfType<Button>().Count());
        Assert.Contains("Second", Texts(r.P.WorldCards));
        found = [found[1]];
        await r.P.CheckWorlds();
        Assert.DoesNotContain("First", Texts(r.P.WorldCards));
        // The same world under another name, or found elsewhere (a recent world now): its card says so.
        found = [found[0] with { Name = "Renamed", Where = "recent" }];
        await r.P.CheckWorlds();
        Assert.Contains("Renamed", Texts(r.P.WorldCards));
        Assert.Contains("recent", Texts(r.P.WorldCards));
        // Another page shows: the worlds are not drawn behind it.
        r.P.SetMode("home");
        found = [];
        await r.P.CheckWorlds();
        Assert.Contains("Renamed", Texts(r.P.WorldCards));
    }

    [AvaloniaFact]
    public void AFolderTypedWithAHomeTildeIsUnderstood()
    {
        using var r = new Run();
        r.P.OpenFolder("\"~/no-world-for-vwe-tests-here\"");
        Assert.NotEqual("", r.P.PathError.Text);
        Assert.Empty(r.Opened);
    }

    [AvaloniaFact]
    public async Task MyGameWithUnreadablePluginSettingsKeepsWhatItShows()
    {
        // Root (CI's containers) reads a file whatever its permissions: nothing to check there.
        if (!OperatingSystem.IsLinux() || Environment.UserName == "root")
        {
            return;
        }
        using var r = new Run();
        string bep = Path.Combine(r.Temp, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        string cfg = Path.Combine(bep, "config", LocalGame.ConfigName);
        File.WriteAllText(cfg, $"Port = {FakeGame.FreePort()}\nToken = t\n");
        r.Settings.BepInExFolders.Add(r.Temp);
        await r.P.PollGame();
        object? shown = r.P.GameState.Content;
        Assert.Contains("Start Valheim and load your world", Texts((Control)shown!));
        // The game is now running, but its settings cannot be read: the page stays as it was.
        using var game = new FakeGame();
        File.WriteAllText(cfg, $"Port = {game.Port}\nToken = {game.Token}\n");
        File.SetUnixFileMode(cfg, UnixFileMode.None);
        try
        {
            await r.P.PollGame();
            Assert.Same(shown, r.P.GameState.Content);
        }
        finally
        {
            File.SetUnixFileMode(cfg, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [AvaloniaFact]
    public void HomesteadInstalledIsSaidWithWhereItIs()
    {
        using var r = new Run();
        r.P.ShowHomestead(new Homestead.Status(true, "1.4.0", ["Steam", "Steam", "r2modman"], "/somewhere"));
        Assert.Equal("Homestead 1.4.0 is installed (Steam, r2modman): the editor's blueprints show in its hammer tab, to build in game.", r.P.HomesteadText.Text);
    }

    [AvaloniaFact]
    public async Task ASavedServerWithItsPasswordIsATileThatConnects()
    {
        using var game = new FakeGame();
        using var r = new Run();
        ServerConfig.Remember(new ServerConfig.Server { Name = "Valhalla", Host = "203.0.113.10", SshPort = 22, User = "valheim", Password = "pw", Token = game.Token });
        r.Tunnel(game.Port, game.Token);
        r.P.FillHome();
        var tile = r.P.RecentTiles.Children.OfType<Button>().Single(b => Texts(b).Contains("Valhalla"));
        Assert.Contains("Server · connects in one click", Texts(tile));
        Click(tile);
        await TestWindows.Until(() => r.Tunnels.Count == 1);
        Assert.Equal(("203.0.113.10", "valheim", "pw", game.Token), (r.Tunnels[0].Host, r.Tunnels[0].User, r.Tunnels[0].Password, r.Tunnels[0].Token));
    }

    [AvaloniaFact]
    public async Task AGameThatCannotBeCheckedSaysWhyUntilItCanBe()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var r = new Run();
        string config = Path.Combine(r.Temp, "BepInEx", "config");
        Directory.CreateDirectory(config);
        string cfg = Path.Combine(config, LocalGame.ConfigName);
        // A link to nothing: there as a file, but it cannot be read (even by root).
        File.CreateSymbolicLink(cfg, Path.Combine(r.Temp, "missing.cfg"));
        r.Settings.BepInExFolders.Add(r.Temp);
        await r.P.PollGame();
        Assert.Contains("Your game could not be checked", Texts(r.P.GamePanel));
        // Checked again the same way: said once.
        await r.P.PollGame();
        Assert.Contains("Your game could not be checked", Texts(r.P.GamePanel));
        // Readable again: the failure goes.
        File.Delete(cfg);
        File.WriteAllText(cfg, $"Port = {FakeGame.FreePort()}\nToken = t\n");
        await r.P.PollGame();
        Assert.DoesNotContain("Your game could not be checked", Texts(r.P.GamePanel));
        Assert.Contains("Start Valheim and load your world", Texts((Control)r.P.GameState.Content!));
    }
}
