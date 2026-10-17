using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A spot's ground checks for a location kind: its distance from the middle, its biome, its height and forest, and the
/// rise of the ground across its size.
/// </summary>
public class SeedLocationsPlacerGroundTests
{
    /// <summary>A placer for a seed; the biome map is not needed for these checks.</summary>
    private static SeedLocations.Placer Placer(WorldGenerator gen) => new(gen, () => throw new InvalidOperationException("no biome map needed"));

    [Fact]
    public void ASpotCloserThanTheMinimumIsOutOfTheRing()
    {
        var rule = new SeedLocations.Rule { MinDistance = 100 };

        Assert.False(SeedLocations.Placer.AtRightDistance(rule, new Vector3(50, 0, 0)));
        Assert.True(SeedLocations.Placer.AtRightDistance(rule, new Vector3(150, 0, 0)));
    }

    [Fact]
    public void ASpotFartherThanTheMaximumIsOutOfTheRing()
    {
        var rule = new SeedLocations.Rule { MaxDistance = 200 };

        Assert.False(SeedLocations.Placer.AtRightDistance(rule, new Vector3(250, 0, 0)));
        Assert.True(SeedLocations.Placer.AtRightDistance(rule, new Vector3(150, 0, 0)));
    }

    [Fact]
    public void ASpotOnTheRightBiomeAndAltitudeIsOnTheGroundAtItsHeight()
    {
        var gen = Seed();
        var placer = Placer(gen);
        var spot = new Vector3(100, 0, 100);
        var biome = gen.GetBiome(spot);
        var rule = new SeedLocations.Rule { MinAltitude = -10000, MaxAltitude = 10000 };

        bool ok = placer.OnRightGround(rule, biome, ref spot, out _);

        Assert.True(ok);
        Assert.Equal(gen.GetBiomeHeight(biome, 100, 100, out _), spot.Y);
    }

    [Fact]
    public void ASpotInNoBiomeOfTheKindIsRefused()
    {
        var gen = Seed();
        var spot = new Vector3(100, 0, 100);
        var rule = new SeedLocations.Rule { MinAltitude = -10000, MaxAltitude = 10000 };

        Assert.False(Placer(gen).OnRightGround(rule, 0, ref spot, out _));
    }

    [Fact]
    public void ASpotAboveTheKindsAltitudeIsRefused()
    {
        var gen = Seed();
        var spot = new Vector3(100, 0, 100);
        var biome = gen.GetBiome(spot);
        var rule = new SeedLocations.Rule { MinAltitude = -10000, MaxAltitude = -10000 };

        Assert.False(Placer(gen).OnRightGround(rule, biome, ref spot, out _));
    }

    [Fact]
    public void AFlatEnoughSpotIsWithinTheRiseTheKindAllows()
    {
        var gen = Seed();
        var rule = new SeedLocations.Rule { ExteriorRadius = 10, MinTerrainDelta = 0, MaxTerrainDelta = 1000 };

        Assert.True(Placer(gen).FlatEnough(rule, new Vector3(100, 0, 100)));
    }

    [Fact]
    public void ASpotWithTooLittleRiseIsRefused()
    {
        var gen = Seed();
        var rule = new SeedLocations.Rule { ExteriorRadius = 10, MinTerrainDelta = 1000, MaxTerrainDelta = 2000 };

        Assert.False(Placer(gen).FlatEnough(rule, new Vector3(100, 0, 100)));
    }
}
