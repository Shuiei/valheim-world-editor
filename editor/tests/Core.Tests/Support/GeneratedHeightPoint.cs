using ValheimGen;

namespace WorldEditor.Tests;

/// <summary>
/// The seed's generator, as the height lines of a dump are checked against it: a tally that holds it, and the biome and
/// height it gives a point.
/// </summary>
public static class GeneratedHeightPoint
{
    /// <summary>
    /// A dump tally whose generator is the seed 5DCcdIcuYJ's.
    /// </summary>
    internal static DumpVerifier.Tally SeedTally() => new() { Generator = SeedLocationsSupport.Seed() };

    /// <summary>
    /// The biome (as its number) and the height of the generator at x, z.
    /// </summary>
    public static (int Biome, float Height) At(WorldGenerator gen, float x, float z)
    {
        var biome = gen.GetBiome(x, z);
        float h = gen.GetBiomeHeight(biome, x, z, out _);
        return ((int)biome, h);
    }
}
