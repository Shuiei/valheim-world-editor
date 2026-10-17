using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Desktop;

public sealed partial class WorldScene
{
    /// <summary>The objects and building pieces of the block's zones (those not deleted).</summary>
    /// <param name="world">The save.</param>
    /// <param name="x0">The block's south-west zone, x.</param>
    /// <param name="z0">The block's south-west zone, z.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="deleted">Ids of deleted objects, left out.</param>
    /// <param name="added">Objects added in this session (other areas of the world may have placed or moved some here).</param>
    public static List<Thing> ReadThings(WorldSave world, int x0, int z0, int zonesX, int zonesZ, ICollection<int> deleted, IEnumerable<NewObject>? added = null)
    {
        lock (world.Sync)
        {
            return ReadThingsIn(world, x0, z0, zonesX, zonesZ, deleted, added);
        }
    }

    /// <summary><c>ReadThings</c>' work, with the world's lock held.</summary>
    private static List<Thing> ReadThingsIn(WorldSave world, int x0, int z0, int zonesX, int zonesZ, ICollection<int> deleted, IEnumerable<NewObject>? added)
    {
        float minX = x0 * 64f - 32f, maxX = (x0 + zonesX - 1) * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = (z0 + zonesZ - 1) * 64f + 32f;
        bool Inside(Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
        var things = new List<Thing>();
        List<NewObject> addedNow = [.. added ?? []];
        var links = LinkIndex.Of(world, deleted, addedNow);
        foreach (var (id, prefab, p, r, sc) in world.Objects)
        {
            if (Inside(p) && !deleted.Contains(id))
            {
                things.Add(Placed(new Thing(id, prefab, p, r, sc.X, false) { Tamed = world.Tamed.Contains(id) }, links));
            }
        }
        foreach (var (id, prefab, p, ry) in world.Pieces)
        {
            if (Inside(p) && !deleted.Contains(id))
            {
                things.Add(Placed(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true), links));
            }
        }
        // Not the ground discs for No limit ground (invisible: they are just the ground).
        foreach (var n in addedNow)
        {
            if (Inside(n.Position) && n.Prefab != WorldSave.LocationProxyPrefab)
            {
                things.Add(Placed(new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Tamed = TamedOf(world, n) }, links));
            }
        }
        // The ruins' pieces joined to a building (the world's and this session's built pieces), found over the whole
        // world: the building may be just outside the area.
        var joined = JoinedRuins.InWorld(world, deleted, addedNow.Where(n => PieceCatalog.Get(n.Prefab)?.Tool != null).Select(n => n.Position));
        for (int i = 0; i < things.Count; i++)
        {
            if (!things[i].Piece && joined.Contains(things[i].Id))
            {
                things[i] = things[i] with { Joined = true };
            }
        }
        return things;
    }
}
