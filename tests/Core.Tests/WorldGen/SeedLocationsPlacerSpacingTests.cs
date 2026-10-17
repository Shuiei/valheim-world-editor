using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A spot's checks against the kind's other spots and the ground around it: distance from its own kind, the vegetation
/// bounds, and the greenest-spot check that the first nine spots only set the bar for.
/// </summary>
public class SeedLocationsPlacerSpacingTests
{
    /// <summary>A placer for a seed; the biome map is not needed for these checks.</summary>
    private static SeedLocations.Placer Placer(WorldGenerator gen) => new(gen, () => throw new InvalidOperationException("no biome map needed"));

    [Fact]
    public void ASpotTooCloseToAnotherOfItsKindIsRefused()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { prefab = "Temple", minDistanceFromSimilar = 50 };
        placer.Register(rule, new Vector3(0, 0, 0));

        Assert.False(placer.RightNeighbours(rule, new Vector3(10, 0, 0)));
    }

    [Fact]
    public void ASpotFarEnoughFromItsKindIsKept()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { prefab = "Temple", minDistanceFromSimilar = 50 };
        placer.Register(rule, new Vector3(0, 0, 0));

        Assert.True(placer.RightNeighbours(rule, new Vector3(200, 0, 0)));
    }

    [Fact]
    public void ASpotNeedsAnotherOfItsKindWithinTheMaximumWhenOneIsSet()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { prefab = "Temple", maxDistanceFromSimilar = 50 };

        Assert.False(placer.RightNeighbours(rule, new Vector3(200, 0, 0)));
        placer.Register(rule, new Vector3(190, 0, 0));
        Assert.True(placer.RightNeighbours(rule, new Vector3(200, 0, 0)));
    }

    [Fact]
    public void VegetationMustBeAboveTheMinimumAndBelowTheMaximum()
    {
        var rule = new SeedLocations.Rule { minimumVegetation = 0.3f, maximumVegetation = 0.8f };

        Assert.False(SeedLocations.Placer.RightVegetation(rule, 0.2f));
        Assert.True(SeedLocations.Placer.RightVegetation(rule, 0.5f));
        Assert.False(SeedLocations.Placer.RightVegetation(rule, 0.9f));
    }

    [Fact]
    public void AKindWithNoGreenestCheckAlwaysStandsOut()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { surroundCheckVegetation = 0 };

        Assert.True(placer.StandsOut(rule, new Vector3(100, 0, 100)));
    }

    [Fact]
    public void TheFirstNineSpotsOnlySetTheBarForTheGreenestCheck()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { surroundCheckVegetation = 1, surroundCheckLayers = 1, surroundCheckDistance = 10, surroundBetterThanAverage = 0 };

        for (int i = 0; i < 9; i++)
        {
            Assert.False(placer.StandsOut(rule, new Vector3(100 + i, 0, 100)));
        }
    }
}
