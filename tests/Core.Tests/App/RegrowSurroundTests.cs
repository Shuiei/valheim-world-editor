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
        // Nine spots across a zone whose vegetation grows from west to east set the bar; a tenth halfway to one end
        // passes at the average only on the side the sums grow, and never when the bar is the best so far.
        bool[] atAverage = [Passes(w, 0f, -4f), Passes(w, 0f, 4f)];
        Assert.Equal(1, atAverage.Count(p => p));
        Assert.False(Passes(w, 1f, -4f));
        Assert.False(Passes(w, 1f, 4f));
    }

    /// <summary>
    /// Whether a tenth spot at this x passes the check of a rule with this share, after nine at x = -8, -6... 8 on a
    /// zone whose mask's vegetation is x / 64 (the nine only set the bar).
    /// </summary>
    private static bool Passes(TempWorld w, float better, float x)
    {
        object zone = RegrowFixtures.FlatZone();
        float[] mask = new float[65 * 65 * 4];
        for (int i = 0; i < 65 * 65; i++)
        {
            mask[i * 4 + 3] = i % 65 / 64f;
        }
        PrivateMember.Set(zone, "Mask", mask);
        var rule = Checked(1, better);
        rule.surroundCheckDistance = 4f;
        object pass = PrivateMember.New(PrivateMember.Nested(typeof(Regrow), "Pass"), new TerrainService(w.Load(), null), RegrowFixtures.FlatGround(), new List<VegetationRule>(), null);
        bool Check(float px) => PrivateMember.Call<bool>(typeof(Regrow), "PassesSurround", null, pass, rule, zone, px, 0f);
        for (float px = -8f; px <= 8f; px += 2f)
        {
            Assert.False(Check(px));
        }
        return Check(x);
    }
}
