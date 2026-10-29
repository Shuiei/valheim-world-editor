using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>What each zone of a save holds: its objects and, of those, its player-built pieces, counted with the session's edits.</summary>
internal static class ZoneCensus
{
    /// <summary>Counts the objects and the pieces per zone: the save's (deleted, vanished and terrain left out), then the objects added in the session.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (deleted and added objects).</param>
    /// <returns>Per zone, the pieces; and per zone, the objects.</returns>
    internal static (Dictionary<(int, int), int> Pieces, Dictionary<(int, int), int> Objects) Count(WorldSave world, EditStore edits)
    {
        HashSet<int> deleted = edits.Deleted;
        Dictionary<(int, int), int> pieces = [], objects = [];
        for (int id = 0; id < world.ObjectRefs.Count; id++)
        {
            ObjectRef o = world.ObjectRefs[id];
            if (deleted.Contains(id) || world.Vanished.Contains(id) || o.IsTerrain)
            {
                continue;
            }
            var key = o.Zone;
            objects[key] = objects.GetValueOrDefault(key) + 1;
            if (o.IsPiece)
            {
                pieces[key] = pieces.GetValueOrDefault(key) + 1;
            }
        }
        // Objects added in this session count as the save will hold them.
        foreach (NewObject n in edits.Added)
        {
            var key = ZoneOf(n.Position);
            objects[key] = objects.GetValueOrDefault(key) + 1;
            if (IsBuilt(world, n))
            {
                pieces[key] = pieces.GetValueOrDefault(key) + 1;
            }
        }
        return (pieces, objects);
    }

    /// <summary>The zone a position lies in (as ObjectRef.Zone).</summary>
    private static (int X, int Z) ZoneOf(System.Numerics.Vector3 p) => ChunkMath.SectorOf(p.X, p.Z);

    /// <summary>
    /// Whether an added object will be a player-built piece (have a builder) once saved: a builder given in its data
    /// decides; a new one is when players build its kind (WorldSave.NewObjectBytes gives it the builder); a moved or
    /// edited one keeps its source's.
    /// </summary>
    private static bool IsBuilt(WorldSave world, NewObject n)
    {
        if (n.Data?.LastOrDefault(f => f.Section == "longs" && f.Key == ObjectField.CreatorKey) is { } given)
        {
            return given.Value is { } v && v != "0";
        }
        if (!n.Fresh && n.SourceId is int s && s >= 0 && s < world.ObjectRefs.Count)
        {
            return world.ObjectRefs[s].IsPiece;
        }
        return Terrain.PieceCatalog.Get(n.Prefab)?.Tool != null;
    }
}
