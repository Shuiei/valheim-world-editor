using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>Searches all pieces by English name or prefab (every word must match), across the tabs.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("build.hint.search"), FontSize = 12 };

    /// <summary>The piece tiles of the tab or search, with a heading per station.</summary>
    internal WrapPanel Tiles { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };

    /// <summary>Snap to the snap points of the pieces already there.</summary>
    internal CheckBox SnapBox { get; } = new CheckBox { Content = Lang.T("build.label.snap"), FontSize = 12, IsChecked = true }.Classed("switch");

    /// <summary>The grid step for pieces not snapped (Off, 0.5, 1 or 2 m).</summary>
    internal ComboBox GridBox { get; } = new() { ItemsSource = new[] { Lang.T("build.grid.off"), "0.5 m", "1 m", "2 m" }, SelectedIndex = 0, FontSize = 12 };

    /// <summary>The step , and . turn the piece by. The game turns the hammer's piece by 22.5° a step.</summary>
    internal ComboBox TurnBox { get; } = new() { ItemsSource = new[] { "1°", "15°", "22.5°", "45°", "90°" }, SelectedIndex = 2, FontSize = 12 };

    /// <summary>The chosen piece's English name.</summary>
    internal TextBlock Chosen { get; } = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

    /// <summary>What the chosen piece costs in game.</summary>
    internal TextBlock Cost { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap };

    /// <summary>The lift Ctrl + wheel gives (see <see cref="ShowLift"/>).</summary>
    internal TextBlock LiftText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Takes the lift back to none.</summary>
    internal Button LiftReset { get; } = new() { Content = Lang.T("build.label.reset"), FontSize = 11, Padding = new Thickness(6, 1) };

    /// <summary>The tab buttons.</summary>
    private readonly WrapPanel _tabs = new() { ItemSpacing = 3, LineSpacing = 3 };

    /// <summary>On the Mods tab, one button per mod: the tab lists one mod's pieces at a time.</summary>
    private readonly WrapPanel _modRow = new() { ItemSpacing = 3, LineSpacing = 3, IsVisible = false };
}
