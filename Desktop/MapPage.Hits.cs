using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The search's results: what was found, and what happens when a result is picked.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// What a search found, for the search's info line: the twelve most found kinds or items with their counts, then
    /// how many in all and how many are pinned.
    /// </summary>
    /// <param name="res">The search's results (something found).</param>
    /// <param name="what">What was searched: "kinds", "items" or "texts".</param>
    internal static string Found(WorldSearch.Result res, string what)
    {
        var counts = res.Counts.OrderByDescending(c => c.Value).ToList();
        var shown = counts.Take(12).Select(c => Lang.T("map.msg.count", c.Value, c.Key));
        string top = string.Join(" · ", counts.Count > 12 ? shown.Append(Lang.T("map.msg.more", counts.Count - 12)) : shown);
        string found = (what == "items", res.Truncated) switch
        {
            (true, true) => Lang.T("map.msg.foundInAllPinned", res.Total, res.Hits.Count),
            (true, false) => Lang.T("map.msg.foundInAll", res.Total),
            (false, true) => Lang.T("map.msg.foundPinned", res.Total, res.Hits.Count),
            (false, false) => Lang.T("map.msg.found", res.Total),
        };
        return $"{top}\n{found}";
    }

    /// <summary>
    /// A result picked: the map goes there, and Edit in 3D selects it (items and texts: in the inspector).
    /// </summary>
    internal void ShowHit(int i)
    {
        if (i < 0 || i >= _hits.Count)
        {
            return;
        }
        var h = _hits[i];
        Map.PinChosen = i;
        Map.LookAt(h.X, h.Z, MathF.Min(Map.MetersPerPixel, 0.6f));
        var (zx, zz) = Save.ChunkMath.SectorOf(h.X, h.Z);
        Pick(zx, zz);
        Target = (h.Id, _searchWhat != "kinds");
        PickTitle.Text = Lang.T("map.label.hit", h.Name, h.X, h.Z, zx, zz);
    }
}
