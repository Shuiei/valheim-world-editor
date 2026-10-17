using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A biome's points listed once: the flood fill (for land biomes) or the rows (for the ocean and the Ashlands), and the
/// points at or above sea level kept among them.
/// </summary>
public class SeedLocationsBiomeMapListTests
{
    [Fact]
    public void ALandBiomeIsListedOnceAndKeepsItsOrder()
    {
        var map = new SeedLocations.BiomeMap(Seed(), true, TestContext.Current.CancellationToken);
        int meadows = SeedLocations.BiomeMap.Index(Heightmap.Biome.Meadows);

        map.List(meadows);
        int[] first = map.All[meadows];

        Assert.NotNull(first);
        Assert.NotEmpty(first);
        Assert.True(map.AboveSea[meadows].Length <= first.Length);
        map.List(meadows);
        Assert.Same(first, map.All[meadows]);
    }

    [Fact]
    public void TheOceanIsListedRowByRow()
    {
        var map = new SeedLocations.BiomeMap(Seed(), true, TestContext.Current.CancellationToken);
        int ocean = SeedLocations.BiomeMap.Index(Heightmap.Biome.Ocean);

        map.List(ocean);

        Assert.NotNull(map.All[ocean]);
        Assert.NotNull(map.AboveSea[ocean]);
    }
}
