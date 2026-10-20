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
    /// order, adding what passes the checks to <paramref name="spots"/>; the zone starts with its locations' clear areas.
    /// <paramref name="pass"/> holds the zones read so far, shared by every zone of one <see cref="Zones"/> call.
    /// </summary>
    private static void PlaceZone(Pass pass, int seed, int zx, int zz, List<Spot> spots)
    {
        Zone hmap = pass.ZoneAt(zx * 64f, zz * 64f);
        float cx = zx * 64f, cz = zz * 64f;
        pass.StartZone(zx, zz);
        foreach (Rule veg in pass.Rules)
        {
            if (veg.enable == 0 || !hmap.HaveBiome(veg.biome))
            {
                continue;
            }
            Rnd.InitState(seed + zx * 4271 + zz * 9187 + Save.StableHash.Of(veg.prefab));
            pass.Sums.Clear();
            if (TryRollGroupCount(veg, out int count))
            {
                PlaceRule(pass, veg, cx, cz, count, spots);
            }
        }
    }

    /// <summary>
    /// Adds one object at a world point when it passes every check of its rule (the surface, then the region, then
    /// Expand World Data's own distance check), with its rotation; the prefab drawn among the rule's when it has several
    /// (the mod's draw, before the object is made), then what grows there (<see cref="Grow"/>: the mod's scale by axis
    /// replaces the game's scaling, drawn only when the game would scale, the uniform draw not 1).
    /// </summary>
    private static bool TryPlaceSpot(Pass pass, Rule veg, float px, float pz, SpotDraw draw, List<Spot> spots)
    {
        if (!pass.Ground.Inside(px, pz))
        {
            return false;
        }
        float py = pass.Ground.Height(px, pz, out NVector3 normal);
        Zone hm = pass.ZoneAt(px, pz);
        if (!PassesSurfaceChecks(veg, pass.Ground, hm, px, py, pz, normal) || !PassesRegionChecks(pass.Terrain, veg, px, py, pz) || !PassesLastChecks(pass, veg, hm, new(px, py, pz)))
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
        pass.KeepClear(veg, px, pz);
        (string name, int draws) = veg.prefabs == null ? (veg.prefab, veg.draws) : DrawPrefab(veg);
        Grow(veg, name, draws, new NVector3(px, py, pz), euler, draw.Scale, spots);
        return true;
    }

    /// <summary>
    /// What grows at a spot that passed: a blueprint's parts when the prefab is the rule's blueprint, else the object
    /// with the game's draws for its components (as it wakes), then the mod's draws for a scale by axis.
    /// </summary>
    private static void Grow(Rule veg, string name, int draws, NVector3 at, NVector3 euler, float scale, List<Spot> spots)
    {
        if (veg.blueprints?.GetValueOrDefault(name) is { } parts)
        {
            PlaceBlueprint(veg, parts, at, BlueprintFormats.FromEuler(euler), spots);
            return;
        }
        // The components' draws come as the object wakes, before the mod scales it.
        for (int k = draws; k > 0; k--)
        {
            Rnd.NextUInt();
        }
        float[]? axes = veg.scaleMinAxes != null && veg.scaleMaxAxes != null && scale != 1f ? ScaleByAxes(veg) : null;
        spots.Add(new Spot(name, at.X, at.Y, at.Z, euler.X, euler.Y, euler.Z, axes?[0] ?? scale, axes, veg.data));
    }

    /// <summary>
    /// The mod's draw of one of a rule's several prefabs: its name and its object's own draws (none when unknown). The
    /// draw is the game's seeded generator's, so the pick is the mod's; nothing here is security-sensitive.
    /// </summary>
    private static (string Name, int Draws) DrawPrefab(Rule veg)
    {
        int which = Rnd.Range(0, veg.prefabs!.Length);
        return (veg.prefabs[which], veg.prefabDraws?[which] ?? 0);
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
