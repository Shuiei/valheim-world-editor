
namespace TerrainEditor.Terrain;

public static partial class PrefabCatalog
{
    /// <summary>
    /// Adds an entry's item rules: what the item is, what the item stand takes and the armour stand's slots.
    /// </summary>
    internal static void AddItemRules(Data d, int hash, Entry e)
    {
        if (e.it >= 0)
        {
            d.ItemKinds[hash] = new ItemKind(e.it, e.ao, e.var, e.at != 0, e.ats != 0, (float)e.dur, (float)e.durl, Math.Max(1, e.ms));
        }
        if (e.@is != null)
        {
            d.ItemStands[hash] = new StandRule(e.@is.ToHashSet(), (e.isi ?? []).ToHashSet(), (e.isu ?? []).ToHashSet());
        }
        if (e.@as != null)
        {
            d.ArmourStands[hash] = e.@as;
        }
    }

    /// <summary>
    /// Adds an entry's spawner, when it is a spawn point or a nest.
    /// </summary>
    internal static void AddSpawn(Data d, int hash, Entry e)
    {
        if ((e.cs ?? e.sa) is { } spawned)
        {
            d.Spawns[hash] = new SpawnKind(spawned, e.sa != null, e.trig != 0, (float)e.rt, e.lv?.ElementAtOrDefault(0) ?? 1, e.lv?.ElementAtOrDefault(1) ?? 1, e.day != 0, e.night != 0, (float)e.sr, e.max);
        }
    }

    /// <summary>
    /// Records what a sapling grows into, keeping the largest grow radius when several saplings grow the same thing.
    /// </summary>
    internal static void AddGrownFrom(Data d, string name, Entry e)
    {
        foreach (string grown in e.grows ?? [])
        {
            if (!d.GrownFrom.TryGetValue(grown, out var g) || g.Radius < e.gr)
            {
                d.GrownFrom[grown] = ((float)e.gr, name);
            }
        }
    }
}
