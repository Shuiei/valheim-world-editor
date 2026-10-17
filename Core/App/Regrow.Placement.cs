using ValheimGen;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.App;

/// <summary>
/// Placing vegetation the way the game does in one zone: each rule, its groups and the checks a spot must pass.
/// </summary>
public static partial class Regrow
{
    /// <summary>The random draws that give one spot its rotation and scale.</summary>
    /// <param name="Rx">Random tilt about x (degrees).</param>
    /// <param name="Ry">Random turn about y (degrees).</param>
    /// <param name="Rz">Random tilt about z (degrees).</param>
    /// <param name="Scale">Uniform scale.</param>
    private readonly record struct SpotDraw(float Rx, float Ry, float Rz, float Scale);

    /// <summary>
    /// ZoneSystem.PlaceVegetation for one zone: every enabled rule of its biomes, the same draws in the same
    /// order, adding what passes the checks to <paramref name="spots"/>. <paramref name="zones"/> holds the zones
    /// read so far, shared by every zone of one <see cref="Zones"/> call.
    /// </summary>
    private static void PlaceZone(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, int seed, int zx, int zz, List<Spot> spots)
    {
        Zone hmap = ZoneAt(terrain, ground, zones, zx * 64f, zz * 64f);
        float cx = zx * 64f, cz = zz * 64f;
        foreach (Rule veg in _rules)
        {
            if (veg.enable == 0 || !hmap.HaveBiome(veg.biome))
            {
                continue;
            }
            Rnd.InitState(seed + zx * 4271 + zz * 9187 + Save.StableHash.Of(veg.prefab));
            if (TryRollGroupCount(veg, out int count))
            {
                PlaceRule(terrain, ground, zones, veg, cx, cz, count, spots);
            }
        }
    }

    /// <summary>
    /// Adds one object at a world point when it passes every check of its rule (the surface, then the region),
    /// with its rotation, then makes the game's draws for the object's components.
    /// </summary>
    private static bool TryPlaceSpot(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, Rule veg, float px, float pz, SpotDraw draw, List<Spot> spots)
    {
        if (!ground.Inside(px, pz))
        {
            return false;
        }
        float py = ground.Height(px, pz, out NVector3 normal);
        Zone hm = ZoneAt(terrain, ground, zones, px, pz);
        if (!PassesSurfaceChecks(veg, ground, hm, px, py, pz, normal) || !PassesRegionChecks(veg, px, py, pz))
        {
            return false;
        }
        if (veg.snapToWater != 0)
        {
            py = TerrainService.WaterLevel;
        }
        py += veg.groundOffset;
        NVector3 euler = SpotRotation(veg, normal, draw);
        spots.Add(new Spot(veg.prefab, px, py, pz, euler.X, euler.Y, euler.Z, draw.Scale));
        for (int k = veg.draws; k > 0; k--)
        {
            Rnd.NextUInt();
        }
        return true;
    }
}
