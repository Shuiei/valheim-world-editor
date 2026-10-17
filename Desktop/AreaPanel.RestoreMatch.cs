using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Matches the backup's objects against the current ones, cell by cell: a backup object unchanged since the backup
    /// keeps the first current object of its cell (and its neighbours) still free; the others are added from the backup.
    /// </summary>
    /// <param name="bw">The backup's world, for the objects' data.</param>
    /// <param name="s">The scene holding the current objects.</param>
    /// <param name="wanted">The backup's objects inside the selection.</param>
    /// <param name="current">The current objects inside the selection, as indices into the scene's things.</param>
    /// <param name="kept">The current objects kept as they are; filled in here.</param>
    /// <returns>The backup's objects that have no match, to add.</returns>
    private static List<(NewObject, bool)> MatchBackupObjects(WorldSave bw, WorldScene s, List<WorldScene.Thing> wanted, List<int> current, HashSet<int> kept)
    {
        var source = bw.SourceReader();
        // The current objects by prefab and metre cell, in their order: a backup object is matched against the
        // cells round it only (SameObject is within a centimetre), the first in that order still free.
        var byCell = new Dictionary<(int, int, int), List<int>>();
        var order = new Dictionary<int, int>();
        foreach (int i in current)
        {
            var key = CellOf(s.Things[i]);
            (byCell.TryGetValue(key, out var list) ? list : byCell[key] = []).Add(i);
            order.TryAdd(i, order.Count);
        }
        var adds = new List<(NewObject, bool)>();
        foreach (var o in wanted)
        {
            var (prefab, cx, cz) = CellOf(o);
            int r = -1;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (byCell.TryGetValue((prefab, cx + dx, cz + dz), out var near)
                        && near.FirstOrDefault(i => !kept.Contains(i) && SameObject(s.Things[i], o), -1) is int c and >= 0 && (r < 0 || order[c] < order[r]))
                    {
                        r = c;
                    }
                }
            }
            if (r >= 0)
            {
                kept.Add(r);
                continue;
            }
            byte[] raw = bw.ObjectBytes(o.Id, source);
            var z = ZdoData.Parse(raw);
            adds.Add((new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, raw), o.Piece));
        }
        return adds;
    }
}
