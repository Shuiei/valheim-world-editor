using ValheimGen;

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
