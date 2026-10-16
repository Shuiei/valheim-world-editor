using System.Numerics;
using System.Runtime.CompilerServices;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The parts of the area's locations that are not saved objects: a crypt entrance's rocks, the trader's camp, a
/// boss's altar, a troll cave's walls, the rune tablets of ruins. The save keeps only a location proxy for each
/// location (its name hash, place, turn and seed); the game makes those parts again from the location's prefab
/// whenever the zone loads. The location's saved objects (stone pieces, chests, spawners) are objects of their own.
/// </summary>
/// <remarks>
/// A part the game keeps or drops by chance (RandomSpawn) is drawn when it is there at least half the time, as in
/// dungeon rooms (see GameBundles.LoadModel): the game draws that chance from the proxy's seed each time the zone
/// loads.
/// </remarks>
public static class LocationParts
{
    /// <summary>A location drawn: its location's name hash (its model's prefab), place and turn (Euler degrees).</summary>
    /// <param name="Location">The location's name hash.</param>
    /// <param name="Position">Where its proxy is, in world metres.</param>
    /// <param name="Rotation">Its proxy's turn, Euler degrees.</param>
    public readonly record struct Shown(int Location, Vector3 Position, Vector3 Rotation);

    /// <summary>The locations found for a scene, with the save, objects' edit version and proxy count they were found at.</summary>
    private sealed record Cache(WorldSave World, int Version, int Count, IReadOnlyList<Shown> Locations);

    /// <summary>What was found last, per scene.</summary>
    private static readonly ConditionalWeakTable<WorldScene, Cache> Caches = [];

    /// <summary>
    /// The area's locations whose parts are drawn: every location proxy in it, those added in this session too (a
    /// generated dungeon's entrance), but runestones (objects of the area already), the invisible ground discs of No
    /// limit ground and proxies deleted in this session.
    /// </summary>
    public static IReadOnlyList<Shown> Of(WorldScene s)
    {
        var world = s.World;
        // The objects' version: ground strokes leave the locations as they are.
        int version = s.Session?.Edits.ObjectsVersion ?? 0;
        int count;
        lock (world.Sync)
        {
            count = world.Placed.Count;
        }
        if (Caches.TryGetValue(s, out var c) && c.World == world && c.Version == version && c.Count == count)
        {
            return c.Locations;
        }
        var found = Find(s, world, s.Session?.Edits.Deleted ?? [], s.Session?.Edits.Added ?? []);
        Caches.AddOrUpdate(s, new Cache(world, version, count, found));
        return found;
    }

    /// <summary>The locations of <see cref="Of"/>, found in the save and among the session's added objects.</summary>
    /// <param name="s">The area.</param>
    /// <param name="world">Its save.</param>
    /// <param name="deleted">The saved objects deleted in this session (by id).</param>
    /// <param name="added">The objects added in this session: its location proxies (with their bytes) count.</param>
    internal static List<Shown> Find(WorldScene s, WorldSave world, ICollection<int> deleted, IEnumerable<NewObject> added)
    {
        float minX = s.OriginX, maxX = (s.X0 + s.ZonesX - 1) * 64f + 32f, minZ = s.OriginZ, maxZ = (s.Z0 + s.ZonesZ - 1) * 64f + 32f;
        bool Inside(Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
        var list = new List<Shown>();
        lock (world.Sync)
        {
            // A deleted proxy is known by its place (the save's list of locations has no ids).
            var gone = deleted.Where(id => id >= 0 && id < world.ObjectRefs.Count && world.ObjectRefs[id].Prefab == WorldSave.LocationProxyPrefab)
                .Select(id => world.ObjectRefs[id].Position).ToList();
            foreach (var p in world.Placed)
            {
                if (p.Prefab == WorldSave.LocationProxyPrefab && p.Location != 0 && !Uplift.IsDisc(p.Location) && !PrefabCatalog.IsRunestone(p.Location)
                    && Inside(p.Position) && !gone.Any(g => Vector3.DistanceSquared(g, p.Position) < 1e-4f))
                {
                    list.Add(new Shown(p.Location, p.Position, p.Rotation));
                }
            }
        }
        foreach (var n in added)
        {
            if (n.Prefab == WorldSave.LocationProxyPrefab && n.Raw != null && Inside(n.Position)
                && ZdoData.Parse(n.Raw).IntList.FirstOrDefault(i => i.Key == WorldSave.LocationKey).Value is int location and not 0
                && !Uplift.IsDisc(location) && !PrefabCatalog.IsRunestone(location))
            {
                list.Add(new Shown(location, n.Position, n.Rotation));
            }
        }
        return list;
    }
}
