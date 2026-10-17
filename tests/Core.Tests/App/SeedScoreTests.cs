using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How a seed's preview scores against the wishes (SeedPreview.Score): each criterion adds or takes away on its own, and
/// the score is never below zero.
/// </summary>
public class SeedScoreTests
{
    private static SeedPreview.Stats Stats(float land = 0.5f, float startContinent = 0f, float landNearStart = 0f, IReadOnlyDictionary<Heightmap.Biome, float>? distance = null) =>
        new(land, SeedPreview.Biomes.ToDictionary(b => b, _ => 0f), 0, (0f, 0f), landNearStart, startContinent,
            distance ?? SeedPreview.Biomes.ToDictionary(b => b, _ => float.PositiveInfinity),
            SeedPreview.Biomes.ToDictionary(b => b, _ => false));

    [Fact]
    public void NoWishesScoreZero()
    {
        var wishes = new SeedPreview.Wishes(BigStartLand: false, LandAroundStart: false);
        Assert.Equal(0f, SeedPreview.Score(Stats(startContinent: 1f, landNearStart: 1f), wishes));
    }

    [Fact]
    public void ABigStartLandAddsThreeTimesTheStartContinent()
    {
        var wishes = new SeedPreview.Wishes(LandAroundStart: false);
        Assert.Equal(1.5f, SeedPreview.Score(Stats(startContinent: 0.5f), wishes));
    }

    [Fact]
    public void LandAroundTheStartAddsTwiceItsShare()
    {
        var wishes = new SeedPreview.Wishes(BigStartLand: false);
        Assert.Equal(0.5f, SeedPreview.Score(Stats(landNearStart: 0.25f), wishes));
    }

    [Fact]
    public void ASwampNearerThanTheWishScoresMoreThanOneAtTheWishDistance()
    {
        var distance = SeedPreview.Biomes.ToDictionary(b => b, _ => float.PositiveInfinity);
        distance[Heightmap.Biome.Swamp] = 50f;
        var wishes = new SeedPreview.Wishes(BigStartLand: false, LandAroundStart: false, MaxSwampDistance: 100f);
        Assert.Equal(1.5f, SeedPreview.Score(Stats(distance: distance), wishes));
    }

    [Fact]
    public void AMissingBiomeWithinTheWishCostsTwo()
    {
        var wishes = new SeedPreview.Wishes(LandAroundStart: false, MaxSwampDistance: 100f);
        Assert.Equal(1f, SeedPreview.Score(Stats(startContinent: 1f), wishes));
    }

    [Fact]
    public void LandBelowTheMinimumLowersTheScore()
    {
        var wishes = new SeedPreview.Wishes(LandAroundStart: false, MinLand: 0.8f);
        Assert.InRange(SeedPreview.Score(Stats(land: 0.5f, startContinent: 1f), wishes), 1.49f, 1.51f);
    }

    [Fact]
    public void LandAboveTheMaximumLowersTheScore()
    {
        var wishes = new SeedPreview.Wishes(LandAroundStart: false, MaxLand: 0.5f);
        Assert.InRange(SeedPreview.Score(Stats(land: 0.9f, startContinent: 1f), wishes), 0.99f, 1.01f);
    }

    [Fact]
    public void TheScoreIsNeverBelowZero()
    {
        var wishes = new SeedPreview.Wishes(BigStartLand: false, LandAroundStart: false, MinLand: 0.5f);
        Assert.Equal(0f, SeedPreview.Score(Stats(land: 0f), wishes));
    }
}
