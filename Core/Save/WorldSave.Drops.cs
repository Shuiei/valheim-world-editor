using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Whether objects of this prefab shape the ground as the game runs (location proxies and ModifierPrefabs).
    /// </summary>
    private static bool Shapes(int prefab) => prefab == LocationProxyPrefab || ModifierPrefabs.Contains(prefab);

    /// <summary>These objects out of every list (their ids stay; the changed ones are read again after).</summary>
    private void Drop(HashSet<int> ids, Merged result)
    {
        Objects.RemoveAll(x => ids.Contains(x.Id));
        Pieces.RemoveAll(x => ids.Contains(x.Id));
        Discs.RemoveAll(x => ids.Contains(x.Id));
        foreach (int id in ids)
        {
            ObjectRef o = ObjectRefs[id];
            Tamed.Remove(id);
            Links.Remove(id);
            // A builder with no pieces left is no builder (TopBuilder, Builders).
            if (o.Creator != 0 && Creators.TryGetValue(o.Creator, out int n))
            {
                if (n > 1)
                {
                    Creators[o.Creator] = n - 1;
                }
                else
                {
                    Creators.Remove(o.Creator);
                }
            }
            if (o.Prefab == LocationProxyPrefab)
            {
                int at = Locations.FindIndex(l => Vector3.DistanceSquared(l.Position, o.Position) < 1e-4f);
                if (at >= 0)
                {
                    Locations.RemoveAt(at);
                }
            }
            if (Shapes(o.Prefab))
            {
                result.Modifiers = true;
                int placed = Placed.FindIndex(x => x.Prefab == o.Prefab && Vector3.DistanceSquared(x.Position, o.Position) < 1e-4f);
                if (placed >= 0)
                {
                    Placed.RemoveAt(placed);
                }
            }
        }
        var sources = ids.Select(id => (ObjectRefs[id].File, ObjectRefs[id].Start)).ToHashSet();
        foreach (var z in TerrainZones.Where(z => z.Source is { } src && sources.Contains((src.File, src.Start))))
        {
            result.TerrainZones.Add((z.ZoneX, z.ZoneZ));
        }
        TerrainZones.RemoveAll(z => z.Source is { } src && sources.Contains((src.File, src.Start)));
    }

    /// <summary>
    /// The largest live snapshot read, unpacked: the biggest worlds' saves are a few hundred megabytes, and one array
    /// cannot pass 2 GiB anyway.
    /// </summary>
    internal const int MaxSnapshot = 1536 << 20;
}
