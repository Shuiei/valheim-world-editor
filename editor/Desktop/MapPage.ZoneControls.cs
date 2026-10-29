using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The zone filter's controls, and the zone counts and matches kept while the page is open.
/// </summary>
public sealed partial class MapPage
{
    // Zone filter.
    /// <summary>
    /// The zone filter's biome toggles, by biome value.
    /// </summary>
    internal readonly Dictionary<int, ToggleButton> BiomeButtons = [];
    /// <summary>
    /// Only zones with no buildings in them or within <c>DistBox</c> zones.
    /// </summary>
    internal CheckBox NoBuildBox { get; } = new() { Content = Lang.T("map.label.noBuild"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// How many zones around a match must have no buildings, 0..50.
    /// </summary>
    internal NumericUpDown DistBox { get; } = new() { Value = 2, Minimum = 0, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12, MinWidth = 70, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// Only zones without ground edits.
    /// </summary>
    internal CheckBox NoEditBox { get; } = new() { Content = Lang.T("map.label.noEdit"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Only zones the game generated.
    /// </summary>
    internal CheckBox OnlyGenBox { get; } = new() { Content = Lang.T("map.label.onlyGen"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Smallest distance from the world centre, in metres; empty for none.
    /// </summary>
    internal TextBox RMinBox { get; } = new() { PlaceholderText = "0", FontSize = 12, Width = 70 };
    /// <summary>
    /// Largest distance from the world centre, in metres; empty for none.
    /// </summary>
    internal TextBox RMaxBox { get; } = new() { PlaceholderText = "10500", FontSize = 12, Width = 70 };
    /// <summary>
    /// Resetting also undoes the zones' ground edits.
    /// </summary>
    internal CheckBox GroundBox { get; } = new() { Content = Lang.T("map.label.ground"), FontSize = 12 };
    /// <summary>
    /// Shows the zones that match the filter on the map.
    /// </summary>
    internal Button ShowMatchButton { get; } = new() { Content = Lang.T("map.label.showMatch"), FontSize = 12 };
    /// <summary>
    /// Marks the matching zones for reset (after asking).
    /// </summary>
    internal Button MarkButton { get; } = new Button { Content = Lang.T("map.label.mark"), FontSize = 12 }.Classed("primary");
    /// <summary>
    /// Unmarks every zone marked for reset.
    /// </summary>
    internal Button UnmarkButton { get; } = new() { Content = Lang.T("map.label.unmark"), FontSize = 12 };
    /// <summary>
    /// How many zones match, and how many are marked.
    /// </summary>
    internal TextBlock ZoneInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Every zone's counts (<c>ZoneStats.Compute</c>), read when first needed; null after the world changes.
    /// </summary>
    private int[]? _stats;
    /// <summary>
    /// Counts each time the zone counts are thrown away (Show): counts read before that are not kept.
    /// </summary>
    private int _statsGeneration;
    /// <summary>
    /// How many zone count reads are going on (the "Reading every zone…" status is theirs).
    /// </summary>
    private int _reading;
    /// <summary>
    /// Whether matches are shown, so changing the filter shows them again at once.
    /// </summary>
    private bool _filtered;
    /// <summary>
    /// The zones that matched the filter last.
    /// </summary>
    internal List<(int X, int Z)> Matches { get; private set; } = [];
}
