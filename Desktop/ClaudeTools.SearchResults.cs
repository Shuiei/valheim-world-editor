using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// search_world's answer, once the search is done (on the window's thread): the hits with the open area's id of each
    /// (when it is in it), nearest a point first when one is given; or why the ids no longer fit.
    /// </summary>
    /// <param name="world">The world searched.</param>
    /// <param name="scene">The open area when the search began (null: none).</param>
    /// <param name="reads">The area's read count then.</param>
    /// <param name="res">The search's result.</param>
    /// <param name="x">World x of the point to sort the hits by, or null.</param>
    /// <param name="z">World z of that point.</param>
    /// <param name="limit">At most this many hits.</param>
    private string SearchAnswer(WorldSession world, WorldScene? scene, int reads, WorldSearch.Result res, float? x, float? z, int limit)
    {
        // Another world opened while it was searched: its hits are the old world's, and the area's ids would be
        // given to objects of the new one with the same saved ids.
        if (_w.World != world)
        {
            return "The world changed while it was searched: search again.";
        }
        // Another area opened, or the world read again after a save (the objects added before it have saved ids
        // now, and new ones take the added ids again): the hits' ids would be given to other objects.
        if ((_w.InWorkshop ? null : _w.Session?.Scene) != scene || scene != null && scene.Reads != reads)
        {
            return "The open area changed while it was searched (another area, or the world saved): search again.";
        }
        var inArea = AreaIdsOf(scene);
        var hits = res.Hits.AsEnumerable();
        if (x is float px && z is float pz)
        {
            hits = hits.OrderBy(h => (h.X - px) * (h.X - px) + (h.Z - pz) * (h.Z - pz));
        }
        var given = hits.Take(Math.Clamp(limit, 1, MostHits)).Select(h => new
        {
            name = h.Name,
            x = MathF.Round(h.X, 1),
            y = MathF.Round(h.Y, 1),
            z = MathF.Round(h.Z, 1),
            match = h.Match,
            areaId = inArea.TryGetValue(h.Id, out int i) ? i : (int?)null,
        }).ToList();
        // Ids of the area given: the ids are its (a search with no hit in it gives none, and says nothing of them).
        if (scene != null && given.Any(h => h.areaId != null))
        {
            GaveIds(scene, reads);
        }
        return ToJson(new
        {
            found = res.Total,
            byName = res.Counts.OrderByDescending(c => c.Value).Take(40).Select(c => new { name = c.Key, count = c.Value }),
            hits = given,
            note = res.Truncated ? $"Only the first {res.Hits.Count} hits were kept: ask for something narrower." : null,
        });
    }

    /// <summary>The open area's things by their world id, each with its index into the area (none without an area).</summary>
    /// <param name="scene">The open area, or null.</param>
    private static Dictionary<int, int> AreaIdsOf(WorldScene? scene)
    {
        var inArea = new Dictionary<int, int>();
        if (scene != null)
        {
            lock (scene.Things)
            {
                for (int i = 0; i < scene.Things.Count; i++)
                {
                    if (!scene.Things[i].Gone)
                    {
                        inArea.TryAdd(scene.Things[i].Id, i);
                    }
                }
            }
        }
        return inArea;
    }
}
