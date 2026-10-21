using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ocean depth and paint mask checks when their range is empty (a minimum equal to the maximum): the range is not
/// checked at all, so every spot passes whatever its value.
/// </summary>
public sealed class RegrowSpotChecksTests
{
    /// <summary>An empty ocean depth range lets every spot pass, whatever its depth.</summary>
    [Fact]
    public void AnEmptyOceanDepthRangeLetsEverySpotPass()
    {
        object zone = RegrowFixtures.FlatZone();
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minOceanDepth", 5f);
        PrivateMember.Set(rule, "maxOceanDepth", 5f);

        Assert.True(RegrowFixtures.Check("PassesOceanDepth", rule, zone, 0f, 0f));
    }

    /// <summary>An empty paint mask range lets every spot pass, whatever its mask.</summary>
    [Fact]
    public void AnEmptyVegetationMaskRangeLetsEverySpotPass()
    {
        object zone = RegrowFixtures.FlatZone();
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minVegetation", 0.5f);
        PrivateMember.Set(rule, "maxVegetation", 0.5f);

        Assert.True(RegrowFixtures.Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }
}
