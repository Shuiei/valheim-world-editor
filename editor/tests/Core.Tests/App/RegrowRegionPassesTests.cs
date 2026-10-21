using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The checks a spot passes by where it lies in the region: how far it is from the centre, the forest around it and
/// the region's two checks together. Each check fails alone when its one value is out of its range.
/// </summary>
public class RegrowRegionPassesTests
{
    [Fact]
    public void CenterDistancePassesWhenNoLimitIsSet()
    {
        Assert.True(RegrowFixtures.Check("PassesCenterDistance", RegrowFixtures.OpenRule(), 300f, 400f));
    }

    [Fact]
    public void CenterDistanceFailsCloserThanTheMinimum()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minDistanceFromCenter", 10f);
        Assert.False(RegrowFixtures.Check("PassesCenterDistance", rule, 3f, 4f));
        Assert.True(RegrowFixtures.Check("PassesCenterDistance", rule, 30f, 40f));
    }

    [Fact]
    public void CenterDistanceFailsFartherThanTheMaximum()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "maxDistanceFromCenter", 20f);
        Assert.False(RegrowFixtures.Check("PassesCenterDistance", rule, 30f, 40f));
        Assert.True(RegrowFixtures.Check("PassesCenterDistance", rule, 3f, 4f));
    }

    [Fact]
    public void ForestIsNotCheckedWhenTheRuleIsNotForestOnly()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "forestTresholdMin", 100f);
        Assert.True(RegrowFixtures.Check("PassesForest", RegrowFixtures.Terrain, rule, 0f, 0f, 0f));
    }

    [Fact]
    public void ForestPassesWhenTheForestFactorIsWithinTheThresholds()
    {
        // The forest factor is a small number at any spot (GetForestFactor): the wide range holds it, a far one does not.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "inForest", 1);
        PrivateMember.Set(rule, "forestTresholdMin", -10f);
        PrivateMember.Set(rule, "forestTresholdMax", 10f);
        Assert.True(RegrowFixtures.Check("PassesForest", RegrowFixtures.Terrain, rule, 0f, 0f, 0f));
        PrivateMember.Set(rule, "forestTresholdMin", 100f);
        PrivateMember.Set(rule, "forestTresholdMax", 200f);
        Assert.False(RegrowFixtures.Check("PassesForest", RegrowFixtures.Terrain, rule, 0f, 0f, 0f));
    }

    [Fact]
    public void RegionChecksFailWhenEitherPartFails()
    {
        object rule = RegrowFixtures.OpenRule();
        Assert.True(RegrowFixtures.Check("PassesRegionChecks", RegrowFixtures.Terrain, rule, 3f, 0f, 4f));
        PrivateMember.Set(rule, "minDistanceFromCenter", 10f);
        Assert.False(RegrowFixtures.Check("PassesRegionChecks", RegrowFixtures.Terrain, rule, 3f, 0f, 4f));
    }
}
