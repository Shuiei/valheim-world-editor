using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// One spot of a zone: refused before any other check when it is out of the kind's ring, and kept per zone when it passes.
/// </summary>
public class SeedLocationsPlacerSpotsTests
{
    /// <summary>A placer for a seed; the biome map is not needed for these checks.</summary>
    private static SeedLocations.Placer Placer(WorldGenerator gen) => new(gen, () => throw new InvalidOperationException("no biome map needed"));

    [Fact]
    public void ASpotOutOfTheKindsRingIsNotTried()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { Prefab = "Temple", MinDistance = 1_000_000 };

        Assert.False(placer.TrySpot(rule, 0, (0, 0), 0f));
        Assert.Empty(placer.Instances);
    }

    [Fact]
    public void ARegisteredSpotIsKeptOncePerZone()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { Prefab = "Temple" };

        placer.Register(rule, new Vector3(10, 0, 0));
        placer.Register(rule, new Vector3(20, 0, 0));
        placer.Register(rule, new Vector3(200, 0, 0));

        Assert.Equal(2, placer.Instances.Count);
        Assert.Equal((0, 0), placer.Instances.Keys.First());
    }
}
