using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Set up once, from the constructor (after the top bar).</summary>
    private void SetUpEditorWorld()
    {
        AreaNav.Children.Add(new TextBlock { Text = Lang.T("top.label.area"), FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (b, dx, dz) in new[] { (AreaWest, -1, 0), (AreaNorth, 0, 1), (AreaSouth, 0, -1), (AreaEast, 1, 0) })
        {
            b.Click += async (_, _) => await MoveArea(dx, dz);
            AreaNav.Children.Add(b);
        }
        AreaNav.Children.Add(AreaSizeBox);
        AreaSizeBox.Tip("top.areaSize");
        AreaSizeBox.SelectionChanged += async (_, _) =>
        {
            if (!_fillingSize && AreaSizeBox.SelectedIndex >= 0)
            {
                await ResizeArea(AreaSizes[AreaSizeBox.SelectedIndex]);
            }
        };
        AreaNav.Children.Add(FollowButton);
        FollowButton.Tip("top.follow");
        FollowButton.Classes.Set("on", Settings.AreaFollow);
        FollowButton.Click += (_, _) => SetFollow(!Settings.AreaFollow);
        _followTimer.Tick += async (_, _) => await CheckFollow();
        // While the window is open (a running timer keeps the window alive).
        Opened += (_, _) => _followTimer.Start();
        Closed += (_, _) =>
        {
            _followTimer.Stop();
            _gameTimer.Stop();
            _playersTimer.Stop();
            _labelsTimer.Stop();
            MapPage?.Stop();
        };

        DiscardButton.Tip("top.discard");
        DiscardButton.Click += async (_, _) => await DiscardHere();
        ReloadButton.Tip("top.reload");
        ReloadButton.Click += async (_, _) => await ReloadHere();
        AutoApplyBox.Tip("top.auto");
        AutoApplyBox.IsChecked = Settings.AutoApply;
        AutoApplyBox.IsCheckedChanged += async (_, _) =>
        {
            Settings.AutoApply = AutoApplyBox.IsChecked == true;
            SaveSettings(Settings);
            if (Settings.AutoApply && Session is { IsLive: true } s && s.Pending is not (0, 0, 0, 0))
            {
                await ApplyNow();
            }
        };

        var close = new Button { Content = Icons.Make("close", 12), Padding = new Thickness(4) }.Classed("ghost").Tip("note.close");
        close.Click += (_, _) => LocationNote.IsVisible = false;
        Grid.SetColumn(close, 1);
        LocationNote = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2c2416")),
            BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(10, 0, 10, 58),
            MaxWidth = 420,
            // Its left margin follows the tool column (KeepNoteBesideTools): over it, it covered the rail's last tools
            // and the bottom of the tool's panel.
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = false,
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { LocationText, close } },
        };

        GoPlayerButton.Click += async (_, _) => await GoToPlayer(PlayerBox.SelectedItem as string);
        _playersTimer.Tick += async (_, _) => await PollPlayers();
        _gameTimer.Tick += async (_, _) => await FollowGame();
        _labelsTimer.Tick += (_, _) => PlaceLabels();
    }

    /// <summary>
    /// Keeps the locations note just right of the cards of the tool column that reach down to it (the rail, a tall
    /// tool panel; not the short Mask card at the top), whatever their width.
    /// </summary>
    /// <param name="tools">The tool column.</param>
    private void KeepNoteBesideTools(Panel tools) => tools.LayoutUpdated += (_, _) =>
    {
        double right = tools.Children.Where(c => c.IsVisible && c.Bounds.Bottom > tools.Bounds.Height - 100).Select(c => c.Bounds.Right).DefaultIfEmpty(0).Max();
        LocationNote.Margin = new Thickness(tools.Bounds.X + right + Ui.Gap, 0, 10, _panelsBottom);
    };

    /// <summary>The View panel's Go to a player (live worlds).</summary>
    private StackPanel PlayersControl()
    {
        PlayersRow.Children.Add(new TextBlock { Text = Lang.T("live.head.players"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(0, 8, 0, 2) });
        PlayerBox.Tip("view.player");
        GoPlayerButton.Tip("view.goPlayer");
        PlayersRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Lang.T("live.label.goTo"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, PlayerBox, GoPlayerButton } });
        return PlayersRow;
    }

    /// <summary>After the top bar's save line is updated: the editor's own buttons.</summary>
    private void UpdateEditorWorld(EditSession s, bool dirty)
    {
        DiscardButton.IsEnabled = dirty;
        ReloadButton.IsVisible = AutoApplyBox.IsVisible = s.IsLive;
    }
}
