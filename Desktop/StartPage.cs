using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// The start page, like the web editor's (start.html): how to edit (your game live, a dedicated server
// live, or a saved world offline), the worlds found on this computer, saved servers, and the settings.
// Choosing one opens the world (OpenRequested), then the map.
public sealed class StartPage
{
	public Control View { get; }
	private readonly AppSettings _settings;
	private string _mode = "";
	public event Action<Func<Task<WorldSession>>, string>? OpenRequested;
	// Asks the window for a folder or a file (Browse…).
	internal Func<string, Task<string?>> PickFolder { get; set; } = _ => Task.FromResult<string?>(null);
	internal Func<string, Task<string?>> PickFile { get; set; } = _ => Task.FromResult<string?>(null);
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	// Tests: the SSH tunnel and the worlds found on this computer (the real ones otherwise).
	internal Func<Tunnel.Request, Task<Tunnel.Result>> StartTunnel { get; set; } = Tunnel.Start;
	internal Func<AppSettings, List<Worlds.Info>> FindWorlds { get; set; } = Worlds.Find;
	public event Action? SettingsRequested;
	public const string DocsUrl = "https://github.com/Shuiei/valheim-world-editor#readme";
	// Documentation: the project's page, in the web browser.
	internal Button DocsLink { get; } = new Button { Content = Icons.With("help", "Documentation"), FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");
	internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

	internal GameLookBanner LookBanner { get; }
	internal Dictionary<string, Button> ModeButtons { get; } = new();
	internal StackPanel GamePanel { get; } = new() { Spacing = 8 };
	internal StackPanel ServerPanel { get; } = new() { Spacing = 8 };
	internal Expander ServerForm { get; } = new() { Header = new TextBlock { Text = "Connect to a server", FontWeight = FontWeight.SemiBold }, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal StackPanel OfflinePanel { get; } = new() { Spacing = 8 };
	internal WrapPanel WorldCards { get; } = new() { ItemSpacing = 10, LineSpacing = 10 };
	internal TextBox PathBox { get; } = new() { PlaceholderText = "Folder of the world (with _main.<n>.chunks files)", FontSize = 13 };
	internal TextBlock PathError { get; } = Err();
	private readonly TextBlock _lastError = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, IsVisible = false };
	private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
	private readonly ContentControl _gameState = new();
	// Homestead (the building library's in-game side): found in the game's BepInEx or not.
	internal TextBlock HomesteadText { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
	internal Button GetHomesteadButton { get; } = new() { Content = "Get Homestead", FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false };
	private readonly TextBlock _gameError = Err();

	private static readonly IBrush Panel = Ui.Panel, Line = Ui.Line, Accent = Ui.Accent, Muted = Ui.Muted;

	internal static string Tilde(string path) => Ui.Tilde(path);

	// An error line: takes no room while it says nothing.
	private static TextBlock Err()
	{
		var t = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = false };
		t.PropertyChanged += (_, e) =>
		{
			if (e.Property == TextBlock.TextProperty)
			{
				t.IsVisible = !string.IsNullOrEmpty(t.Text);
			}
		};
		return t;
	}
	private static TextBlock Hint(string t) => new() { Text = t, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
	private static Border Card(Control c) => Ui.Card(c);
	private static TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = Muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 18, 0, 6) };

