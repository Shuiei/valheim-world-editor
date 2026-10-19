using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The surround check of a rule that asks for it: the first nine spots of the rule in a zone only set the bar, and a
/// spot must then reach its share of the way from the average to the best.
/// </summary>
[Collection("World files")]
public class RegrowSurroundTests
{
    /// <summary>A rule that grows anywhere on land, this many tries in each zone, with the surround check.</summary>
    private static VegetationRule Checked(int tries, float better)
    {
        var rule = (VegetationRule)RegrowFixtures.OpenRule();
        (rule.min, rule.max) = (tries, tries);
        (rule.surroundCheckVegetation, rule.surroundCheckDistance, rule.surroundCheckLayers, rule.surroundBetterThanAverage) = (1, 20f, 2, better);
        return rule;
    }

    /// <summary>The spots Regrow places in zones 0..1 under this rule.</summary>
    private static List<Regrow.Spot> Spots(TempWorld w, VegetationRule rule)
    {
        var world = w.Load();
        world.Rules = new WorldRules { Vegetation = [rule] };
        var terrain = new TerrainService(world, null);
        return Regrow.Zones(terrain, new EditStore(world), world.Seed, 0, 0, 1, 1);
    }

    [Fact]
    public void TheFirstNineSpotsOnlySetTheBar()
    {
        using var w = new TempWorld();
        Assert.Empty(Spots(w, Checked(9, 0f)));
        var plain = Checked(40, 0f);
        plain.surroundCheckVegetation = 0;
        var all = Spots(w, plain);
        var some = Spots(w, Checked(40, 0f));
        Assert.NotEmpty(some);
        Assert.True(some.Count < all.Count);
        // Every spot kept is one the rule would place without the check (the check draws nothing).
        Assert.All(some, s => Assert.Contains(s, all));
    }

    [Fact]
    public void AShareOfTheWayToTheBestRaisesTheBar()
    {
        using var w = new TempWorld();
        var average = Spots(w, Checked(40, 0f));
        var best = Spots(w, Checked(40, 1f));
        Assert.True(best.Count < average.Count);
        Assert.All(best, s => Assert.Contains(s, average));
    }
}
