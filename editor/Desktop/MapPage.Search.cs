using Avalonia.Controls;
using TerrainEditor.App;
using Vector2 = System.Numerics.Vector2;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world search: the search box and its button, what to search, and running the search.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>Builds the search section: the box, what to search, the Find button, the counts and the hits.</summary>
    /// <returns>The section.</returns>
    private Expander BuildSearch()
    {
        SearchBox.Tip("map.search");
        FindButton.Tip("map.find");
        FindButton.Click += async (_, _) => await Search();
        SearchBox.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { await Search(); } };
        SearchWhat.SelectionChanged += async (_, _) => { if (!string.IsNullOrWhiteSpace(SearchBox.Text)) { await Search(); } };
        Hits.SelectionChanged += (_, _) => ShowHit(Hits.SelectedIndex);
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
        searchRow.Children.Add(SearchBox);
        Grid.SetRow(SearchWhat, 1);
        searchRow.Children.Add(SearchWhat);
        Grid.SetColumn(FindButton, 1);
        Grid.SetRowSpan(FindButton, 2);
        searchRow.Children.Add(FindButton);
        return Section(Lang.T("map.head.search"), true, searchRow, SearchInfo, Hits);
    }

    // ---- Search: every object of the world by kind, items in containers, or texts.
    /// <summary>
    /// Runs a search (world, text, what) off the window's thread. Tests hold it to start or show something meanwhile.
    /// </summary>
    internal Func<WorldSession, string, string, Task<WorldSearch.Result>> RunSearch { get; set; } =
        (s, q, what) => Task.Run(() => WorldSearch.Search(s.World, s.Edits, q, what));

    /// <summary>
    /// Searches the world for the search box's text and pins what is found; an empty box clears the results.
    /// </summary>
    internal async Task Search()
    {
        if (_session is not { } s)
        {
            return;
        }
        string q = SearchBox.Text?.Trim() ?? "";
        string what = SearchWhat.SelectedIndex switch { 1 => "items", 2 => "texts", _ => "kinds" };
        // A search still running is dropped when it ends: this one (or the cleared box) is newer.
        int search = ++_searches;
        _searching = q.Length > 0;
        if (q.Length == 0)
        {
            _hits = [];
            Hits.ItemsSource = null;
            SearchInfo.Text = "";
            Map.Pins = [];
            Map.RequestNextFrameRendering();
            return;
        }
        SearchInfo.Text = Lang.T("map.msg.searching");
        WorldSearch.Result res;
        try
        {
            res = await RunSearch(s, q, what);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "searching the world");
            if (search == _searches && _session == s)
            {
                _searching = false;
                SearchInfo.Text = Lang.T("map.msg.searchFailed", ex.Message);
            }
            return;
        }
        // Another search started meanwhile, or a world (another, or this one read again) is shown: these results are
        // not wanted.
        if (search != _searches || _session != s)
        {
            return;
        }
        _searching = false;
        _searchWhat = what;
        _hits = res.Hits;
        Map.Pins = [.. _hits.Select(h => new Vector2(h.X, h.Z))];
        Map.PinChosen = -1;
        if (res.Total == 0)
        {
            SearchInfo.Text = Lang.T("map.msg.nothingFound");
            Hits.ItemsSource = null;
        }
        else
        {
            SearchInfo.Text = Found(res, what);
            Hits.ItemsSource = _hits.Take(300).Select(h => $"{h.Name}   {h.X:0}, {h.Z:0}{(h.Match != null ? "\n   " + h.Match : "")}").ToList();
        }
        Map.RequestNextFrameRendering();
    }
}
