using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's controls: the side panel's info, the save and live boxes, the map options, the picked spot and the cursor readout.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// The world's name, seed, save number and object count.
    /// </summary>
    internal TextBlock Meta { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// What is waiting to be saved or applied.
    /// </summary>
    internal TextBlock Pending { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Save to world, or Apply live for a live world.
    /// </summary>
    internal Button SaveButton { get; } = new Button { Content = Lang.T("map.label.save"), FontSize = 12.5 }.Classed("primary");
    /// <summary>
    /// Throws away what is waiting.
    /// </summary>
    internal Button DiscardButton { get; } = new() { Content = Lang.T("common.discard"), FontSize = 12 };
    /// <summary>
    /// Live: the players online, or that the game does not answer.
    /// </summary>
    internal TextBlock LiveText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Live: reads the world from the game again.
    /// </summary>
    internal Button ReloadButton { get; } = new() { Content = Icons.With("reload", Lang.T("map.label.reload")), FontSize = 12 };
    /// <summary>
    /// The live box (shown only for a live world) and the waiting-changes box.
    /// </summary>
    private readonly Control _liveBox, _changesBox;
    /// <summary>
    /// Show player-built pieces; its text gets their count once read.
    /// </summary>
    internal CheckBox BuildingsBox { get; } = new() { Content = Lang.T("map.label.buildings"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show ruins: the pieces of the world's ruins too (spoilers; off at first).
    /// </summary>
    internal CheckBox RuinsBox { get; } = new() { Content = Lang.T("map.label.ruins"), IsChecked = false, FontSize = 12 };
    /// <summary>
    /// Show the zone grid.
    /// </summary>
    internal CheckBox GridBox { get; } = new() { Content = Lang.T("map.label.grid"), FontSize = 12 };
    /// <summary>
    /// Outline edited zones.
    /// </summary>
    internal CheckBox EditedBox { get; } = new() { Content = Lang.T("map.label.edited"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show painted ground when zoomed in.
    /// </summary>
    internal CheckBox PaintBox { get; } = new() { Content = Lang.T("map.label.paint"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show the game's map clouds.
    /// </summary>
    internal CheckBox CloudsBox { get; } = new() { Content = Lang.T("map.label.clouds"), FontSize = 12 };
    /// <summary>
    /// The picked zone (or search result) and its biome.
    /// </summary>
    internal TextBlock PickTitle { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Opens the picked spot in the 3D editor.
    /// </summary>
    internal Button EditButton { get; } = new Button { Content = Lang.T("map.label.edit"), FontSize = 12.5 }.Classed("primary");
    /// <summary>
    /// The area to open: 3 to 17 zones a side (<see cref="MainWindow.AreaSizes"/>).
    /// </summary>
    internal ComboBox SizeBox { get; } = new() { ItemsSource = MainWindow.AreaSizes.Select(n => Lang.T("map.label.size", n, n * 64)).ToArray(), SelectedIndex = 1, FontSize = 12 };
    /// <summary>
    /// The picked spot's box, hidden until a spot is picked.
    /// </summary>
    private readonly Control _pickBox;
    /// <summary>
    /// The world point, zone and biome under the mouse; its box hides while empty.
    /// </summary>
    internal TextBlock Cursor { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
    /// <summary>
    /// The map's status line (shown while the map is being built).
    /// </summary>
    private readonly TextBlock _status = new() { FontSize = 13, Foreground = Brushes.White };
}
