using Avalonia;
using Avalonia.Controls;
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

    public event Action<Func<Task<WorldSession>>, string>? OpenRequested;
    // The Workshop: a blueprint file to open in it, or null for a blank plot.
    public event Action<string?>? WorkshopRequested;
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
    // Open log: ValheimWorldEditor.log in the system's text viewer, to attach to a bug report.
    internal Button LogLink { get; } = new Button { Content = "Open log", FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");

    internal GameLookBanner LookBanner { get; }
    internal Dictionary<string, Button> ModeButtons { get; } = [];
    internal StackPanel GamePanel { get; } = new() { Spacing = 8 };
    internal StackPanel ServerPanel { get; } = new() { Spacing = 8 };
    internal Expander ServerForm { get; } = new() { Header = new TextBlock { Text = "Connect to a server", FontWeight = FontWeight.SemiBold }, HorizontalAlignment = HorizontalAlignment.Stretch };
    internal StackPanel OfflinePanel { get; } = new() { Spacing = 8 };
    // Making something new, apart from editing a world: the Workshop and a new world.
    internal Button NewWorldButton { get; } = new Button { Content = "Make a new world", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    public event Action? NewWorldRequested;
    internal Button NewBuildingButton { get; } = new Button { Content = "Open the Workshop", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    internal TextBlock WorkshopHomestead { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    // The pages the side menu shows (one at a time), and what Home and the Workshop list.
    internal StackPanel HomePanel { get; } = new() { Spacing = 10 };
    internal StackPanel WorkshopPanel { get; } = new() { Spacing = 10 };
    internal StackPanel NewWorldPanel { get; } = new() { Spacing = 10 };
    internal WrapPanel RecentTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
    internal WrapPanel BlueprintTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
    private readonly ContentControl _running = new();
    private readonly TextBlock _homeError = Err();
    private readonly TextBlock _pageTitle = new() { FontSize = 26, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _pageText = new() { Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _gameTag = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(123, 196, 127)), VerticalAlignment = VerticalAlignment.Center };
    internal WrapPanel WorldCards { get; } = new() { ItemSpacing = 10, LineSpacing = 10 };
    internal TextBox PathBox { get; } = new() { PlaceholderText = "Folder of the world (with _main.<n>.chunks files)", FontSize = 13 };
    internal TextBlock PathError { get; } = Err();
    private readonly TextBlock _lastError = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };

    // Homestead (the building library's in-game side): found in the game's BepInEx or not.
    internal TextBlock HomesteadText { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    internal Button GetHomesteadButton { get; } = new() { Content = "Get Homestead", FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false };
    // Why the game could not be checked (the folders or the plugin), until a check works again.
    private readonly TextBlock _gameError = Err();

    private static readonly IBrush Line = Ui.Line, Muted = Ui.Muted;

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
        LookBanner = new GameLookBanner(settings) { PickFolder = t => PickFolder(t), OpenLog = OpenLog };
        var settingsButton = new Button { Content = Icons.With("settings", "Settings"), FontSize = 13 }.Classed("ghost");
        settingsButton.Tip("start.settings");
        DocsLink.Tip("start.docs");
        settingsButton.Click += (_, _) => SettingsRequested?.Invoke();
        DocsLink.Click += async (_, _) => await OpenUrl(new Uri(DocsUrl));
        LogLink.Tip("start.log");
        LogLink.Click += async (_, _) => await OpenLog();
        // The side menu: Home, the ways to edit a world, the ways to make something new.
        var menu = new StackPanel { Spacing = 2 };
        void Item(string key, string title, string icon, Control? tag = null)
        {
            var label = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            label.Children.Add(Icons.Make(icon, 16));
            label.Children.Add(Col(new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center }, 1));
            if (tag != null)
            {
                label.Children.Add(Col(tag, 2));
            }
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 9) }.Classed("ghost");
            b.Tip($"start.{key}");
            b.Click += (_, _) => SetMode(key);
            ModeButtons[key] = b;
            menu.Children.Add(b);
        }
        TextBlock Section(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 11, Foreground = Muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(12, 16, 0, 4) };
        Item("home", "Home", "home");
        menu.Children.Add(Section("Edit a world"));
        Item("game", "My game", "game", _gameTag);
        Item("server", "A dedicated server", "server");
        Item("offline", "A saved world", "folder");
        menu.Children.Add(Section("Create"));
        Item("workshop", "The Workshop", "shape");
        Item("new", "A new world", "globe");
        if (lastError != null)
        {
            _lastError.Text = lastError;
            _lastError.IsVisible = true;
        }

        // My game.
        GamePanel.Children.Add(Card(GameState));
        GamePanel.Children.Add(_gameError);
        GetHomesteadButton.Tip("blueprints.getHomestead");
        GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));

        _gameTimer.Tick += async (_, _) => await PollGame();

        // A dedicated server.
        BuildServerPanel();

        // A saved world.
        var browse = new Button { Content = "Browse…" }.Tip("start.browse");
        browse.Click += async (_, _) => { if (await PickFolder("Choose a world folder (with _main.<n>.chunks)") is string p) { PathBox.Text = p; } };
        var openPath = new Button { Content = "Open" }.Classed("primary").Tip("start.open");
        PathBox.Tip("start.folder");
        openPath.Click += (_, _) => OpenFolder(PathBox.Text ?? "");
        OfflinePanel.Children.Add(WorldCards);
        OfflinePanel.Children.Add(Hint("Close Valheim (or stop the server) before you save changes into a world: a running game writes over the files. Each save is read back and checked before the old one is removed."));
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

        NewBuildingButton.Tip("start.workshop");
        NewBuildingButton.Click += (_, _) => WorkshopRequested?.Invoke(null);
        NewWorldButton.Tip("start.new");
        NewWorldButton.Click += (_, _) => NewWorldRequested?.Invoke();

        // Home: the game when it runs, then what was opened last.
        HomePanel.Children.Add(_running);
        HomePanel.Children.Add(_homeError);
        HomePanel.Children.Add(H2("Recent"));
        HomePanel.Children.Add(RecentTiles);

        // The Workshop: open it, Homestead's state, the blueprints to open in it.
        WorkshopPanel.Children.Add(NewBuildingButton);
        WorkshopPanel.Children.Add(new StackPanel { Spacing = 4, Children = { WorkshopHomestead, GetHomesteadButton } });
        WorkshopPanel.Children.Add(H2("Your blueprints"));
        WorkshopPanel.Children.Add(BlueprintTiles);

        // A new world.
        NewWorldPanel.Children.Add(NewWorldButton);
        NewWorldPanel.Children.Add(Hint("The world is a normal Valheim world: players join it without any mod, consoles on crossplay included."));

        var footer = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                settingsButton, DocsLink, LogLink,
                new TextBlock { Text = $"v{BuildInfo.Version}", Foreground = Muted, FontSize = 11, Margin = new Thickness(12, 8, 0, 0) },
                new TextBlock { Text = $"Settings, saved servers and log: {Tilde(AppSettings.DataDir)}", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 8, 0) },
            },
        };
        foreach (var b in new[] { settingsButton, DocsLink, LogLink })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Padding = new Thickness(12, 7);
            b.FontSize = 12.5;
        }
        DockPanel.SetDock(footer, Dock.Bottom);
        var side = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 27, 33)),
            BorderBrush = Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(14, 26, 14, 18),
            Child = new DockPanel
            {
                Children =
                {
                    footer,
                    new StackPanel { Spacing = 0, Children = { Ui.BrandTitle(19), new Border { Height = 18 }, menu } },
                },
            },
        };
        var main = new ScrollViewer
        {
            Content = new StackPanel
            {
                MaxWidth = 1040,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(40, 32, 30, 50),
                Spacing = 6,
                Children = { _lastError, LookBanner.View, _pageTitle, _pageText, new Border { Height = 8 }, HomePanel, GamePanel, ServerPanel, OfflinePanel, WorkshopPanel, NewWorldPanel },
            },
        };
        Grid.SetColumn(main, 1);
        View = new Grid { Background = Ui.Bg, ColumnDefinitions = new ColumnDefinitions("260,*"), Children = { side, main } };
        // Homestead's state for the Workshop (looked for away from the window's thread).
        _ = Task.Run(() => Homestead.Find(settings)).ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion)
            {
                ShowHomestead(t.Result);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
        // Home first: it shows the game when it runs and what was opened last.
        SetMode("home");
    }

    private static Control Col(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }

    public string Mode { get; private set; } = "";

    // Each page's title and what it is for.
    private static readonly Dictionary<string, (string Title, string Text)> Pages = new()
    {
        ["home"] = ("Pick up where you left off", ""),
        ["game"] = ("My game", "Edit the world you are playing in, single player or the one you host. You see the changes in game right away."),
        ["server"] = ("A dedicated server", "Edit your server's world while people play. The editor connects to the server itself."),
        ["offline"] = ("A saved world", "Edit world files on this computer with the game closed, then start the game again."),
        ["workshop"] = ("The Workshop", "Build on a blank plot with every piece the game has, see what would hold, and keep it as a blueprint to build in game with Homestead. Your blueprints are in the Workshop's Library too: open them, add them, or drag them onto the plot."),
        ["new"] = ("A new world", "Look at seeds before their world exists: land, biomes, and exactly where the game will put the start, the bosses, the traders and the dungeons. Let the editor search many seeds for one that suits you, then make the world."),
    };

    public void SetMode(string mode)
    {
        Mode = mode;
        foreach (var (k, b) in ModeButtons)
        {
            b.Classes.Set("on", k == mode);
            b.Background = k == mode ? Ui.Panel2 : Brushes.Transparent;
        }
        (_pageTitle.Text, _pageText.Text) = Pages[mode];
        _pageText.IsVisible = _pageText.Text.Length > 0;
        HomePanel.IsVisible = mode == "home";
        GamePanel.IsVisible = mode == "game";
        ServerPanel.IsVisible = mode == "server";
        OfflinePanel.IsVisible = mode == "offline";
        WorkshopPanel.IsVisible = mode == "workshop";
        NewWorldPanel.IsVisible = mode == "new";
        _gameTimer.Stop();
        // The game is watched on Home too (it offers to edit it live).
        if (mode is "game" or "home")
        {
            _ = PollGame();
            _gameTimer.Start();
        }
        if (mode == "home")
        {
            FillHome();
        }
        if (mode == "workshop")
        {
            FillBlueprints();
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
            // Polled every few seconds: the log gets a failure once, not at each poll.
            string why = $"Your game could not be checked: {ex.Message}";
            if (_gameError.Text != why)
            {
                Console.Error.WriteLine($"My game: {ex}");
                _gameError.Text = why;
            }
            return;
        }
        if (!string.IsNullOrEmpty(_gameError.Text))
        {
            Console.Error.WriteLine("My game: checked again.");
            _gameError.Text = "";
        }
        string where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where ?? "";
        string state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
        string key = $"{state}|{running?.World}|{running?.Players}|{where}";
        if (key == _gameKey)
        {
            return;
        }
        _gameKey = key;
        _gameTag.Text = running != null ? "running" : "";
        _running.Content = running == null ? null : RunningCard(running, where);
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
        GameState.Content = content;
        _gameTimer.Interval = TimeSpan.FromSeconds(running != null ? 5 : 2.5);
    }

    // ---- Opening.
    private void OpenLive(LiveBridge live, string label, string mode, string what)
    {
        _settings.LastMode = mode;
        _settings.Save();
        OpenRequested?.Invoke(() => WorldSession.OpenLive(live, label), what);
    }

    internal Task<bool> OpenLog() => OpenUrl(new Uri(Path.GetFullPath(Log.FilePath)));

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
    // Home's card when the game runs with the plugin: its world, and Edit live.
    private Border RunningCard(LocalGame.Running running, string where)
    {
        var edit = new Button { Content = "Edit live", VerticalAlignment = VerticalAlignment.Center, FontSize = 13 }.Classed("primary").Tip("start.editLive");
        var bridge = running.Bridge;
        edit.Click += (_, _) => OpenLive(new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token), "my game", "game", "Connecting to your game…");
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(31, 42, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(62, 107, 69)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 20,
                Children =
                {
                    new StackPanel { Children = { new TextBlock { Text = $"Valheim is running with the world {running.World}", FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint($"{running.Players} player(s) connected · changes show in game at once{(where != "" ? $" ({where})" : "")}") } },
                    Col(edit, 1),
                },
            },
        };
    }

    // Home's tiles: saved worlds and blueprints opened or saved last (newest first), then the servers
    // that connect in one click.
    internal void FillHome()
    {
        RecentTiles.Children.Clear();
        var dated = new List<(DateTime When, Button Tile)>();
        foreach (var r in _settings.Recent.Where(r => Directory.Exists(r.Path)).Take(8))
        {
            string path = r.Path;
            dated.Add((r.Opened, Tile(null, "folder", Color.FromRgb(42, 58, 44), r.Name, $"Saved world · {Ago(r.Opened)}", Ui.Tilde(path), () => OpenFolder(path))));
        }
        foreach (var b in Blueprints().Take(4))
        {
            string path = b.Path;
            dated.Add((b.Saved, Tile(b.Picture, "shape", Color.FromRgb(58, 48, 36), b.Name, $"Blueprint · {b.Pieces} pieces", "Open it in the Workshop", () => WorkshopRequested?.Invoke(path))));
        }
        foreach (var (_, tile) in dated.OrderByDescending(d => d.When).Take(8))
        {
            RecentTiles.Children.Add(tile);
        }
        foreach (var s in ServerConfig.Load().Where(s => s.Token != null && (s.Password != null || s.KeyFile != null)))
        {
            var server = s;
            RecentTiles.Children.Add(Tile(null, "server", Color.FromRgb(40, 51, 70), s.Name, "Server · connects in one click", $"{s.User}@{s.Host}", async () =>
                await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, server.Password, server.KeyFile, null, server.Token, null,
                    server.Password != null, server.Name, server.GameFolder), _homeError, $"Connecting to {server.Name}…")));
        }
        if (RecentTiles.Children.Count == 0)
        {
            RecentTiles.Children.Add(Hint("Nothing opened yet. Choose how to edit a world, or what to make, on the left."));
        }
    }

    // The Workshop's tiles: every blueprint, newest first.
    internal void FillBlueprints()
    {
        BlueprintTiles.Children.Clear();
        foreach (var b in Blueprints())
        {
            string path = b.Path;
            BlueprintTiles.Children.Add(Tile(b.Picture, "shape", Color.FromRgb(58, 48, 36), b.Name, $"{b.Pieces} pieces · {Ago(b.Saved)}", "Open it in the Workshop", () => WorkshopRequested?.Invoke(path)));
        }
        if (BlueprintTiles.Children.Count == 0)
        {
            BlueprintTiles.Children.Add(Hint("No blueprints yet: build one in the Workshop and save it."));
        }
    }

    private static List<Homestead.Entry> Blueprints()
    {
        try
        {
            return [.. Homestead.List(Homestead.Folder()).OrderByDescending(b => b.Saved)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // A tile: a picture (or the kind's icon on its colour), the name, a line under it.
    private static Button Tile(string? picture, string icon, Color tint, string name, string line, string tip, Action open)
    {
        Control top = new Border { Background = new SolidColorBrush(tint), Child = new Viewbox { Width = 30, Height = 30, Child = Icons.Make(icon, 30), Opacity = 0.7 } };
        if (picture != null && File.Exists(picture))
        {
            try
            {
                using var s = File.OpenRead(picture);
                top = new Image { Source = new Avalonia.Media.Imaging.Bitmap(s), Stretch = Stretch.UniformToFill };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                // The kind's icon instead.
            }
        }
        var tile = new Button
        {
            Width = 220,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Height = 100, ClipToBounds = true, CornerRadius = new CornerRadius(6, 6, 0, 0), Child = top },
                    new StackPanel
                    {
                        Margin = new Thickness(12, 10),
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = line, FontSize = 12, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
                        },
                    },
                },
            },
        };
        ToolTip.SetTip(tile, tip);
        tile.Click += (_, _) => open();
        return tile;
    }

    private static string Ago(DateTime when)
    {
        int days = (DateTime.Now.Date - when.Date).Days;
        return days <= 0 ? "today" : days == 1 ? "yesterday" : days < 14 ? $"{days} days ago" : when.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

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
            string path = w.Path;
            card.Click += (_, _) => OpenFolder(path);
            WorldCards.Children.Add(card);
        }
    }

    // ---- Dedicated servers.
    internal StackPanel SavedServers { get; } = new() { Spacing = 6 };
    private readonly TextBlock _savedError = Err();
    internal TextBlock ServerError { get; } = Err();
    internal TextBlock UrlError { get; } = Err();
    internal ContentControl GameState { get; } = new();

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
        WorkshopHomestead.Text = HomesteadText.Text;
        WorkshopHomestead.Foreground = HomesteadText.Foreground;
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
        keyBrowse.Click += async (_, _) => { if (await PickFile("Choose the SSH key file") is string p) { SKey.Text = p; } };
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
                ServerError,
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
                            UrlError,
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
            // A key file with a passphrase: never saved, so asked here (it could not connect again).
            var phrase = s.KeyFile != null ? new TextBox { PlaceholderText = "Key passphrase (if any)", PasswordChar = '•', Width = 180 }.Tip("start.savedPassphrase") : null;
            var go = new Button { Content = "Connect" }.Classed("primary").Tip("start.savedConnect");
            var server = s;
            go.Click += async (_, _) => await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, pass?.Text ?? server.Password, server.KeyFile,
                string.IsNullOrEmpty(phrase?.Text) ? null : phrase.Text, token?.Text ?? server.Token, null,
                server.Password != null, server.Name, server.GameFolder), _savedError, $"Connecting to {server.Name}…");
            var edit = new Button { Content = "edit", Foreground = Muted }.Classed("ghost").Tip("start.savedEdit");
            edit.Click += (_, _) =>
            {
                ServerForm.IsExpanded = true;
                (SHost.Text, SPort.Value, SUser.Text, SKey.Text, SFolder.Text, SName.Text) = (server.Host, server.SshPort, server.User, server.KeyFile, server.GameFolder, server.Name);
                ServerError.Text = "Change what you need, enter the token and the password (unless saved or using a key), then Connect: the saved server is updated.";
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
            if (token != null)
            {
                row.Children.Add(token);
            }

            if (pass != null)
            {
                row.Children.Add(pass);
            }

            if (phrase != null)
            {
                row.Children.Add(phrase);
            }

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
            ServerError.Text = "Enter the server address.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SToken.Text))
        {
            ServerError.Text = "Enter the plugin's token: the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server (written the first time the server starts with the plugin).";
            return;
        }
        await Connect(new Tunnel.Request(SHost.Text.Trim(), (int)(SPort.Value ?? 22), SUser.Text?.Trim() ?? "", NullIfEmpty(SPass.Text), NullIfEmpty(SKey.Text), NullIfEmpty(SPhrase.Text), SToken.Text.Trim(),
            SBridge.Value is decimal b ? (int)b : null, SSave.IsChecked == true, NullIfEmpty(SName.Text), NullIfEmpty(SFolder.Text)), ServerError, $"Connecting to {SHost.Text.Trim()}…");
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
            UrlError.Text = unreachable;
            return;
        }
        _settings.LiveUrl = url;
        OpenLive(new LiveBridge(url, token), "live " + url, "server", "Loading the world from the game…");
    }
}
