using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The checks a spot passes on the ground it stands on: its biome, its altitude, the paint mask, the ocean depth, the
/// slope and the height difference around it. Each check fails alone when its one value is out of its range.
/// </summary>
public class RegrowPassesTests
{
    [Fact]
    public void BiomePassesWhenTheSpotsBiomeIsOneTheRuleGrowsIn()
    {
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 4);
        Assert.True(RegrowFixtures.Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void BiomeFailsWhenTheRuleDoesNotGrowInTheSpotsBiome()
    {
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 8);
        Assert.False(RegrowFixtures.Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void BiomeFailsWhenTheRuleWantsAnotherAreaOfTheBiome()
    {
        // The zone's four corners are one biome: the spot is in its median, so an edge-only rule (1) is refused.
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 4);
        PrivateMember.Set(rule, "biomeArea", 1);
        Assert.False(RegrowFixtures.Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void AltitudePassesInsideTheRuleRangeAboveTheWater()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minAltitude", 0f);
        PrivateMember.Set(rule, "maxAltitude", 10f);
        Assert.True(RegrowFixtures.Check("PassesAltitude", rule, TerrainService.WaterLevel + 5f));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(-1f)]
    public void AltitudeFailsAboveOrBelowTheRuleRange(float offset)
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minAltitude", 0f);
        PrivateMember.Set(rule, "maxAltitude", 3f);
        Assert.False(RegrowFixtures.Check("PassesAltitude", rule, TerrainService.WaterLevel + offset));
    }

    [Fact]
    public void VegetationMaskPassesWhenItIsInsideItsRange()
    {
        // The spot at the zone's middle reads the mask's vertex at (32, 32), set to 0.5.
        object zone = RegrowFixtures.FlatZone();
        ((float[])PrivateMember.Get(zone, "Mask")!)[(32 * 65 + 32) * 4 + 3] = 0.5f;
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minVegetation", 0.2f);
        PrivateMember.Set(rule, "maxVegetation", 0.8f);
        Assert.True(RegrowFixtures.Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }

    [Theory]
    [InlineData(0.4f, 0.0f)]
    [InlineData(0.8f, 0.6f)]
    public void VegetationMaskFailsOutsideItsRange(float max, float min)
    {
        object zone = RegrowFixtures.FlatZone();
        ((float[])PrivateMember.Get(zone, "Mask")!)[(32 * 65 + 32) * 4 + 3] = 0.5f;
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minVegetation", min);
        PrivateMember.Set(rule, "maxVegetation", max);
        Assert.False(RegrowFixtures.Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }

    [Fact]
    public void VegetationMaskIsNotCheckedWhenItsRangeIsEmpty()
    {
        // Equal bounds mean "no check" (the rule leaves the mask alone), whatever the mask holds.
        object zone = RegrowFixtures.FlatZone();
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minVegetation", 0.9f);
        PrivateMember.Set(rule, "maxVegetation", 0.9f);
        Assert.True(RegrowFixtures.Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }

    [Fact]
    public void OceanDepthPassesInsideItsRange()
    {
        object zone = RegrowFixtures.FlatZone();
        Array.Fill((float[])PrivateMember.Get(zone, "OceanDepth")!, 5f);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minOceanDepth", 1f);
        PrivateMember.Set(rule, "maxOceanDepth", 10f);
        Assert.True(RegrowFixtures.Check("PassesOceanDepth", rule, zone, 0f, 0f));
        PrivateMember.Set(rule, "maxOceanDepth", 3f);
        Assert.False(RegrowFixtures.Check("PassesOceanDepth", rule, zone, 0f, 0f));
    }

    [Fact]
    public void SlopePassesWhenTheGroundIsWithinTheRuleTilt()
    {
        // A normal tilted 60 degrees from up (Y = 0.5) is within a 90 degree limit, not within 30.
        var normal = new Vector3(0.866f, 0.5f, 0f);
        object rule = RegrowFixtures.OpenRule();
        Assert.True(RegrowFixtures.Check("PassesSlope", rule, normal));
        PrivateMember.Set(rule, "maxTilt", 30f);
        Assert.False(RegrowFixtures.Check("PassesSlope", rule, normal));
    }

    [Fact]
    public void TerrainDeltaIsNotCheckedWithoutARadius()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minTerrainDelta", 5f);
        Assert.True(RegrowFixtures.Check("PassesTerrainDelta", rule, RegrowFixtures.FlatGround(), 0f, 0f));
    }

    [Fact]
    public void TerrainDeltaFailsWhenTheGroundAroundIsFlatterThanTheRuleAllows()
    {
        // Ten samples within 2 m of a flat ground differ by nothing: below the rule's least difference.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "terrainDeltaRadius", 2f);
        PrivateMember.Set(rule, "minTerrainDelta", 0.5f);
        Assert.False(RegrowFixtures.Check("PassesTerrainDelta", rule, RegrowFixtures.FlatGround(3f), 0f, 0f));
    }

    [Fact]
    public void TerrainDeltaPassesWhenTheGroundAroundIsWithinTheRuleRange()
    {
        // A slope of 1 m per metre across a 2 m radius: the height differs by more than 0 and by less than 10.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "terrainDeltaRadius", 2f);
        PrivateMember.Set(rule, "minTerrainDelta", 0f);
        PrivateMember.Set(rule, "maxTerrainDelta", 10f);
        Assert.True(RegrowFixtures.Check("PassesTerrainDelta", rule, RegrowFixtures.SlopedGround(1f), 0f, 0f));
    }

    [Fact]
    public void SurfaceChecksPassWhenEveryCheckPasses()
    {
        object rule = RegrowFixtures.OpenRule();
        object zone = RegrowFixtures.FlatZone(biome: 4);
        PrivateMember.Set(rule, "biome", 4);
        Assert.True(RegrowFixtures.Check("PassesSurfaceChecks", rule, RegrowFixtures.FlatGround(), zone, 0f, TerrainService.WaterLevel + 5f, 0f, Vector3.UnitY));
    }

    [Fact]
    public void SurfaceChecksFailWhenOneCheckFails()
    {
        object rule = RegrowFixtures.OpenRule();
        object zone = RegrowFixtures.FlatZone(biome: 4);
        PrivateMember.Set(rule, "biome", 4);
        PrivateMember.Set(rule, "maxAltitude", 1f);
        Assert.False(RegrowFixtures.Check("PassesSurfaceChecks", rule, RegrowFixtures.FlatGround(), zone, 0f, TerrainService.WaterLevel + 5f, 0f, Vector3.UnitY));
    }
}
