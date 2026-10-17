using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>Fills the tiles with the tab's pieces, or with those matching the search across all tabs.</summary>
    internal void Render()
    {
        Tiles.Children.Clear();
        string q = Search.Text?.Trim() ?? "";
        var pieces = Pieces;
        UpdateTabButtons(q, pieces);
        var list = PiecesShown(q, pieces);
        _waiting.Clear();
        if (list.Count == 0)
        {
            Tiles.Children.Add(new TextBlock { Text = Lang.T("build.noMatch"), FontSize = 11, Foreground = Ui.Muted });
            return;
        }
        int all = list.Count;
        list = [.. list.Take(MaxTiles)];
        string? chosen = Tool.Chosen.Count == 1 ? Tool.Chosen[0] : null;
        string? group = "?";
        foreach (var (prefab, name, _) in list)
        {
            // A heading where the station changes (on the Mods tab, the mod or the station).
            string heading = StationHeading(prefab);
            if (heading != group)
            {
                group = heading;
                Tiles.Children.Add(HeadingBlock(heading));
            }
            Tiles.Children.Add(TileButton(prefab, name, chosen));
        }
        if (all > list.Count)
        {
            Tiles.Children.Add(new TextBlock { Text = Lang.T("build.showing", list.Count, all), Width = 320, FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(2, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        }
    }

    /// <summary>Marks the tab on (none while a search runs), and shows the Mods tab only when the game has mods' pieces.</summary>
    /// <param name="q">The search, trimmed (empty: none).</param>
    /// <param name="pieces">Every piece.</param>
    private void UpdateTabButtons(string q, List<(string Prefab, string Name, int Category)> pieces)
    {
        foreach (var b in _tabs.Children.OfType<Button>())
        {
            b.Classes.Set("on", q.Length == 0 && (int)b.Tag! == _tab);
            if ((int)b.Tag! == Mods)
            {
                b.IsVisible = pieces.Any(p => p.Category == Mods);
            }
        }
    }

    /// <summary>The pieces the tiles list: the tab's, or those matching the search across all tabs; on the Mods tab, one mod's.</summary>
    /// <param name="q">The search, trimmed (empty: none).</param>
    /// <param name="pieces">Every piece.</param>
    /// <returns>The pieces, in their order.</returns>
    private List<(string Prefab, string Name, int Category)> PiecesShown(string q, List<(string Prefab, string Name, int Category)> pieces)
    {
        var list = pieces.Where(p => q.Length > 0
            ? q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || p.Prefab.Contains(w, StringComparison.OrdinalIgnoreCase))
            : p.Category == _tab).ToList();
        ShowMods(q.Length == 0 && _tab == Mods ? list : []);
        if (_modRow.IsVisible)
        {
            list = [.. list.Where(p => ModGroup(p.Prefab) == _mod)];
        }
        return list;
    }
}
