
namespace TerrainEditor.Terrain;

public static partial class PrefabCatalog
{
    /// <summary>
    /// Adds an entry's item rules: what the item is, what the item stand takes and the armour stand's slots.
    /// </summary>
    internal static void AddItemRules(Data d, int hash, Entry e)
    {
        if (e.It >= 0)
        {
            d.ItemKinds[hash] = new ItemKind(e.It, e.Ao, e.Var, e.At != 0, e.Ats != 0, (float)e.Dur, (float)e.Durl, Math.Max(1, e.Ms));
        }
        if (e.Is != null)
        {
            d.ItemStands[hash] = new StandRule(e.Is.ToHashSet(), (e.Isi ?? []).ToHashSet(), (e.Isu ?? []).ToHashSet());
        }
        if (e.As != null)
        {
            d.ArmourStands[hash] = e.As;
        }
    }

    /// <summary>
    /// Adds an entry's spawner, when it is a spawn point or a nest.
    /// </summary>
    internal static void AddSpawn(Data d, int hash, Entry e)
    {
        if ((e.Cs ?? e.Sa) is { } spawned)
        {
            d.Spawns[hash] = new SpawnKind(spawned, e.Sa != null, e.Trig != 0, (float)e.Rt, e.Lv?.ElementAtOrDefault(0) ?? 1, e.Lv?.ElementAtOrDefault(1) ?? 1, e.Day != 0, e.Night != 0, (float)e.Sr, e.Max);
        }
    }

    /// <summary>
    /// Records what a sapling grows into, keeping the largest grow radius when several saplings grow the same thing.
    /// </summary>
    internal static void AddGrownFrom(Data d, string name, Entry e)
    {
        foreach (string grown in e.Grows ?? [])
        {
            if (!d.GrownFrom.TryGetValue(grown, out var g) || g.Radius < e.Gr)
            {
                d.GrownFrom[grown] = ((float)e.Gr, name);
            }
        }
    }
}
