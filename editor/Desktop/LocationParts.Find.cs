using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public static partial class LocationParts
{
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
