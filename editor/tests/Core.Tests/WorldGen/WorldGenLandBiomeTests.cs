using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biome of a point on land, from its distance from the middle alone: near the middle it is Meadows, and far past every
/// biome's distance limit it is Black Forest.
/// </summary>
public class WorldGenLandBiomeTests
{
    /// <summary>
    /// A generator for a seed of the current generator version.
    /// </summary>
    private static WorldGenerator Generator() => WorldGenerator.Create(new World { Seed = 12345, WorldGenVersion = 2 });

    [Fact]
    public void NearTheMiddleOfTheWorldTheLandIsMeadows()
    {
        Assert.Equal(Heightmap.Biome.Meadows, Generator().LandBiome(0, 0, 100f, 0.1f, 0f));
    }

    [Fact]
    public void FarOutTheLandIsBlackForest()
    {
        Assert.Equal(Heightmap.Biome.BlackForest, Generator().LandBiome(0, 0, 20000f, 0.1f, 0f));
    }
}
