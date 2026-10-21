using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>What a contents search matches in one object: the containers' items it holds, and the texts it has.</summary>
internal static class WorldSearchMatch
{
    /// <summary>Adds the container's items that match, grouped by item name, with their stack sizes added up.</summary>
    /// <param name="tally">Where the hits go.</param>
    /// <param name="id">The object's id.</param>
    /// <param name="name">The object's display name.</param>
    /// <param name="z">The object.</param>
    internal static void Containers(WorldSearchTally tally, int id, string name, ZdoData z)
    {
        if (z.GetBytes(ObjectData.ItemsKey) is not byte[] items)
        {
            return;
        }
        InventoryData inv;
        try
        {
            inv = InventoryData.Read(items);
        }
        catch (Exception ex) when (CheckedReads.Damaged(ex))
        {
            Log.CaughtBrief(ex, "reading a container's items while searching the world (damaged: passed over)");
            return;
        }
        foreach (var g in inv.Items.GroupBy(i => PrefabCatalog.NameOf(i.Prefab) ?? "?").Where(g => tally.Matches(g.Key)))
        {
            int stack = g.Sum(i => i.Stack);
            tally.Found(id, name, z.Position, $"{g.Key} ×{stack}", g.Key, stack);
        }
    }

    /// <summary>Adds the object once when one of its texts matches: the first matching text is what is shown.</summary>
    /// <param name="tally">Where the hits go.</param>
    /// <param name="id">The object's id.</param>
    /// <param name="name">The object's display name.</param>
    /// <param name="z">The object.</param>
    internal static void Texts(WorldSearchTally tally, int id, string name, ZdoData z)
    {
        foreach (var (key, value) in z.StringList)
        {
            if (value.Length > 0 && tally.Matches(value))
            {
                tally.Found(id, name, z.Position, $"{ZdoKeys.NameOf(key) ?? "text"}: {value}", name);
                break;
            }
        }
    }
}
