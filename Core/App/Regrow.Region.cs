using ValheimGen;

using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

/// <summary>
/// The checks a spot must pass for where it stands in the world: its distance from the world's middle and the
/// forest factor there.
/// </summary>
public static partial class Regrow
{
    /// <summary>Whether the spot passes the region checks of its rule (distance from the middle, then forest).</summary>
    private static bool PassesRegionChecks(TerrainService terrain, Rule veg, float px, float py, float pz) =>
        PassesCenterDistance(veg, px, pz) && PassesForest(terrain, veg, px, py, pz);

    /// <summary>Whether the spot is far enough from, and not too far from, the world's middle (0: no limit).</summary>
    private static bool PassesCenterDistance(Rule veg, float px, float pz)
    {
        if (!(veg.minDistanceFromCenter > 0f || veg.maxDistanceFromCenter > 0f))
        {
            return true;
        }
        float dist = MathF.Sqrt(px * px + pz * pz);
        return !((veg.minDistanceFromCenter > 0f && dist < veg.minDistanceFromCenter) || (veg.maxDistanceFromCenter > 0f && dist > veg.maxDistanceFromCenter));
    }

    /// <summary>
    /// Expand World Data's own distance check, for a rule with a centre of its own (its InsideClearArea prefix, so after
    /// the forest check, with the spot at the ground's height): the distance from the centre within the rule's range
    /// (the most 0: no upper limit). Measured as the mod measures it: in three dimensions, the centre's second number
    /// taken for the height (the mod turns its (x, y) centre into a (x, y, 0) point).
    /// </summary>
    private static bool PassesOwnDistance(Rule veg, float px, float py, float pz)
    {
        if (veg.center == null || veg.distance == null)
        {
            return true;
        }
        float dx = px - veg.center[0], dy = py - veg.center[1], dz = pz;
        float d = (float)Math.Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz);
        float least = veg.distance[0], most = veg.distance[1];
        return most == 0f ? least <= d : least <= d && d <= most;
    }

    /// <summary>Whether the forest factor at the spot is within the rule's thresholds (only when the rule asks for a forest).</summary>
    private static bool PassesForest(TerrainService terrain, Rule veg, float px, float py, float pz)
    {
        if (veg.inForest == 0)
        {
            return true;
        }
        float forest = terrain.Generator.GetForestFactor(new Vector3(px, py, pz));
        return !(forest < veg.forestTresholdMin || forest > veg.forestTresholdMax);
    }
}