	public StartPage(AppSettings settings, string? lastError = null)
	{
		_settings = settings;
		LookBanner = new GameLookBanner(settings) { PickFolder = t => PickFolder(t) };
		var settingsButton = new Button { Content = Icons.With("settings", "Settings"), FontSize = 13 }.Classed("ghost");
		settingsButton.Tip("start.settings");
		DocsLink.Tip("start.docs");
		settingsButton.Click += (_, _) => SettingsRequested?.Invoke();
		DocsLink.Click += async (_, _) => await OpenUrl(new Uri(DocsUrl));
		var modes = new UniformGrid { Columns = 3 };
		foreach (var (key, title, tag, text, icon) in new[]
		{
			("game", "My game", "live", "Edit the world you are playing in, single player or the one you host. You see the changes in game right away.", "game"),
			("server", "A dedicated server", "live", "Edit your server's world while people play. The editor connects to the server itself.", "server"),
			("offline", "A saved world", "offline", "Edit world files on this computer with the game closed, then start the game again.", "folder"),
		})
		{
			var b = new Button
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				Margin = new Thickness(0, 0, 10, 0),
				Padding = Ui.Pad,
				Content = new StackPanel
				{
					Spacing = 4,
					Children =
					{
						new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Icons.Make(icon, 20), new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = tag, FontSize = 11, Foreground = tag == "live" ? new SolidColorBrush(Color.FromRgb(123, 196, 127)) : Muted, VerticalAlignment = VerticalAlignment.Center } } },
						new TextBlock { Text = text, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap },
					},
				},
			};
			b.Tip($"start.{key}");
			b.Click += (_, _) => SetMode(key);
			ModeButtons[key] = b;
			modes.Children.Add(b);
		}
		if (lastError != null)
		{
			_lastError.Text = lastError;
			_lastError.IsVisible = true;
		}

		// My game.
		GamePanel.Children.Add(Card(_gameState));
		GamePanel.Children.Add(_gameError);
		GetHomesteadButton.Tip("blueprints.getHomestead");
		GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));
		GamePanel.Children.Add(new StackPanel { Spacing = 4, Margin = new Thickness(2, 6, 0, 0), Children = { HomesteadText, GetHomesteadButton } });
		_gameTimer.Tick += async (_, _) => await PollGame();

		// A dedicated server.
		BuildServerPanel();

		// A saved world.
		var browse = new Button { Content = "Browse…" }.Tip("start.browse");
		browse.Click += async (_, _) => { if (await PickFolder("Choose a world folder (with _main.<n>.chunks)") is string p) PathBox.Text = p; };
		var openPath = new Button { Content = "Open" }.Classed("primary").Tip("start.open");
		PathBox.Tip("start.folder");
		openPath.Click += (_, _) => OpenFolder(PathBox.Text ?? "");
		OfflinePanel.Children.Add(WorldCards);
		OfflinePanel.Children.Add(Hint("Close Valheim (or stop the server) before you save changes into a world: a running game writes over the files. Saving always makes a full backup of the world first."));
		OfflinePanel.Children.Add(Card(new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = "Another world folder", FontWeight = FontWeight.SemiBold },
				new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6, Children = { PathBox, Col(browse, 1), Col(openPath, 2) } },
				PathError,
				Hint("For example a copy of a dedicated server's world: <savedir>/worlds_local/<World>."),
			},
		}));

		View = new ScrollViewer
		{
			Background = Ui.Bg,
			Content = new StackPanel
			{
				MaxWidth = 980,
				Margin = new Thickness(20, 30, 20, 50),
				Spacing = 4,
				Children =
				{
					new Grid
					{
						ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
						Children =
						{
							Ui.BrandTitle(26),
							Col(new TextBlock { Text = $"  v{BuildInfo.Version}", Foreground = Muted, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 6) }, 1),
							Col(settingsButton, 3),
						},
					},
					new TextBlock { Text = "Shape the ground, paint it, build paths, place and move trees, rocks, crops and building pieces, copy and paste whole areas.", Foreground = Muted, TextWrapping = TextWrapping.Wrap },
					_lastError,
					LookBanner.View,
					H2("How do you want to edit?"),
					modes,
					new Border { Height = 14 },
					GamePanel,
					ServerPanel,
					OfflinePanel,
					new StackPanel
					{
						Orientation = Orientation.Horizontal,
						Spacing = 10,
						Margin = new Thickness(0, 24, 0, 0),
						Children = { DocsLink, new TextBlock { Text = $"Settings, saved servers and log: {Tilde(AppSettings.DataDir)}", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } },
					},
				},
			},
		};
		SetMode(settings.LastMode is "game" or "server" or "offline" ? settings.LastMode : "game");
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	public string Mode => _mode;

	public void SetMode(string mode)
	{
		_mode = mode;
		foreach (var (k, b) in ModeButtons)
		{
			b.Classes.Set("on", k == mode);
			b.BorderThickness = new Thickness(k == mode ? 2 : 1);
		}
		GamePanel.IsVisible = mode == "game";
		ServerPanel.IsVisible = mode == "server";
		OfflinePanel.IsVisible = mode == "offline";
		_gameTimer.Stop();
		if (mode == "game")
		{
			_ = PollGame();
			_gameTimer.Start();
		}
		if (mode == "server")
		{
			FillServers();
		}
		if (mode == "offline")
		{
			FillWorlds();
		}
	}

	public void Stop() => _gameTimer.Stop();

	// ---- My game: is Valheim running with the plugin?
	private string _gameKey = "";

	internal async Task PollGame()
	{
		List<LocalGame.Bridge> bridges;
		bool bepInEx, plugin;
		LocalGame.Running? running;
		try
		{
			(bridges, bepInEx, plugin) = await Task.Run(() =>
			{
				var found = LocalGame.FindBridges(_settings, out bool b, out bool p);
				return (found, b, p);
			});
			running = await LocalGame.FindRunning(bridges);
			ShowHomestead(await Task.Run(() => Homestead.Find(_settings)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
		{
			return;
		}
		string where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where ?? "";
		string state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
		string key = $"{state}|{running?.World}|{running?.Players}|{where}";
		if (key == _gameKey)
		{
			return;
		}
		_gameKey = key;
		Control content;
		if (running != null)
		{
			var edit = new Button { Content = "Edit live", VerticalAlignment = VerticalAlignment.Center }.Classed("primary").Tip("start.editLive");
			var bridge = running.Bridge;
			edit.Click += (_, _) => OpenLive(new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token), "my game", "game", "Connecting to your game…");
			content = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,Auto"),
				Children =
				{
					new StackPanel { Children = { new TextBlock { Text = $"Valheim is running with the world {running.World}", FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint($"{running.Players} player(s) connected{(where != "" ? $" ({where})" : "")}") } },
					Col(edit, 1),
				},
			};
		}
		else if (state == "installed")
		{
			content = new StackPanel { Children = { new TextBlock { Text = "Start Valheim and load your world", FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint($"Single player, or the world you host. This page notices it by itself.{(where != "" ? $" ({where})" : "")}") } };
		}
		else if (state == "plugin-not-started")
		{
			content = new StackPanel { Children = { new TextBlock { Text = "Start Valheim once with BepInEx", FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint("The plugin is installed; it makes its settings on the first start. Then load your world.") } };
		}
		else
		{
			content = new StackPanel
			{
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Your game needs BepInEx and the WorldEditorBridge plugin (once, about 5 minutes)", FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
					Hint(state == "no-plugin" ? "BepInEx is installed: only steps 2 and 3 are left." : ""),
					new TextBlock { Text = "1. Install BepInEx for Valheim: BepInExPack for Valheim (with a mod manager such as r2modman, or by hand into the Valheim folder: see its page).", TextWrapping = TextWrapping.Wrap },
					new TextBlock { Text = "2. Add the plugin: with a mod manager, install WorldEditorBridge; by hand, copy WorldEditorBridge.dll (in WorldEditorBridge-<version>.zip, on the editor's releases page) into BepInEx/plugins of your game.", TextWrapping = TextWrapping.Wrap },
					new TextBlock { Text = "3. Start Valheim (with BepInEx) and load your world. This page notices it by itself.", TextWrapping = TextWrapping.Wrap },
				},
			};
		}
		_gameState.Content = content;
		_gameTimer.Interval = TimeSpan.FromSeconds(running != null ? 5 : 2.5);
	}

	// ---- Opening.
	private void OpenLive(LiveBridge live, string label, string mode, string what)
	{
		_settings.LastMode = mode;
		_settings.Save();
		OpenRequested?.Invoke(() => WorldSession.OpenLive(live, label), what);
	}

	internal void OpenFolder(string path)
	{
		path = path.Trim().Trim('"');
		if (path.StartsWith('~'))
		{
			path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
		}
		if (Worlds.Check(path) is string problem)
		{
			PathError.Text = problem;
			return;
		}
		PathError.Text = "";
		string full = Path.GetFullPath(path);
		_settings.LastMode = "offline";
		_settings.AddRecent(full, Worlds.WorldName(full));
		_settings.Save();
		OpenRequested?.Invoke(() => Task.Run(() => WorldSession.Open(full)), "Opening the world…");
	}

	// ---- Saved worlds.
	internal void FillWorlds()
	{
		WorldCards.Children.Clear();
		var worlds = FindWorlds(_settings);
		if (worlds.Count == 0)
		{
			WorldCards.Children.Add(Hint("No worlds found on this computer. Choose a folder below, or add world folders in Settings."));
			return;
		}
		foreach (var w in worlds)
		{
			var card = new Button
			{
				Width = 300,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				Padding = Ui.Pad,
				IsEnabled = w.Usable,
				Content = new StackPanel
				{
					Spacing = 2,
					Children =
					{
						new TextBlock { Text = w.Name, FontSize = 15, FontWeight = FontWeight.SemiBold },
						new TextBlock { Text = w.Usable ? $"Saved {w.Saved:g} · save #{w.SaveNumber}" : w.Problem ?? "", FontSize = 12, Foreground = w.Usable ? Muted : Brushes.Orange, TextWrapping = TextWrapping.Wrap },
						new TextBlock { Text = w.Where, FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
					},
				},
			};
			ToolTip.SetTip(card, $"{Tips.Of("start.world")}\n{Ui.Tilde(w.Path)}");
			var path = w.Path;
			card.Click += (_, _) => OpenFolder(path);
			WorldCards.Children.Add(card);
		}
	}

	// ---- Dedicated servers.
	internal StackPanel SavedServers { get; } = new() { Spacing = 6 };
	private readonly TextBlock _savedError = Err(), _serverError = Err(), _urlError = Err();
	internal TextBlock ServerError => _serverError;
	internal TextBlock UrlError => _urlError;
	internal ContentControl GameState => _gameState;

	// Whether Homestead is in the game: the editor's blueprints are Homestead's, built in game with it.
	internal void ShowHomestead(Homestead.Status hs)
	{
		if (hs.Installed)
		{
			HomesteadText.Text = $"Homestead {hs.Version} is installed ({string.Join(", ", hs.Where.Distinct())}): the editor's blueprints show in its hammer tab, to build in game.";
			HomesteadText.Foreground = Muted;
		}
		else
		{
			HomesteadText.Text = "Homestead is not installed in your Valheim's BepInEx (nor in a mod manager profile). The editor's blueprints are Homestead's: install it to build them in game.";
			HomesteadText.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
		}
		GetHomesteadButton.IsVisible = !hs.Installed;
	}
	internal TextBox SName { get; } = new() { PlaceholderText = "how it shows in your list, e.g. Friends server (optional)" };
	internal TextBox SHost { get; } = new() { PlaceholderText = "my.server.com or 203.0.113.10" };
	internal NumericUpDown SPort { get; } = new() { Value = 22, Minimum = 1, Maximum = 65535, FormatString = "0" };
	internal TextBox SUser { get; } = new() { PlaceholderText = "the account you log in with" };
	internal TextBox SPass { get; } = new() { PasswordChar = '•', PlaceholderText = "or use a key file below" };
	internal TextBox SKey { get; } = new() { PlaceholderText = "~/.ssh/id_ed25519" };
	internal TextBox SToken { get; } = new() { PlaceholderText = "the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server" };
	internal TextBox SFolder { get; } = new() { PlaceholderText = "/home/valheim/server, the folder with BepInEx (for the plugin's port and precise errors)" };
	internal CheckBox SSave { get; } = new() { Content = "Save password" };
	internal TextBox SPhrase { get; } = new() { PasswordChar = '•' };
	internal NumericUpDown SBridge { get; } = new() { Minimum = 1, Maximum = 65535, FormatString = "0", PlaceholderText = "5182" };
	internal TextBox LiveUrl { get; } = new() { Text = "127.0.0.1:5182" };
	internal TextBox LiveToken { get; } = new() { PlaceholderText = "from BepInEx/config/Tie.WorldEditorBridge.cfg" };

	private void BuildServerPanel()
	{
		Control Field(string label, Control box)
		{
			var l = new TextBlock { Text = label, FontSize = 12, Foreground = Muted };
			Tips.Label(l, box);
			return new StackPanel { Spacing = 3, Children = { l, box } };
		}
		SName.Tip("start.name");
		SHost.Tip("start.host");
		SPort.Tip("start.sshPort");
		SUser.Tip("start.user");
		SPass.Tip("start.password");
		SKey.Tip("start.keyFile");
		SToken.Tip("start.token");
		SFolder.Tip("start.gameFolder");
		SSave.Tip("start.savePassword");
		SPhrase.Tip("start.passphrase");
		SBridge.Tip("start.bridgePort");
		LiveUrl.Tip("start.bridgeUrl");
		LiveToken.Tip("start.token");
		Control Two(Control a, Control b, string cols = "*,*") => new Grid { ColumnDefinitions = new ColumnDefinitions(cols), ColumnSpacing = 10, Children = { a, Col(b, 1) } };
		var keyBrowse = new Button { Content = "Browse…", VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.browse");
		keyBrowse.Click += async (_, _) => { if (await PickFile("Choose the SSH key file") is string p) SKey.Text = p; };
		var connect = new Button { Content = "Connect", HorizontalAlignment = HorizontalAlignment.Right }.Classed("primary").Tip("start.connect");
		connect.Click += async (_, _) => await ConnectNew();
		var connectUrl = new Button { Content = "Connect", VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.connectUrl");
		connectUrl.Click += async (_, _) => await ConnectUrl();
		ServerPanel.Children.Add(SavedServers);
		ServerPanel.Children.Add(_savedError);
		// The form folds away once there are saved servers (they connect with one click).
		ServerForm.Content = new StackPanel
		{
			Spacing = 8,
			Margin = new Thickness(0, 6, 0, 0),
			Children =
			{
				Field("Name", SName),
				Two(Field("Server address", SHost), Field("SSH port", SPort), "*,110"),
				Two(Field("User", SUser), Field("Password", SPass)),
				Two(Field("SSH key file (optional)", SKey), keyBrowse, "*,Auto"),
				Field("Plugin token", SToken),
				Field("Server's Valheim folder (optional)", SFolder),
				new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { SSave, Col(connect, 1) } },
				_serverError,
				Hint("The server needs BepInEx and the WorldEditorBridge plugin. The editor logs in with SSH and connects to the plugin through its own encrypted tunnel: no ssh command to type. After the first connection the server (with its token) is saved for one-click access."),
				new Expander
				{
					Header = "More options",
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Content = new StackPanel
					{
						Spacing = 8,
						Children =
						{
							Two(Field("Key passphrase", SPhrase), Field("Plugin port", SBridge), "*,130"),
							new TextBlock { Text = "I made my own tunnel", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) },
							new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 10, Children = { Field("Bridge address", LiveUrl), Col(Field("Token", LiveToken), 1), Col(connectUrl, 2) } },
							_urlError,
						},
					},
				},
			},
		};
		ServerPanel.Children.Add(Card(ServerForm));
	}

	internal void FillServers()
	{
		SavedServers.Children.Clear();
		var servers = ServerConfig.Load();
		bool any = servers.Count > 0;
		SavedServers.IsVisible = any;
		ServerForm.IsExpanded = !any;
		((TextBlock)ServerForm.Header!).Text = any ? "Connect to another server" : "Connect to a server";
		foreach (var s in servers)
		{
			var token = s.Token == null ? new TextBox { PlaceholderText = "Plugin token", Width = 180 }.Tip("start.savedToken") : null;
			var pass = s.Password == null && s.KeyFile == null ? new TextBox { PlaceholderText = "Password", PasswordChar = '•', Width = 180 }.Tip("start.savedPassword") : null;
			var go = new Button { Content = "Connect" }.Classed("primary").Tip("start.savedConnect");
			var server = s;
			go.Click += async (_, _) => await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, pass?.Text ?? server.Password, server.KeyFile, null, token?.Text ?? server.Token, null,
				server.Password != null, server.Name, server.GameFolder), _savedError, $"Connecting to {server.Name}…");
			var edit = new Button { Content = "edit", Foreground = Muted }.Classed("ghost").Tip("start.savedEdit");
			edit.Click += (_, _) =>
			{
				ServerForm.IsExpanded = true;
				(SHost.Text, SPort.Value, SUser.Text, SKey.Text, SFolder.Text, SName.Text) = (server.Host, server.SshPort, server.User, server.KeyFile, server.GameFolder, server.Name);
				_serverError.Text = "Change what you need, enter the token and the password (unless saved or using a key), then Connect: the saved server is updated.";
			};
			var forget = new Button { Content = "forget", Foreground = Muted }.Classed("ghost").Tip("start.savedForget");
			forget.Click += async (_, _) =>
			{
				if (await Confirm($"Forget {server.Name}?"))
				{
					ServerConfig.Forget(server.Id);
					FillServers();
				}
			};
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			row.Children.Add(new StackPanel
			{
				Width = 330,
				Children =
				{
					new TextBlock { Text = s.Name, FontWeight = FontWeight.SemiBold },
					new TextBlock { Text = $"{s.User}@{s.Host}{(s.SshPort != 22 ? ":" + s.SshPort : "")} · {(s.KeyFile != null ? "key file" : s.Password != null ? "password saved" : "password asked")}{(s.GameFolder != null ? " · " + s.GameFolder : "")}", FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
				},
			});
			if (token != null) row.Children.Add(token);
			if (pass != null) row.Children.Add(pass);
			row.Children.Add(go);
			row.Children.Add(edit);
			row.Children.Add(forget);
			SavedServers.Children.Add(Card(row));
		}
	}

	internal async Task ConnectNew()
	{
		if (string.IsNullOrWhiteSpace(SHost.Text))
		{
			_serverError.Text = "Enter the server address.";
			return;
		}
		if (string.IsNullOrWhiteSpace(SToken.Text))
		{
			_serverError.Text = "Enter the plugin's token: the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server.";
			return;
		}
		await Connect(new Tunnel.Request(SHost.Text.Trim(), (int)(SPort.Value ?? 22), SUser.Text?.Trim() ?? "", NullIfEmpty(SPass.Text), NullIfEmpty(SKey.Text), NullIfEmpty(SPhrase.Text), SToken.Text.Trim(),
			SBridge.Value is decimal b ? (int)b : null, SSave.IsChecked == true, NullIfEmpty(SName.Text), NullIfEmpty(SFolder.Text)), _serverError, $"Connecting to {SHost.Text.Trim()}…");
	}

	private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

	// Opens the SSH tunnel, checks the plugin answers through it, remembers the server, then opens.
	private async Task Connect(Tunnel.Request t, TextBlock error, string what)
	{
		error.Text = what;
		var tr = await StartTunnel(t);
		if (tr.Error != null)
		{
			error.Text = tr.Error;
			return;
		}
		string url = $"http://127.0.0.1:{tr.LocalPort}";
		string? down = await LiveBridge.Check(url, tr.Token, TimeSpan.FromSeconds(10));
		if (down != null)
		{
			string? why = down.Contains("refused the token") ? null : Tunnel.Diagnose(t.GameFolder);
			Tunnel.Close();
			error.Text = down.Contains("refused the token") ? down : why ?? "The tunnel is open, but the plugin does not answer on the server: is the server running with BepInEx and WorldEditorBridge? Set the server's Valheim folder under \"More options\" for a precise check.";
			return;
		}
		if (tr.Server != null)
		{
			ServerConfig.Remember(tr.Server);
		}
		error.Text = "";
		OpenLive(new LiveBridge(url, tr.Token), "server " + t.Host, "server", "Loading the world from the server…");
	}

	internal async Task ConnectUrl()
	{
		string url = LiveUrl.Text?.Trim() ?? "";
		if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			url = "http://" + url;
		}
		string token = LiveToken.Text?.Trim() ?? "";
		if (await LiveBridge.Check(url, token, TimeSpan.FromSeconds(10)) is string unreachable)
		{
			_urlError.Text = unreachable;
			return;
		}
		_settings.LiveUrl = url;
		OpenLive(new LiveBridge(url, token), "live " + url, "server", "Loading the world from the game…");
	}
}
