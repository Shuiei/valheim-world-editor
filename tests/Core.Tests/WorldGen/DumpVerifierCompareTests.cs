using ValheimGen;
using Xunit;

using static WorldEditor.Tests.DumpVerifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// One replayed line compared with the offline generator, by its kind: Perlin samples, lake and river counts, a
/// point's biome and height, and a zone's base heights.
/// </summary>
public class DumpVerifierCompareTests
{
    [Fact]
    public void APerlinSampleThatMatchesIsOk()
    {
        var t = new DumpVerifier.Tally();
        float x = 0.5f, y = 0.25f;
        string line = $"P {Hex(x)} {Hex(y)} {Hex(UnityPerlin.Noise(x, y))}";

        DumpVerifier.Compare(Split(line), line, t, "unused");

        Assert.Equal(1, t.PerlinOk);
        Assert.Equal(0, t.PerlinWrong);
    }

    [Fact]
    public void APerlinSampleThatDiffersIsWrongWithItsDifference()
    {
        var t = new DumpVerifier.Tally();
        float x = 0.5f, y = 0.25f;
        string line = $"P {Hex(x)} {Hex(y)} {Hex(2f)}";

        DumpVerifier.Compare(Split(line), line, t, "unused");

        Assert.Equal(1, t.PerlinWrong);
        Assert.True(t.PerlinMax > 0);
    }

    [Fact]
    public void AnUnknownKindChangesNothing()
    {
        var t = new DumpVerifier.Tally();

        DumpVerifier.Compare(Split("Q 1 2"), "Q 1 2", t, "unused");

        Assert.Equal(0, t.PerlinOk + t.PerlinWrong + t.RandomOk + t.RandomWrong + t.HeightsOk + t.ZonesOk + t.WaterWrong);
        Assert.Empty(t.Errors);
    }

    [Fact]
    public void AWaterCountThatMatchesTheGeneratorIsNotWrong()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };
        string line = $"W lakes {gen.GetLakes().Count} rivers {gen.GetRivers().Count} streams {gen.GetStreams().Count}";

        DumpVerifier.CompareWater(line, t, "unused");

        Assert.Equal(0, t.WaterWrong);
    }

    [Fact]
    public void AWaterCountThatDiffersIsWrong()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };

        DumpVerifier.CompareWater("W lakes -1 rivers -1 streams -1", t, "unused");

        Assert.Equal(1, t.WaterWrong);
        Assert.Contains("Water: expected", Assert.Single(t.Errors));
    }

    [Fact]
    public void APointHeightAndBiomeThatMatchTheGeneratorAreOk()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };
        float x = 10f, z = 20f;
        var biome = gen.GetBiome(x, z);
        float h = gen.GetBiomeHeight(biome, x, z, out _);

        DumpVerifier.CompareHeight(Split($"H {Hex(x)} {Hex(z)} {(int)biome} {Hex(h)}"), t, "unused");

        Assert.Equal(1, t.HeightsOk);
        Assert.Equal(0, t.BiomesWrong);
    }

    [Fact]
    public void AWrongBiomeIsCountedAsABiomeMismatch()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };
        float x = 10f, z = 20f;
        var biome = gen.GetBiome(x, z);
        float h = gen.GetBiomeHeight(biome, x, z, out _);
        int wrong = (int)biome + 1;

        DumpVerifier.CompareHeight(Split($"H {Hex(x)} {Hex(z)} {wrong} {Hex(h)}"), t, "unused");

        Assert.Equal(1, t.BiomesWrong);
        Assert.Equal(1, t.HeightsOk);
    }

    [Fact]
    public void AZoneWhoseHeightsMatchIsExact()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };
        float[] heights = BaseTerrain.BuildZone(gen, 3, -2);
        string line = "Z 3 -2 " + string.Join(" ", heights.Select(Hex));

        DumpVerifier.CompareZone(Split(line), t, "unused");

        Assert.Equal(1, t.ZonesOk);
        Assert.Equal(0, t.ZonesWrong);
    }

    [Fact]
    public void AZoneWithOneHeightOffIsWrongWithTheDifference()
    {
        var gen = SeedLocationsSupport.Seed();
        var t = new DumpVerifier.Tally { Generator = gen };
        float[] heights = BaseTerrain.BuildZone(gen, 3, -2);
        heights[0] += 1000f;
        string line = "Z 3 -2 " + string.Join(" ", heights.Select(Hex));

        DumpVerifier.CompareZone(Split(line), t, "unused");

        Assert.Equal(1, t.ZonesWrong);
        Assert.True(t.ZoneMax > 0);
        Assert.Contains("1 of", Assert.Single(t.Errors));
    }
}
