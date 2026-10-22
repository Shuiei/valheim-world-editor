using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's controls: the world's name, seed and folder, Create, the map and its switches, and the lists beside the map.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The world's name; it must be a valid file name.
    /// </summary>
    internal TextBox NameBox { get; } = new() { Text = "", PlaceholderText = Lang.T("newWorld.hint.name"), FontSize = 13 };
    /// <summary>
    /// The seed, up to 10 characters; its world is shown when Generate is pressed.
    /// </summary>
    internal TextBox SeedBox { get; } = new() { FontSize = 13, MaxLength = 10, PlaceholderText = Lang.T("newWorld.hint.seed") };
    /// <summary>
    /// Puts a random seed in the seed box.
    /// </summary>
    internal Button Roll { get; } = new() { Content = Lang.T("newWorld.label.roll"), FontSize = 12 };
    /// <summary>
    /// Shows the seed box's world; greyed while that world is the one shown.
    /// </summary>
    internal Button Generate { get; } = new Button { Content = Lang.T("newWorld.label.generate"), FontSize = 12, IsEnabled = false }.Classed("primary");
    /// <summary>
    /// Which of the game's world folders to create the world in; its <c>Tag</c> holds the full paths.
    /// </summary>
    internal ComboBox Where { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    /// <summary>
    /// Checks the name and seed, then asks the window to create the world.
    /// </summary>
    internal Button Create { get; } = new Button { Content = Lang.T("newWorld.label.create"), FontSize = 13 }.Classed("primary");
    /// <summary>
    /// Why Create refused, or empty.
    /// </summary>
    internal TextBlock Problem { get; } = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>
    /// What the worlds follow when it is not the game itself: Expand World's rules, in a few words; hidden otherwise.
    /// </summary>
    internal TextBlock Generation { get; } = new() { Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = false };

    /// <summary>What kind of world is made (<see cref="KindText"/>): a normal one, or one players need the generator's mod for.</summary>
    internal TextBlock WorldKind { get; } = Ui.Hint("");
    /// <summary>
    /// The seed's map, the landmarks over it, and which kinds show (switches over the map's corner, as the editor's
    /// View panel).
    /// </summary>
    internal SeedMap Map { get; } = new();
    /// <summary>
    /// The landmarks over the map.
    /// </summary>
    internal Canvas Marks { get; } = new() { ClipToBounds = true };
    /// <summary>
    /// Show the start.
    /// </summary>
    internal CheckBox ShowStart { get; } = Toggle("start", "", Lang.T("newWorld.label.start"));
    /// <summary>
    /// Show the bosses' altars.
    /// </summary>
    internal CheckBox ShowBosses { get; } = Toggle("boss", "", Lang.T("newWorld.label.bossAltars"));
    /// <summary>
    /// Show Haldor's spots.
    /// </summary>
    internal CheckBox ShowHaldor { get; } = Toggle("trader", "Haldor", Lang.T("newWorld.landmark.haldor"));
    /// <summary>
    /// Show the Bog Witch's spots.
    /// </summary>
    internal CheckBox ShowBogWitch { get; } = Toggle("trader", "The Bog Witch", Lang.T("newWorld.label.bogWitch"));
    /// <summary>
    /// Show Hildir's spots.
    /// </summary>
    internal CheckBox ShowHildir { get; } = Toggle("trader", "Hildir", Lang.T("newWorld.landmark.hildir"));
    internal TextBlock Readout { get; } = new() { FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(8, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    /// <summary>
    /// Shown over the map while a seed's world is being looked at: what is being done.
    /// </summary>
    internal TextBlock LoadingText { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
    /// <summary>
    /// The loading sign over the map.
    /// </summary>
    internal Border Loading { get; }
    /// <summary>
    /// What the seed's world holds, as lists.
    /// </summary>
    internal StackPanel Info { get; } = new() { Spacing = 14 };
}
