using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>The kind search: the world's objects (and those added in the session) whose display name matches the query.</summary>
internal static class WorldSearchKinds
{
    /// <summary>Adds every object whose name matches to the tally, deleted and vanished ones left out.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (added and deleted objects).</param>
    /// <param name="tally">Where the hits go.</param>
    internal static void Run(WorldSave world, EditStore edits, WorldSearchTally tally)
    {
        HashSet<int> deleted = edits.Deleted;
        for (int id = 0; id < world.ObjectRefs.Count; id++)
        {
            ObjectRef o = world.ObjectRefs[id];
            if (deleted.Contains(id) || world.Vanished.Contains(id) || o.IsTerrain || o.Prefab == WorldSave.LocationProxyPrefab)
            {
                continue;
            }
            string? name = PrefabCatalog.DisplayName(o.Prefab);
            if (name != null && tally.Shown(name) && tally.Matches(name))
            {
                tally.Found(id, name, o.Position, null, name);
            }
        }
        foreach (NewObject n in edits.Added)
        {
            string? name = PrefabCatalog.DisplayName(n.Prefab);
            if (name != null && tally.Shown(name) && tally.Matches(name))
            {
                tally.Found(n.Id, name, n.Position, null, name);
            }
        }
    }
}
