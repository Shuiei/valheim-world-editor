using ValheimGen;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;
using Rule = ValheimGen.VegetationRule;

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
    private static void PlaceZone(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, IReadOnlyList<Rule> rules, int seed, int zx, int zz, List<Spot> spots)
    {
        Zone hmap = ZoneAt(terrain, ground, zones, zx * 64f, zz * 64f);
        float cx = zx * 64f, cz = zz * 64f;
        foreach (Rule veg in rules)
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
    /// Adds one object at a world point when it passes every check of its rule (the surface, then the region, then
    /// Expand World Data's own distance check), with its rotation; the prefab drawn among the rule's when it has several
    /// (the mod's draw, before the object is made), then the game's draws for the object's components, then the mod's
    /// draws for a scale by axis (its replacement of the game's scaling, made only when the game would scale: the
    /// uniform draw is not 1).
    /// </summary>
    private static bool TryPlaceSpot(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, Rule veg, float px, float pz, SpotDraw draw, List<Spot> spots)
    {
        if (!ground.Inside(px, pz))
        {
            return false;
        }
        float py = ground.Height(px, pz, out NVector3 normal);
        Zone hm = ZoneAt(terrain, ground, zones, px, pz);
        if (!PassesSurfaceChecks(veg, ground, hm, px, py, pz, normal) || !PassesRegionChecks(terrain, veg, px, py, pz) || !PassesOwnDistance(veg, px, py, pz))
        {
            return false;
        }
        if (veg.snapToWater != 0)
        {
            // The game's literal 30 (ZoneSystem.PlaceVegetation), whatever the rules put the water at.
            py = WorldGenerator.LiteralWaterLevel;
        }
        py += veg.groundOffset;
        NVector3 euler = SpotRotation(veg, normal, draw);
        int which = veg.prefabs == null ? -1 : Rnd.Range(0, veg.prefabs.Length);
        string name = which < 0 ? veg.prefab : veg.prefabs![which];
        int draws = which < 0 ? veg.draws : veg.prefabDraws?[which] ?? 0;
        // The components' draws come as the object wakes, before the mod scales it.
        for (int k = draws; k > 0; k--)
        {
            Rnd.NextUInt();
        }
        float[]? axes = veg.scaleMinAxes != null && veg.scaleMaxAxes != null && draw.Scale != 1f ? ScaleByAxes(veg) : null;
        spots.Add(new Spot(name, px, py, pz, euler.X, euler.Y, euler.Z, axes?[0] ?? draw.Scale, axes));
        return true;
    }

    /// <summary>
    /// The mod's scale by axis (its RandomValue): one draw of 0..1 for all three axes when uniform, else one draw an
    /// axis, each between the rule's smallest and largest for that axis (x, y, z).
    /// </summary>
    private static float[] ScaleByAxes(Rule veg)
    {
        float[] min = veg.scaleMinAxes!, max = veg.scaleMaxAxes!;
        if (veg.scaleUniform != 0)
        {
            float t = Rnd.Range(0f, 1f);
            return [Lerp3(min[0], max[0], t), Lerp3(min[1], max[1], t), Lerp3(min[2], max[2], t)];
        }
        return [Rnd.Range(min[0], max[0]), Rnd.Range(min[1], max[1]), Rnd.Range(min[2], max[2])];
    }

    /// <summary>min + (max - min) * t, as the mod computes it (in double, rounded once).</summary>
    private static float Lerp3(float min, float max, float t) => (float)((double)min + ((double)max - min) * t);
}
