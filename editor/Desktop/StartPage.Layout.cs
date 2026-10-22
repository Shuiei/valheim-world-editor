using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
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
        DocsLink.Click += (_, _) => OpenHelp();
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
            LastError.Text = lastError;
            LastError.IsVisible = true;
        }

        // My game.
        GamePanel.Children.Add(Card(GameState));
        GamePanel.Children.Add(_gameError);
        GetHomesteadButton.Tip("blueprints.getHomestead");
        GetHomesteadButton.Click += async (_, _) => await OpenOrSay(new Uri(Homestead.PageUrl));

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
                Children = { LastError, LookBanner.View, _pageTitle, _pageText, new Border { Height = 8 }, HomePanel, GamePanel, ServerPanel, OfflinePanel, WorkshopPanel, NewWorldPanel },
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
}
