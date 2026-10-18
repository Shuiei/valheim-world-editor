using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Laying out one kind: nothing for a kind of none wanted, nothing more for a unique kind already there, and at most the
/// number wanted otherwise.
/// </summary>
public class SeedLocationsPlacerGenerateTests
{
    /// <summary>A placer for a seed; the kinds here are laid out from the middle, so the biome map is never asked for.</summary>
    private static SeedLocations.Placer Placer(WorldGenerator gen) => new(gen, () => throw new InvalidOperationException("no biome map needed"));

    [Fact]
    public void AKindWantedNoneIsNotLaidOut()
    {
        var placer = Placer(Seed());

        placer.Generate(new SeedLocations.Rule { Prefab = "Temple", CenterFirst = 1, Quantity = 0 });

        Assert.Empty(placer.Instances);
    }

    [Fact]
    public void AUniqueKindAlreadyThereIsNotLaidOutAgain()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule { Prefab = "Temple", CenterFirst = 1, Quantity = 1, Unique = 1 };
        placer.Register(rule, new ValheimGen.Vector3(10, 0, 0));

        placer.Generate(rule);

        Assert.Single(placer.Instances);
    }

    [Fact]
    public void AKindLaidOutHasNoMoreThanWantedAndOnlyItsOwnPrefab()
    {
        var placer = Placer(Seed());
        var rule = new SeedLocations.Rule
        {
            Prefab = "Temple",
            CenterFirst = 1,
            Quantity = 1,
            Biome = int.MaxValue,
            BiomeArea = int.MaxValue,
            MinAltitude = -1000,
            MaxAltitude = 1000,
        };

        placer.Generate(rule);

        Assert.True(placer.Instances.Count <= 1);
        Assert.All(placer.Instances.Values, p => Assert.Equal("Temple", p.Rule.Prefab));
    }
}
