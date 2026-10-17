using ValheimGen;
using NVector3 = System.Numerics.Vector3;

namespace TerrainEditor.App;

/// <summary>
/// The checks a spot must pass on the surface it stands on: biome, altitude, vegetation mask and ocean depth.
/// </summary>
public static partial class Regrow
{
    /// <summary>
    /// Whether the spot passes every surface check of its rule, in the game's order: the ground's slope and terrain
    /// delta (Regrow.Ground.cs) come after these, as the terrain delta draws random samples.
    /// </summary>
    private static bool PassesSurfaceChecks(Rule veg, Ground ground, Zone hm, float px, float py, float pz, NVector3 normal) =>
        PassesBiome(veg, hm, px, pz) && PassesAltitude(veg, py) && PassesVegetationMask(veg, hm, px, pz)
        && PassesOceanDepth(veg, hm, px, pz) && PassesSlope(veg, normal) && PassesTerrainDelta(veg, ground, px, pz);

    /// <summary>Whether the spot's biome and its place in that biome (edge or middle) are allowed by the rule.</summary>
    private static bool PassesBiome(Rule veg, Zone hm, float px, float pz) =>
        (veg.biome & hm.Biome(px, pz)) != 0 && (veg.biomeArea & hm.Area) != 0;

    /// <summary>Whether the spot's height above the water is within the rule's altitude range.</summary>
    private static bool PassesAltitude(Rule veg, float py)
    {
        float altitude = py - TerrainService.WaterLevel;
        return !(altitude < veg.minAltitude || altitude > veg.maxAltitude);
    }

    /// <summary>Whether the paint mask under the spot is within the rule's range (no check when the range is empty).</summary>
    private static bool PassesVegetationMask(Rule veg, Zone hm, float px, float pz)
    {
        if (veg.minVegetation.Equals(veg.maxVegetation))
        {
            return true;
        }
        float m = hm.VegetationMask(px, pz);
        return !(m > veg.maxVegetation || m < veg.minVegetation);
    }

    /// <summary>Whether the ocean depth at the spot is within the rule's range (no check when the range is empty).</summary>
    private static bool PassesOceanDepth(Rule veg, Zone hm, float px, float pz)
    {
        if (veg.minOceanDepth.Equals(veg.maxOceanDepth))
        {
            return true;
        }
        float d = hm.Ocean(px, pz);
        return !(d < veg.minOceanDepth || d > veg.maxOceanDepth);
    }
}
