using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>Searches all pieces by English name or prefab (every word must match), across the tabs.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("build.hint.search"), FontSize = 12, Width = 240, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The piece tiles of the tab or search, with a heading where the station changes.</summary>
    internal WrapPanel Tiles { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

    /// <summary>How the piece snaps: Auto (as the hammer), Manual (one snap point, Q and E) or Free; one button each, tagged with its mode.</summary>
    internal StackPanel SnapModes { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Snapping by hand: which of the piece's snap points meets the others ("Point 2 of 8").</summary>
    internal TextBlock SnapPointText { get; } = new() { FontSize = 11.5, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    /// <summary>The grid step for pieces not snapped (Off, 0.5, 1 or 2 m).</summary>
    internal ComboBox GridBox { get; } = new() { ItemsSource = new[] { Lang.T("build.grid.off"), "0.5 m", "1 m", "2 m" }, SelectedIndex = 0, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The step , and . turn the piece by. The game turns the hammer's piece by 22.5° a step.</summary>
    internal ComboBox TurnBox { get; } = new() { ItemsSource = new[] { "1°", "15°", "22.5°", "45°", "90°" }, SelectedIndex = 2, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The chosen piece's English name.</summary>
    internal TextBlock Chosen { get; } = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>What the chosen piece costs in game.</summary>
    internal TextBlock Cost { get; } = new() { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The lift Ctrl + wheel gives (see <see cref="ShowLift"/>).</summary>
    internal TextBlock LiftText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Takes the lift back to none.</summary>
    internal Button LiftReset { get; } = new() { Content = Lang.T("build.label.reset"), FontSize = 11, Padding = new Thickness(6, 1), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The hammer's tabs (and the cultivator's and the serving tray's), on the Pieces tab: a button each, tagged with its tab.</summary>
    internal StackPanel HammerTabs { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// On the Mod pieces tab, the mods down its left side, each with its number of pieces (a heading, then a button per
    /// mod, tagged with it): one mod's pieces at a time.
    /// </summary>
    internal StackPanel ModList { get; } = new() { Spacing = 2, IsVisible = false };

    /// <summary>Shows the pieces the game's mods add, one mod at a time.</summary>
    internal Button ModsTab { get; } = new() { Content = Lang.T("build.label.modPieces"), FontSize = 12, Padding = new Thickness(8, 2) };

    /// <summary>Folds the dock down to its row of tabs (and opens it again).</summary>
    internal Button CollapseButton { get; } = new() { FontSize = 12, Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The pieces picked last, newest first: a click picks one again.</summary>
    internal StackPanel RecentRow { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The building settings, in a bar of their own beside the tool rail: snapping, the grid, the turn step and the lift.
    /// Shown with the dock.
    /// </summary>
    public Control Toolbar { get; }

    /// <summary>The Mod pieces tab's word when no mod's piece is known.</summary>
    internal TextBlock NoMods { get; } = new() { Text = Lang.T("build.noModPieces"), FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, IsVisible = false };
}
