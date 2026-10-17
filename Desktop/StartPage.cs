using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The start page, like the web editor's (start.html): how to edit (your game live, a dedicated server
/// live, or a saved world offline), the worlds found on this computer, saved servers, and the settings.
/// Choosing one opens the world (OpenRequested), then the map.
/// </summary>
public sealed class StartPage
{
    /// <summary>The page itself: the side menu and the chosen page.</summary>
    public Control View { get; }
    /// <summary>The settings (last mode, recent worlds, folders), saved when a world is opened.</summary>
    private readonly AppSettings _settings;

    /// <summary>A world to open: how to open it (run by the window, which shows the progress) and what to say meanwhile.</summary>
    public event Action<Func<Task<WorldSession>>, string>? OpenRequested;
    /// <summary>The Workshop: a blueprint file to open in it, or null for a blank plot.</summary>
    public event Action<string?>? WorkshopRequested;
    /// <summary>Asks the window for a folder (Browse…), with the picker's title; null when cancelled.</summary>
    internal Func<string, Task<string?>> PickFolder { get; set; } = _ => Task.FromResult<string?>(null);
    /// <summary>Asks the window for a file (Browse…), with the picker's title; null when cancelled.</summary>
    internal Func<string, Task<string?>> PickFile { get; set; } = _ => Task.FromResult<string?>(null);
    /// <summary>Asks the window a yes/no question (forgetting a server).</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>Tests: the SSH tunnel (the real one otherwise).</summary>
    internal Func<Tunnel.Request, Task<Tunnel.Result>> StartTunnel { get; set; } = Tunnel.Start;
    /// <summary>Tests: the worlds found on this computer (the real ones otherwise).</summary>
    internal Func<AppSettings, List<Worlds.Info>> FindWorlds { get; set; } = Worlds.Find;
    /// <summary>The Settings button was clicked.</summary>
    public event Action? SettingsRequested;
    /// <summary>The project's page, which Documentation opens.</summary>
    public const string DocsUrl = "https://github.com/Shuiei/valheim-world-editor#readme";
    /// <summary>Documentation: the project's page, in the web browser.</summary>
    internal Button DocsLink { get; } = new Button { Content = Icons.With("help", Lang.T("start.label.docs")), FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");
    /// <summary>Opens a web page or file with the system (the window's launcher); does nothing until set.</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);
    /// <summary>Open log: ValheimWorldEditor.log in the system's text viewer, to attach to a bug report.</summary>
    internal Button LogLink { get; } = new Button { Content = Lang.T("start.label.log"), FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");

    /// <summary>The card about reading the game's look from Valheim (or finding its folder), above every page.</summary>
    internal GameLookBanner LookBanner { get; }
    /// <summary>The side menu's buttons by page key ("home", "game", "server", "offline", "workshop", "new").</summary>
    internal Dictionary<string, Button> ModeButtons { get; } = [];
    /// <summary>My game's page: the game's state and what to do.</summary>
    internal StackPanel GamePanel { get; } = new() { Spacing = 8 };
    /// <summary>A dedicated server's page: saved servers, then the form.</summary>
    internal StackPanel ServerPanel { get; } = new() { Spacing = 8 };
    /// <summary>The form to connect to a new server (folded away once there are saved servers).</summary>
    internal Expander ServerForm { get; } = new() { Header = new TextBlock { Text = Lang.T("start.label.connectServer"), FontWeight = FontWeight.SemiBold }, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>A saved world's page: the worlds found, and another folder to open.</summary>
    internal StackPanel OfflinePanel { get; } = new() { Spacing = 8 };
    /// <summary>Making something new, apart from editing a world: a new world.</summary>
    internal Button NewWorldButton { get; } = new Button { Content = Lang.T("start.label.newWorld"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    /// <summary>Make a new world was clicked.</summary>
    public event Action? NewWorldRequested;
    /// <summary>Open the Workshop on a blank plot.</summary>
    internal Button NewBuildingButton { get; } = new Button { Content = Lang.T("start.label.openWorkshop"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    /// <summary>The Workshop page's copy of Homestead's state.</summary>
    internal TextBlock WorkshopHomestead { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    // The pages the side menu shows (one at a time), and what Home and the Workshop list.
    /// <summary>Home's page: the running game's card, then the recent tiles.</summary>
    internal StackPanel HomePanel { get; } = new() { Spacing = 10 };
    /// <summary>The Workshop's page: open it, Homestead's state, the blueprints.</summary>
    internal StackPanel WorkshopPanel { get; } = new() { Spacing = 10 };
    /// <summary>A new world's page.</summary>
    internal StackPanel NewWorldPanel { get; } = new() { Spacing = 10 };
    /// <summary>Home's tiles: recent worlds and blueprints, then one-click servers.</summary>
    internal WrapPanel RecentTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
    /// <summary>The Workshop's tiles: every blueprint.</summary>
    internal WrapPanel BlueprintTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
    /// <summary>Home's card for the running game (empty while it does not run).</summary>
    private readonly ContentControl _running = new();
    /// <summary>Home's error line (a server tile that could not connect).</summary>
    private readonly TextBlock _homeError = Err();
    /// <summary>The chosen page's title.</summary>
    private readonly TextBlock _pageTitle = new() { FontSize = 26, FontWeight = FontWeight.SemiBold };
    /// <summary>What the chosen page is for (hidden when empty).</summary>
    private readonly TextBlock _pageText = new() { Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
    /// <summary>The "running" tag beside My game in the menu.</summary>
    private readonly TextBlock _gameTag = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(123, 196, 127)), VerticalAlignment = VerticalAlignment.Center };
    /// <summary>A card per world found on this computer.</summary>
    internal WrapPanel WorldCards { get; } = new() { ItemSpacing = 10, LineSpacing = 10 };
    /// <summary>Looks for the worlds again (folders added or removed while the editor runs).</summary>
    internal Button RefreshWorlds { get; } = new Button { Content = Icons.With("reload", Lang.T("start.label.refresh")), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left }.Classed("ghost");
    /// <summary>Another world folder to open.</summary>
    internal TextBox PathBox { get; } = new() { PlaceholderText = Lang.T("start.hint.folder"), FontSize = 13 };
    /// <summary>Why that folder cannot be opened.</summary>
    internal TextBlock PathError { get; } = Err();
    /// <summary>The error that brought the window back here (a world that could not open), above every page.</summary>
    private readonly TextBlock _lastError = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    /// <summary>Polls whether the game runs (on Home and My game): every 2.5 s, 5 s while it runs.</summary>
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    /// <summary>Looks for the worlds again every 3 s while A saved world shows, so added or removed worlds show by themselves.</summary>
    private readonly DispatcherTimer _worldsTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    /// <summary>The worlds as last shown (path, save, usable), so an unchanged look redraws nothing.</summary>
    private string _worldsKey = "";
    /// <summary>The look for the worlds running, or the last one (Refresh's or the timer's; see <see cref="CheckWorlds"/>).</summary>
    internal Task? WorldsCheck { get; private set; }
    /// <summary>Counts the looks for the worlds, so a look overtaken by a later one shows nothing.</summary>
    private int _worldsLook;
    /// <summary>Counts the fills of Home's tiles, so a fill overtaken by a later one shows nothing.</summary>
    private int _homeLook;
    /// <summary>Counts the fills of the Workshop's tiles, likewise.</summary>
    private int _workshopLook;
    /// <summary>The blueprint pictures Home's tiles show, disposed when the tiles are made again.</summary>
    private readonly List<Bitmap> _homePictures = [];
    /// <summary>The blueprint pictures the Workshop's tiles show, likewise.</summary>
    private readonly List<Bitmap> _workshopPictures = [];

    /// <summary>Homestead (the building library's in-game side): found in the game's BepInEx or not.</summary>
    internal TextBlock HomesteadText { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>Opens Homestead's page; shown while it is not installed.</summary>
    internal Button GetHomesteadButton { get; } = new() { Content = Lang.T("blueprints.label.getHomestead"), FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false };
    /// <summary>Why the game could not be checked (the folders or the plugin), until a check works again.</summary>
    private readonly TextBlock _gameError = Err();

    /// <summary>
    /// Whether a connection is being made (Connect, ConnectUrl): another click meanwhile does nothing, so no second
    /// tunnel or world is opened.
    /// </summary>
    private bool _connecting;

    /// <summary>Shorthands for the app's line and muted text brushes.</summary>
    private static readonly IBrush Line = Ui.Line, Muted = Ui.Muted;

    /// <summary>A path under the home folder as ~/… (see Ui.Tilde).</summary>
    internal static string Tilde(string path) => Ui.Tilde(path);

    /// <summary>An error line: takes no room while it says nothing.</summary>
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
    /// <summary>A small grey wrapped explanation.</summary>
    private static TextBlock Hint(string t) => new() { Text = t, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>A floating card (Ui.Card).</summary>
    private static Border Card(Control c) => Ui.Card(c);
    /// <summary>A section heading in grey capitals.</summary>
    private static TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = Muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 18, 0, 6) };

    /// <summary>Builds the page on Home, and looks for Homestead in the background.</summary>
    /// <param name="settings">The app's settings.</param>
    /// <param name="lastError">Why the window came back here (shown at the top); null for none.</param>
    public StartPage(AppSettings settings, string? lastError = null)
    {
        _settings = settings;
        // The bridge address last connected to (Connect by address), so a tunnel's is not typed again each start.
        if (!string.IsNullOrWhiteSpace(settings.LiveUrl))
        {
            LiveUrl.Text = settings.LiveUrl;
        }
        LookBanner = new GameLookBanner(settings) { PickFolder = t => PickFolder(t), OpenLog = OpenLog };
        LookBanner.BusyChanged += _ => ShowBusy();
        var settingsButton = new Button { Content = Icons.With("settings", Lang.T("start.label.settings")), FontSize = 13 }.Classed("ghost");
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
            label.Children.Add(Ui.Col(new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center }, 1));
            if (tag != null)
            {
                label.Children.Add(Ui.Col(tag, 2));
            }
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 9) }.Classed("ghost");
            b.Tip($"start.{key}");
            b.Click += (_, _) => SetMode(key);
            ModeButtons[key] = b;
            menu.Children.Add(b);
        }
        TextBlock Section(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 11, Foreground = Muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(12, 16, 0, 4) };
        Item("home", Lang.T("start.label.home"), "home");
        menu.Children.Add(Section(Lang.T("start.head.edit")));
        Item("game", Lang.T("start.page.game"), "game", _gameTag);
        Item("server", Lang.T("start.page.server"), "server");
        Item("offline", Lang.T("start.page.offline"), "folder");
        menu.Children.Add(Section(Lang.T("start.head.create")));
        Item("workshop", Lang.T("start.page.workshop"), "shape");
        Item("new", Lang.T("start.page.new"), "globe");
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

        // A tick during a check skips: asking for one more after it would chain slow checks without a pause.
        _gameTimer.Tick += async (_, _) =>
        {
            if (_poll is { IsCompleted: false })
            {
                return;
            }
            await PollGame();
        };
        _worldsTimer.Tick += async (_, _) =>
        {
            if (WorldsCheck is { IsCompleted: false })
            {
                return;
            }
            WorldsCheck = CheckWorlds();
            await WorldsCheck;
        };

        // A dedicated server.
        BuildServerPanel();

        // A saved world.
        var browse = new Button { Content = Lang.T("common.browse") }.Tip("start.browse");
        browse.Click += async (_, _) => { if (await PickFolder(Lang.T("start.pickFolder")) is string p) { PathBox.Text = p; } };
        var openPath = new Button { Content = Lang.T("common.open") }.Classed("primary").Tip("start.open");
        PathBox.Tip("start.folder");
        openPath.Click += (_, _) => OpenFolder(PathBox.Text ?? "");
        RefreshWorlds.Tip("start.refreshWorlds");
        RefreshWorlds.Click += async (_, _) =>
        {
            WorldsCheck = CheckWorlds();
            await WorldsCheck;
        };
        OfflinePanel.Children.Add(RefreshWorlds);
        OfflinePanel.Children.Add(WorldCards);
        OfflinePanel.Children.Add(Hint(Lang.T("start.hint.closeGame")));
        OfflinePanel.Children.Add(Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("start.head.otherFolder"), FontWeight = FontWeight.SemiBold },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6, Children = { PathBox, Ui.Col(browse, 1), Ui.Col(openPath, 2) } },
                PathError,
                Hint(Lang.T("start.hint.otherFolder")),
            },
        }));

        NewBuildingButton.Tip("start.workshop");
        NewBuildingButton.Click += (_, _) => WorkshopRequested?.Invoke(null);
        NewWorldButton.Tip("start.new");
        NewWorldButton.Click += (_, _) => NewWorldRequested?.Invoke();

        // Home: the game when it runs, then what was opened last.
        HomePanel.Children.Add(_running);
        HomePanel.Children.Add(_homeError);
        HomePanel.Children.Add(H2(Lang.T("start.head.recent")));
        HomePanel.Children.Add(RecentTiles);

        // The Workshop: open it, Homestead's state, the blueprints to open in it.
        WorkshopPanel.Children.Add(NewBuildingButton);
        WorkshopPanel.Children.Add(new StackPanel { Spacing = 4, Children = { WorkshopHomestead, GetHomesteadButton } });
        WorkshopPanel.Children.Add(H2(Lang.T("start.head.blueprints")));
        WorkshopPanel.Children.Add(BlueprintTiles);

        // A new world.
        NewWorldPanel.Children.Add(NewWorldButton);
        NewWorldPanel.Children.Add(Hint(Lang.T("start.hint.newWorld")));

        var footer = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                settingsButton, DocsLink, LogLink,
                new TextBlock { Text = Lang.T("start.version", BuildInfo.Version), Foreground = Muted, FontSize = 11, Margin = new Thickness(12, 8, 0, 0) },
                new TextBlock { Text = Lang.T("start.dataDir", Tilde(AppSettings.DataDir)), Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 8, 0) },
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
        _busyOff = [HomePanel, GamePanel, ServerPanel, OfflinePanel, WorkshopPanel, NewWorldPanel, settingsButton];
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

    /// <summary>What is turned off while the game's files are read: every page's actions, and Settings (which can change the game folder).</summary>
    private readonly Control[] _busyOff;

    /// <summary>
    /// Greys out every action while the game's files are read (LookBanner.Busy), and gives them back once it is done:
    /// what is opened meanwhile would miss the game's look. The side menu still moves between the pages.
    /// </summary>
    private void ShowBusy()
    {
        foreach (var c in _busyOff)
        {
            c.IsEnabled = !LookBanner.Busy;
            c.Opacity = LookBanner.Busy ? 0.45 : 1;
        }
    }

    /// <summary>The page shown (a <c>ModeButtons</c> key).</summary>
    public string Mode { get; private set; } = "";

    /// <summary>
    /// A page's title and what it is for, in the language in use (asked for each time: a page made after the language
    /// changed shows the new one).
    /// </summary>
    /// <param name="mode">The page (a <c>ModeButtons</c> key).</param>
    /// <exception cref="ArgumentOutOfRangeException">No such page.</exception>
    private static (string Title, string Text) PageTexts(string mode) => mode switch
    {
        "home" => (Lang.T("start.page.home"), ""),
        "game" => (Lang.T("start.page.game"), Lang.T("start.page.gameText")),
        "server" => (Lang.T("start.page.server"), Lang.T("start.page.serverText")),
        // The same words as the mode's tip on the left.
        "offline" => (Lang.T("start.page.offline"), Lang.T("tip.start.offline")),
        "workshop" => (Lang.T("start.page.workshop"), Lang.T("start.page.workshopText")),
        "new" => (Lang.T("start.page.new"), Lang.T("start.page.newText")),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "No such start page."),
    };

    /// <summary>
    /// Shows a page ("home", "game", "server", "offline", "workshop", "new") and fills it; the game is polled
    /// on Home and My game only.
    /// </summary>
    public void SetMode(string mode)
    {
        Mode = mode;
        foreach (var (k, b) in ModeButtons)
        {
            b.Classes.Set("on", k == mode);
            b.Background = k == mode ? Ui.Panel2 : Brushes.Transparent;
        }
        (_pageTitle.Text, _pageText.Text) = PageTexts(mode);
        _pageText.IsVisible = _pageText.Text.Length > 0;
        HomePanel.IsVisible = mode == "home";
        GamePanel.IsVisible = mode == "game";
        ServerPanel.IsVisible = mode == "server";
        OfflinePanel.IsVisible = mode == "offline";
        WorkshopPanel.IsVisible = mode == "workshop";
        NewWorldPanel.IsVisible = mode == "new";
        _gameTimer.Stop();
        _worldsTimer.Stop();
        // The game is watched on Home too (it offers to edit it live).
        if (mode is "game" or "home")
        {
            _ = PollGame();
            _gameTimer.Start();
        }
        if (mode == "home")
        {
            _ = FillHome();
        }
        if (mode == "workshop")
        {
            _ = FillBlueprints();
        }
        if (mode == "server")
        {
            FillServers();
        }
        if (mode == "offline")
        {
            FillWorlds();
            _worldsTimer.Start();
        }
    }

    /// <summary>Stops polling the game and the worlds (the page is left).</summary>
    public void Stop()
    {
        _gameTimer.Stop();
        _worldsTimer.Stop();
    }

    // ---- My game: is Valheim running with the plugin?
    /// <summary>The game's state as last shown (state, world, players, where), so an unchanged poll redraws nothing.</summary>
    private string _gameKey = "";
    /// <summary>The check of the game running or last run (see <see cref="PollGame"/>).</summary>
    private Task? _poll;
    /// <summary>Asked for during the running check: one more runs after it.</summary>
    private bool _pollAgain;

    /// <summary>
    /// One check of the game at a time: the timer does not wait for a slow one, and an older one finishing
    /// last would show its stale state (or error) over the newer one's. Asked during a check, it waits for
    /// it and one more check after (asks during the same check make that one more check together), so the
    /// state shown once it returns is newer than the ask. All on the window's thread.
    /// </summary>
    internal Task PollGame()
    {
        if (_poll is { IsCompleted: false })
        {
            _pollAgain = true;
            return _poll;
        }
        return _poll = Checks();

        async Task Checks()
        {
            do
            {
                _pollAgain = false;
                await CheckGame();
            }
            while (_pollAgain);
        }
    }

    /// <summary>
    /// Is Valheim running with the plugin? Looks for the plugin's installs and a running game, and shows the state
    /// (running, installed, not started once, no plugin, no BepInEx) with what to do. A failed look shows why on the
    /// card and in the log, the state shown before staying.
    /// </summary>
    private async Task CheckGame()
    {
        List<LocalGame.Bridge> bridges;
        bool bepInEx, plugin;
        LocalGame.Running? running;
        string where, state, key;
        try
        {
            (bridges, bepInEx, plugin) = await Task.Run(() =>
            {
                var found = LocalGame.FindBridges(_settings, out bool b, out bool p);
                return (found, b, p);
            });
            running = await LocalGame.FindRunning(bridges);
            where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where ?? "";
            state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
            key = $"{state}|{running?.World}|{running?.Players}|{where}";
            // Homestead is looked for again only when the game's state changes: it searches every BepInEx's plugins.
            if (key != _gameKey)
            {
                ShowHomestead(await Task.Run(() => Homestead.Find(_settings)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            // Polled every few seconds: the log gets a failure once, not at each poll.
            string why = Lang.T("start.error.check", ex.Message);
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
        if (key == _gameKey)
        {
            return;
        }
        _gameKey = key;
        _gameTag.Text = running != null ? Lang.T("start.label.running") : "";
        _running.Content = running == null ? null : RunningCard(running, where);
        Control content;
        if (running != null)
        {
            var edit = EditLiveButton(running);
            content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.running", running.World), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.playersAt", running.Players, where) : Lang.T("start.game.players", running.Players)) } },
                    Ui.Col(edit, 1),
                },
            };
        }
        else if (state == "installed")
        {
            content = new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.startValheim"), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.installedAt", where) : Lang.T("start.game.installed")) } };
        }
        else if (state == "plugin-not-started")
        {
            content = new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.startOnce"), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(Lang.T("start.game.startOnceHint")) } };
        }
        else
        {
            content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("start.game.needs"), FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    Hint(state == "no-plugin" ? Lang.T("start.game.bepInExThere") : ""),
                    new TextBlock { Text = Lang.T("start.game.step1"), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Lang.T("start.game.step2"), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Lang.T("start.game.step3"), TextWrapping = TextWrapping.Wrap },
                },
            };
        }
        GameState.Content = content;
        _gameTimer.Interval = TimeSpan.FromSeconds(running != null ? 5 : 2.5);
    }

    // ---- Opening.
    /// <summary>Opens a live world through the bridge, remembering the mode as the last one used.</summary>
    /// <param name="live">The connection to the plugin.</param>
    /// <param name="label">The session's label ("my game", "server " and the host, "live " and the address).</param>
    /// <param name="mode">The page to come back to next time (LastMode).</param>
    /// <param name="what">What to say while it loads.</param>
    private void OpenLive(LiveBridge live, string label, string mode, string what)
    {
        _settings.LastMode = mode;
        _settings.Save();
        OpenRequested?.Invoke(() => WorldSession.OpenLive(live, label), what);
    }

    /// <summary>Opens the editor's log file with the system.</summary>
    internal Task<bool> OpenLog() => OpenUrl(new Uri(Path.GetFullPath(Log.FilePath)));

    /// <summary>
    /// Opens a world folder offline (a leading ~ is the home folder), adding it to Recent; a folder that is no
    /// world says why in <c>PathError</c>.
    /// </summary>
    internal void OpenFolder(string path)
    {
        path = Places.ExpandHome(path);
        if (Worlds.Check(path) is string problem)
        {
            PathError.Text = problem;
            return;
        }
        PathError.Text = "";
        string full = Path.GetFullPath(path);
        _settings.LastMode = "offline";
        // Saves the settings, LastMode with them.
        _settings.AddRecent(full, Worlds.WorldName(full));
        OpenRequested?.Invoke(() => Task.Run(() => WorldSession.Open(full)), Lang.T("start.msg.opening"));
    }

    // ---- Saved worlds.
    /// <summary>The Edit live button for the game running with the plugin: opens its world live through the bridge.</summary>
    /// <param name="running">The running game.</param>
    private Button EditLiveButton(LocalGame.Running running)
    {
        var edit = new Button { Content = Lang.T("start.label.editLive"), VerticalAlignment = VerticalAlignment.Center }.Classed("primary").Tip("start.editLive");
        var bridge = running.Bridge;
        edit.Click += (_, _) => OpenLive(new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token), "my game", "game", Lang.T("start.msg.connectingGame"));
        return edit;
    }

    /// <summary>Home's card when the game runs with the plugin: its world, and Edit live.</summary>
    private Border RunningCard(LocalGame.Running running, string where)
    {
        var edit = EditLiveButton(running);
        edit.FontSize = 13;
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
                    new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.running", running.World), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.playersLiveAt", running.Players, where) : Lang.T("start.game.playersLive", running.Players)) } },
                    Ui.Col(edit, 1),
                },
            },
        };
    }

    /// <summary>
    /// Home's tiles: saved worlds and blueprints opened or saved last (newest first), then the servers
    /// that connect in one click (token and password or key saved).
    /// </summary>
    internal async Task FillHome()
    {
        int look = ++_homeLook;
        var blueprints = await Task.Run(() => Pictured(Blueprints().Take(4)));
        if (look != _homeLook)
        {
            Dispose(blueprints);
            return;
        }
        Keep(_homePictures, blueprints);
        RecentTiles.Children.Clear();
        var dated = new List<(DateTime When, Button Tile)>();
        foreach (var r in _settings.Recent.Where(r => Directory.Exists(r.Path)).Take(8))
        {
            string path = r.Path;
            dated.Add((r.Opened, Tile(null, "folder", Color.FromRgb(42, 58, 44), r.Name, Lang.T("start.tile.savedWorld", Ago(r.Opened)), Ui.Tilde(path), () => OpenFolder(path))));
        }
        foreach (var (b, picture) in blueprints)
        {
            string path = b.Path;
            dated.Add((b.Saved, Tile(picture, "shape", Color.FromRgb(58, 48, 36), b.Name, Lang.T("start.tile.blueprint", b.Pieces), Lang.T("start.tip.openWorkshop"), () => WorkshopRequested?.Invoke(path))));
        }
        foreach (var (_, tile) in dated.OrderByDescending(d => d.When).Take(8))
        {
            RecentTiles.Children.Add(tile);
        }
        foreach (var s in ServerConfig.Load().Where(s => s.Token != null && (s.Password != null || s.KeyFile != null)))
        {
            var server = s;
            RecentTiles.Children.Add(Tile(null, "server", Color.FromRgb(40, 51, 70), s.Name, Lang.T("start.tile.server"), $"{s.User}@{s.Host}", async () =>
                await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, server.Password, server.KeyFile, null, server.Token, null,
                    server.Password != null, server.Name, server.GameFolder), _homeError, Lang.T("start.msg.connectingTo", server.Name))));
        }
        if (RecentTiles.Children.Count == 0)
        {
            RecentTiles.Children.Add(Hint(Lang.T("start.hint.nothingYet")));
        }
    }

    /// <summary>The Workshop's tiles: every blueprint, newest first.</summary>
    internal async Task FillBlueprints()
    {
        int look = ++_workshopLook;
        var blueprints = await Task.Run(() => Pictured(Blueprints()));
        if (look != _workshopLook)
        {
            Dispose(blueprints);
            return;
        }
        Keep(_workshopPictures, blueprints);
        BlueprintTiles.Children.Clear();
        foreach (var (b, picture) in blueprints)
        {
            string path = b.Path;
            BlueprintTiles.Children.Add(Tile(picture, "shape", Color.FromRgb(58, 48, 36), b.Name, Lang.T("start.tile.pieces", b.Pieces, Ago(b.Saved)), Lang.T("start.tip.openWorkshop"), () => WorkshopRequested?.Invoke(path)));
        }
        if (BlueprintTiles.Children.Count == 0)
        {
            BlueprintTiles.Children.Add(Hint(Lang.T("start.hint.noBlueprints")));
        }
    }

    /// <summary>The blueprints in Homestead's folder, newest first; empty when the folder cannot be read.</summary>
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

    /// <summary>
    /// Blueprints with their pictures decoded at twice the tiles' width (sharp on high-density screens, not full size);
    /// a picture missing or unreadable is null. Run away from the window's thread.
    /// </summary>
    private static List<(Homestead.Entry Entry, Bitmap? Picture)> Pictured(IEnumerable<Homestead.Entry> blueprints)
    {
        var pictured = new List<(Homestead.Entry, Bitmap?)>();
        foreach (var b in blueprints)
        {
            Bitmap? picture = null;
            if (b.Picture != null && File.Exists(b.Picture))
            {
                try
                {
                    using var s = File.OpenRead(b.Picture);
                    picture = Bitmap.DecodeToWidth(s, 440);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    // The kind's icon instead.
                }
            }
            pictured.Add((b, picture));
        }
        return pictured;
    }

    /// <summary>Disposes the pictures of blueprints that are not shown (a fill overtaken by a later one).</summary>
    private static void Dispose(List<(Homestead.Entry Entry, Bitmap? Picture)> unshown)
    {
        foreach (var (_, picture) in unshown)
        {
            picture?.Dispose();
        }
    }

    /// <summary>Disposes the pictures tiles showed until now and holds the new tiles' ones instead.</summary>
    private static void Keep(List<Bitmap> held, List<(Homestead.Entry Entry, Bitmap? Picture)> now)
    {
        foreach (var b in held)
        {
            b.Dispose();
        }
        held.Clear();
        foreach (var (_, picture) in now)
        {
            if (picture != null)
            {
                held.Add(picture);
            }
        }
    }

    /// <summary>A tile: a picture (or the kind's icon on its colour), the name, a line under it.</summary>
    /// <param name="picture">A picture; null shows the icon instead.</param>
    /// <param name="icon">The kind's icon.</param>
    /// <param name="tint">The icon's background colour.</param>
    /// <param name="name">The name, in bold.</param>
    /// <param name="line">The line under it.</param>
    /// <param name="tip">The tile's tooltip.</param>
    /// <param name="open">What a click does.</param>
    private static Button Tile(Bitmap? picture, string icon, Color tint, string name, string line, string tip, Action open)
    {
        Control top = picture != null ? new Image { Source = picture, Stretch = Stretch.UniformToFill }
            : new Border { Background = new SolidColorBrush(tint), Child = new Viewbox { Width = 30, Height = 30, Child = Icons.Make(icon, 30), Opacity = 0.7 } };
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

    /// <summary>A date as "today", "yesterday", "n days ago" (under two weeks) or the date itself.</summary>
    private static string Ago(DateTime when)
    {
        int days = (DateTime.Now.Date - when.Date).Days;
        return days <= 0 ? Lang.T("start.ago.today") : days == 1 ? Lang.T("start.ago.yesterday") : days < 14 ? Lang.T("start.ago.days", days) : Lang.T("start.ago.date", when);
    }

    /// <summary>
    /// A card per world found on this computer (worlds that cannot be opened greyed out, with why), looked for away
    /// from the window's thread (<see cref="WorldsCheck"/>): a slow or broken folder neither freezes the page nor
    /// leaves it half set.
    /// </summary>
    internal void FillWorlds() => WorldsCheck = CheckWorlds(always: true);

    /// <summary>
    /// Looks for the worlds again away from the window's thread (a slow drive does not freeze it) and shows
    /// them when they changed: a world folder added or removed, or a world saved again. A look that a later
    /// one (Refresh, or the page shown again) overtook shows nothing: its worlds are older.
    /// </summary>
    /// <param name="always">Shows them even when they did not change, or another page shows (the list filled).</param>
    internal async Task CheckWorlds(bool always = false)
    {
        int look = ++_worldsLook;
        List<Worlds.Info> worlds;
        try
        {
            worlds = await Task.Run(() => FindWorlds(_settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return; // A folder changing while it is read (or the settings while they are walked): the next look sees it.
        }
        if (look == _worldsLook && (always || Mode == "offline" && WorldsKey(worlds) != _worldsKey))
        {
            ShowWorlds(worlds);
        }
    }

    /// <summary>What a list of worlds shows (every detail on a card), to compare one look with the last.</summary>
    private static string WorldsKey(List<Worlds.Info> worlds) =>
        string.Join('\n', worlds.Select(w => $"{w.Path}|{w.Name}|{w.Where}|{w.SaveNumber}|{w.Saved.Ticks}|{w.Usable}|{w.Problem}"));

    /// <summary>
    /// Where a world was found (<see cref="Worlds.Info.Where"/>: "recent", "yours", "local") as the user reads it; another
    /// value as it is.
    /// </summary>
    /// <param name="where">The value.</param>
    internal static string WhereName(string where) => where switch
    {
        "recent" => Lang.T("start.where.recent"),
        "yours" => Lang.T("start.where.yours"),
        "local" => Lang.T("start.where.local"),
        _ => where,
    };

    /// <summary>A card per world given.</summary>
    private void ShowWorlds(List<Worlds.Info> worlds)
    {
        _worldsKey = WorldsKey(worlds);
        WorldCards.Children.Clear();
        if (worlds.Count == 0)
        {
            WorldCards.Children.Add(Hint(Lang.T("start.hint.noWorlds")));
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
                        new TextBlock { Text = w.Usable ? Lang.T("start.world.saved", w.Saved, w.SaveNumber) : w.Problem ?? "", FontSize = 12, Foreground = w.Usable ? Muted : Brushes.Orange, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = WhereName(w.Where), FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
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
    /// <summary>A row per saved server, to connect, edit or forget it.</summary>
    internal StackPanel SavedServers { get; } = new() { Spacing = 6 };
    /// <summary>A saved server's connection error.</summary>
    private readonly TextBlock _savedError = Err();
    /// <summary>The server form's error (or progress) line.</summary>
    internal TextBlock ServerError { get; } = Err();
    /// <summary>The own tunnel's error line.</summary>
    internal TextBlock UrlError { get; } = Err();
    /// <summary>My game's card: the game's state and what to do.</summary>
    internal ContentControl GameState { get; } = new();

    /// <summary>Whether Homestead is in the game: the editor's blueprints are Homestead's, built in game with it.</summary>
    internal void ShowHomestead(Homestead.Status hs)
    {
        if (hs.Installed)
        {
            HomesteadText.Text = Lang.T("start.homestead.installed", hs.Version, string.Join(", ", hs.Where.Distinct()));
            HomesteadText.Foreground = Muted;
        }
        else
        {
            HomesteadText.Text = Lang.T("start.homestead.missing");
            HomesteadText.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
        }
        GetHomesteadButton.IsVisible = !hs.Installed;
        WorkshopHomestead.Text = HomesteadText.Text;
        WorkshopHomestead.Foreground = HomesteadText.Foreground;
    }
    /// <summary>The server form: the name it shows under in the list (optional).</summary>
    internal TextBox SName { get; } = new() { PlaceholderText = Lang.T("start.hint.name") };
    /// <summary>The server's address.</summary>
    internal TextBox SHost { get; } = new() { PlaceholderText = Lang.T("start.hint.host") };
    /// <summary>The SSH port (22).</summary>
    internal NumericUpDown SPort { get; } = new() { Value = 22, Minimum = 1, Maximum = 65535, FormatString = "0" };
    /// <summary>The SSH user.</summary>
    internal TextBox SUser { get; } = new() { PlaceholderText = Lang.T("start.hint.user") };
    /// <summary>The SSH password (or a key file instead).</summary>
    internal TextBox SPass { get; } = new() { PasswordChar = '•', PlaceholderText = Lang.T("start.hint.password") };
    /// <summary>The SSH key file (optional).</summary>
    internal TextBox SKey { get; } = new() { PlaceholderText = Lang.T("start.hint.keyFile") };
    /// <summary>The plugin's token, from its config file on the server.</summary>
    internal TextBox SToken { get; } = new() { PlaceholderText = Lang.T("start.hint.token") };
    /// <summary>The server's Valheim folder (optional: the plugin's port and precise errors).</summary>
    internal TextBox SFolder { get; } = new() { PlaceholderText = Lang.T("start.hint.gameFolder") };
    /// <summary>Save the password with the server.</summary>
    internal CheckBox SSave { get; } = new() { Content = Lang.T("start.label.savePassword") };
    /// <summary>The key file's passphrase (never saved).</summary>
    internal TextBox SPhrase { get; } = new() { PasswordChar = '•' };
    /// <summary>The plugin's port on the server, when not the default.</summary>
    internal NumericUpDown SBridge { get; } = new() { Minimum = 1, Maximum = 65535, FormatString = "0", PlaceholderText = "5182" };
    /// <summary>I made my own tunnel: the bridge's address.</summary>
    internal TextBox LiveUrl { get; } = new() { Text = "127.0.0.1:5182" };
    /// <summary>I made my own tunnel: the plugin's token.</summary>
    internal TextBox LiveToken { get; } = new() { PlaceholderText = Lang.T("start.hint.bridgeToken") };

    /// <summary>Builds the dedicated server page: the saved servers, then the form (with More options).</summary>
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
        Control Two(Control a, Control b, string cols = "*,*") => new Grid { ColumnDefinitions = new ColumnDefinitions(cols), ColumnSpacing = 10, Children = { a, Ui.Col(b, 1) } };
        var keyBrowse = new Button { Content = Lang.T("common.browse"), VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.browse");
        keyBrowse.Click += async (_, _) => { if (await PickFile(Lang.T("start.pickKey")) is string p) { SKey.Text = p; } };
        var connect = new Button { Content = Lang.T("start.label.connect"), HorizontalAlignment = HorizontalAlignment.Right }.Classed("primary").Tip("start.connect");
        connect.Click += async (_, _) => await ConnectNew();
        var connectUrl = new Button { Content = Lang.T("start.label.connect"), VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.connectUrl");
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
                Field(Lang.T("dialog.label.name"), SName),
                Two(Field(Lang.T("start.label.host"), SHost), Field(Lang.T("start.label.sshPort"), SPort), "*,110"),
                Two(Field(Lang.T("start.label.user"), SUser), Field(Lang.T("start.label.password"), SPass)),
                Two(Field(Lang.T("start.label.keyFile"), SKey), keyBrowse, "*,Auto"),
                Field(Lang.T("start.label.pluginToken"), SToken),
                Field(Lang.T("start.label.gameFolder"), SFolder),
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { SSave, Ui.Col(connect, 1) } },
                ServerError,
                Hint(Lang.T("start.hint.server")),
                new Expander
                {
                    Header = Lang.T("start.label.moreOptions"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            Two(Field(Lang.T("start.label.passphrase"), SPhrase), Field(Lang.T("start.label.pluginPort"), SBridge), "*,130"),
                            new TextBlock { Text = Lang.T("start.head.ownTunnel"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) },
                            new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 10, Children = { Field(Lang.T("start.label.bridgeAddress"), LiveUrl), Ui.Col(Field(Lang.T("start.label.token"), LiveToken), 1), Ui.Col(connectUrl, 2) } },
                            UrlError,
                        },
                    },
                },
            },
        };
        ServerPanel.Children.Add(Card(ServerForm));
    }

    /// <summary>
    /// A row per saved server: Connect (asking for what is not saved: token, password, key passphrase), edit (into
    /// the form) and forget.
    /// </summary>
    internal void FillServers()
    {
        SavedServers.Children.Clear();
        var servers = ServerConfig.Load();
        bool any = servers.Count > 0;
        SavedServers.IsVisible = any;
        ServerForm.IsExpanded = !any;
        ((TextBlock)ServerForm.Header!).Text = any ? Lang.T("start.label.connectAnother") : Lang.T("start.label.connectServer");
        foreach (var s in servers)
        {
            var token = s.Token == null ? new TextBox { PlaceholderText = Lang.T("start.label.pluginToken"), Width = 180 }.Tip("start.savedToken") : null;
            var pass = s.Password == null && s.KeyFile == null ? new TextBox { PlaceholderText = Lang.T("start.label.password"), PasswordChar = '•', Width = 180 }.Tip("start.savedPassword") : null;
            // A key file with a passphrase: never saved, so asked here (it could not connect again).
            var phrase = s.KeyFile != null ? new TextBox { PlaceholderText = Lang.T("start.hint.savedPassphrase"), PasswordChar = '•', Width = 180 }.Tip("start.savedPassphrase") : null;
            var go = new Button { Content = Lang.T("start.label.connect") }.Classed("primary").Tip("start.savedConnect");
            var server = s;
            go.Click += async (_, _) => await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, pass?.Text ?? server.Password, server.KeyFile,
                string.IsNullOrEmpty(phrase?.Text) ? null : phrase.Text, token?.Text ?? server.Token, null,
                server.Password != null, server.Name, server.GameFolder), _savedError, Lang.T("start.msg.connectingTo", server.Name));
            var edit = new Button { Content = Lang.T("start.label.edit"), Foreground = Muted }.Classed("ghost").Tip("start.savedEdit");
            edit.Click += (_, _) =>
            {
                ServerForm.IsExpanded = true;
                (SHost.Text, SPort.Value, SUser.Text, SKey.Text, SFolder.Text, SName.Text) = (server.Host, server.SshPort, server.User, server.KeyFile, server.GameFolder, server.Name);
                // All of the form is this server's: what was typed for another one (its password, token) is never
                // sent here, and a saved password stays saved.
                (SPass.Text, SToken.Text, SPhrase.Text, SBridge.Value, SSave.IsChecked) = (server.Password, server.Token, null, null, server.Password != null);
                ServerError.Text = Lang.T("start.hint.editing");
            };
            var forget = new Button { Content = Lang.T("start.label.forget"), Foreground = Muted }.Classed("ghost").Tip("start.savedForget");
            forget.Click += async (_, _) =>
            {
                if (await Confirm(Lang.T("start.ask.forget.text", server.Name)))
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
                    new TextBlock { Text = $"{s.User}@{s.Host}{(s.SshPort != 22 ? ":" + s.SshPort : "")} · {(s.KeyFile != null ? Lang.T("start.server.keyFile") : s.Password != null ? Lang.T("start.server.passwordSaved") : Lang.T("start.server.passwordAsked"))}{(s.GameFolder != null ? " · " + s.GameFolder : "")}", FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
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

    /// <summary>Connects to the server the form describes (address and token required).</summary>
    internal async Task ConnectNew()
    {
        if (string.IsNullOrWhiteSpace(SHost.Text))
        {
            ServerError.Text = Lang.T("start.error.host");
            return;
        }
        if (string.IsNullOrWhiteSpace(SToken.Text))
        {
            ServerError.Text = Lang.T("start.error.token");
            return;
        }
        await Connect(new Tunnel.Request(SHost.Text.Trim(), (int)(SPort.Value ?? 22), SUser.Text?.Trim() ?? "", string.IsNullOrWhiteSpace(SPass.Text) ? null : SPass.Text, NullIfEmpty(SKey.Text), NullIfEmpty(SPhrase.Text), SToken.Text.Trim(),
            SBridge.Value is decimal b ? (int)b : null, SSave.IsChecked == true, NullIfEmpty(SName.Text), NullIfEmpty(SFolder.Text)), ServerError, Lang.T("start.msg.connectingTo", SHost.Text.Trim()));
    }

    /// <summary>Trimmed text, or null when blank.</summary>
    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Opens the SSH tunnel, checks the plugin answers through it, remembers the server, then opens. Progress and
    /// problems go to <paramref name="error"/>; a plugin that does not answer closes the tunnel again.
    /// </summary>
    /// <param name="t">The server and how to log in.</param>
    /// <param name="error">The line for progress and errors.</param>
    /// <param name="what">What to say while connecting.</param>
    private async Task Connect(Tunnel.Request t, TextBlock error, string what)
    {
        if (_connecting)
        {
            return;
        }
        _connecting = true;
        try
        {
            await ConnectOnce(t, error, what);
        }
        finally
        {
            _connecting = false;
        }
    }

    /// <inheritdoc cref="Connect"/>
    private async Task ConnectOnce(Tunnel.Request t, TextBlock error, string what)
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
            string? why = down.Contains("refused the token") ? null : await Tunnel.Diagnose(t.GameFolder);
            Tunnel.Close();
            error.Text = down.Contains("refused the token") ? down : why ?? Lang.T("start.error.noAnswer");
            return;
        }
        if (tr.Server != null)
        {
            ServerConfig.Remember(tr.Server);
        }
        error.Text = "";
        OpenLive(new LiveBridge(url, tr.Token), "server " + t.Host, "server", Lang.T("start.msg.loadingServer"));
    }

    /// <summary>I made my own tunnel: connects to the bridge address typed (http:// added when missing).</summary>
    internal async Task ConnectUrl()
    {
        if (_connecting)
        {
            return;
        }
        _connecting = true;
        try
        {
            await ConnectUrlOnce();
        }
        finally
        {
            _connecting = false;
        }
    }

    /// <inheritdoc cref="ConnectUrl"/>
    private async Task ConnectUrlOnce()
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
        OpenLive(new LiveBridge(url, token), "live " + url, "server", Lang.T("save.msg.loadingLive"));
    }
}
