using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page's server connections: the server form connects, remembers the server and opens it, each way it can
/// fail is said, a click while connecting starts no second connection, saved servers connect and are forgotten, and an
/// edited server's form shows its own secrets only.
/// </summary>
[Collection("DataDir")]
public class StartPageServerTests
{
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
    public async Task ASavedServerWithItsPasswordIsATileThatConnects()
    {
        using var game = new FakeGame();
        using var r = new Run();
        ServerConfig.Remember(new ServerConfig.Server { Name = "Valhalla", Host = "203.0.113.10", SshPort = 22, User = "valheim", Password = "pw", Token = game.Token });
        r.Tunnel(game.Port, game.Token);
        await r.P.FillHome();
        var tile = r.P.RecentTiles.Children.OfType<Button>().Single(b => Texts(b).Contains("Valhalla"));
        Assert.Contains("Server · connects in one click", Texts(tile));
        Click(tile);
        await TestWindows.Until(() => r.Tunnels.Count == 1);
        Assert.Equal(("203.0.113.10", "valheim", "pw", game.Token), (r.Tunnels[0].Host, r.Tunnels[0].User, r.Tunnels[0].Password, r.Tunnels[0].Token));
    }
}
