using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The start page: connecting to a dedicated server through the form and from a saved server (the SSH
// tunnel replaced by one that leads to a stand-in game), each way it can fail, forgetting a server,
// the saved worlds as cards, a world folder typed with ~, and "My game" when the plugin's settings
// cannot be read. Saved servers and settings go to the tests' own files; it runs alone.
[Collection("DataDir")]
public class PanelStartTests
{
	private sealed class Run : IDisposable
	{
		public StartPage P { get; }
		public AppSettings Settings { get; } = new();
		public List<Tunnel.Request> Tunnels { get; } = new();
		public List<string> Opened { get; } = new();
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

		// The tunnel opens on this port with this token (a stand-in game's), or fails.
		public void Tunnel(int port, string token, string? error = null) => P.StartTunnel = r =>
		{
			Tunnels.Add(r);
			return Task.FromResult(new Tunnel.Result(port, token, error, null,
				error == null ? new ServerConfig.Server { Name = r.Name ?? r.Host, Host = r.Host, SshPort = r.Port, User = r.User, Token = r.Token } : null));
		};

		public void Dispose()
		{
			ServerConfig.PathOverride = _servers;
			try
			{
				Directory.Delete(Temp, true);
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

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
		await LiveTests.Until(() => r.Opened.Count == 1);
		var first = r.Tunnels.Single();
		Assert.Equal(("Valhalla", "/keys/id", game.Token, "/srv/valheim", "my phrase"), (first.Name, first.KeyPath, first.Token, first.GameFolder, first.Passphrase));
		boxes[0].Text = game.Token;
		boxes[1].Text = "secret";
		Click(connects[0]);
		await LiveTests.Until(() => r.Opened.Count == 2);
		Assert.Equal(("Bare", game.Token, "secret"), (r.Tunnels[1].Name, r.Tunnels[1].Token, r.Tunnels[1].Password));
		// Forget asks first.
		var forgets = r.P.SavedServers.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == "forget").ToList();
		r.P.Confirm = _ => Task.FromResult(false);
		Click(forgets[0]);
		Assert.Equal(2, ServerConfig.Load().Count);
		r.P.Confirm = _ => Task.FromResult(true);
		Click(forgets[0]);
		await LiveTests.Until(() => ServerConfig.Load().Count == 1);
	}

	[AvaloniaFact]
	public void SavedWorldsAreCardsAndNoneIsSaid()
	{
		using var r = new Run();
		r.P.FindWorlds = _ => new();
		r.P.FillWorlds();
		Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
		string dir = EditTests.CopyFixture();
		try
		{
			r.P.FindWorlds = _ => new()
			{
				new Worlds.Info("CITest", dir, DateTime.Now, 2, "a test folder", true, null),
				new Worlds.Info("Broken", "/nowhere", DateTime.Now, 0, "elsewhere", false, "No save files in it."),
			};
			r.P.FillWorlds();
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
		var shown = r.P.GameState.Content;
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
}
