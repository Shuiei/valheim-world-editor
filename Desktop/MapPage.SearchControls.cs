using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The search section's controls and the pinned results' state.
/// </summary>
public sealed partial class MapPage
{
    // Search.
    /// <summary>
    /// What to search for.
    /// </summary>
    internal TextBox SearchBox { get; } = new() { PlaceholderText = Lang.T("map.hint.search"), FontSize = 12 };
    /// <summary>
    /// What to search: object kinds, items in containers, or texts.
    /// </summary>
    internal ComboBox SearchWhat { get; } = new() { ItemsSource = new[] { Lang.T("map.searchWhat.kinds"), Lang.T("map.searchWhat.items"), Lang.T("map.searchWhat.texts") }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>
    /// Runs the search.
    /// </summary>
    internal Button FindButton { get; } = new Button { Content = Icons.With("search", Lang.T("map.label.find")), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Stretch }.Classed("primary");
    /// <summary>
    /// The search's counts, or what went wrong.
    /// </summary>
    internal TextBlock SearchInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The search results (the first 300); picking one goes there.
    /// </summary>
    internal ListBox Hits { get; } = new() { MaxHeight = 220, FontSize = 11 };
    /// <summary>
    /// The last search's results, pinned on the map.
    /// </summary>
    private List<WorldSearch.Hit> _hits = [];
    /// <summary>
    /// What the pinned results were searched for: "kinds", "items" or "texts".
    /// </summary>
    private string _searchWhat = "kinds";
    /// <summary>
    /// Searches started so far, and worlds shown (each makes a running search's results unwanted): only the latest
    /// search's results are shown, and only while the world it searched is.
    /// </summary>
    private int _searches;
    /// <summary>
    /// True while the latest search runs (its "Searching…" shown).
    /// </summary>
    private bool _searching;
}
