using ValheimGen;

namespace WorldEditor.Tests;

/// <summary>Alt biome rules for the tests.</summary>
public static class AltBiomeRules
{
    /// <summary>An alt biome that every area of every biome may have, given to as many as there are.</summary>
    public static SeedLocations.AltBiomeRule Everywhere(string name) => new()
    {
        Name = name,
        Biome = -1,
        MinDistanceFromCenter = 0f,
        MinAmountSpawned = 100000,
        MaxAmountSpawned = 100000,
        Chance = 1f,
        MinEdgeSize = 0,
        MaxEdgeSize = int.MaxValue,
        MinAvgHeight = -1e9f,
        MaxAvgHeight = 1e9f,
    };
}
