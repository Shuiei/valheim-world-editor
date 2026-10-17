using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The stats of a biome map (SeedMeasure) on small hand-made maps: the land share, the main landmass, the share per
/// biome and the start's landmass.
/// </summary>
public class SeedMeasureTests
{
    private static byte Meadows => (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Meadows);

    private static float CellOf(int size) => 2 * SeedPreview.Radius / size;

    [Fact]
    public void ASeaOnlyMapHasNoLand()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.Equal(0f, s.Land);
    }

    [Fact]
    public void AllLandMapHasFullLandShare()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, Meadows);
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.Equal(1f, s.Land);
    }

    [Fact]
    public void LandShareCountsTheLandCellsInsideTheWorld()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[1] = Meadows;
        biome[8] = Meadows;
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.InRange(s.Land, 3f / 9f - 0.001f, 3f / 9f + 0.001f);
    }

    [Fact]
    public void MainContinentIsTheLargestLandmassShare()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[1] = Meadows;
        biome[8] = Meadows;
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.InRange(s.MainContinent, 2f / 3f - 0.001f, 2f / 3f + 0.001f);
    }

    [Fact]
    public void SharesSplitTheLandByBiome()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[1] = Meadows;
        biome[8] = (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Plains);
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.InRange(s.Shares[Heightmap.Biome.Meadows], 2f / 3f - 0.001f, 2f / 3f + 0.001f);
        Assert.InRange(s.Shares[Heightmap.Biome.Plains], 1f / 3f - 0.001f, 1f / 3f + 0.001f);
    }

    [Fact]
    public void StartContinentIsTheShareOfTheStartsLandmass()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[1] = Meadows;
        biome[8] = (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Plains);
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.InRange(s.StartContinent, 2f / 3f - 0.001f, 2f / 3f + 0.001f);
    }

    [Fact]
    public void BiomesNotOnTheMapAreAtAnInfiniteDistance()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, Meadows);
        var s = SeedMeasure.Measure(biome, 3, CellOf(3));
        Assert.True(float.IsPositiveInfinity(s.Distance[Heightmap.Biome.Swamp]));
    }
}
