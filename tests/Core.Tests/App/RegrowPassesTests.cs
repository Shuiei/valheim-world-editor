using Vector3 = System.Numerics.Vector3;
using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The checks a spot passes on the ground it stands on: its biome, its altitude, the paint mask, the ocean depth, the
/// slope and the height difference around it. Each check fails alone when its one value is out of its range.
/// </summary>
public class RegrowPassesTests
{
    private static bool Check(string name, params object?[] args) =>
        PrivateMember.Call<bool>(typeof(Regrow), name, null, args);

    [Fact]
    public void BiomePassesWhenTheSpotsBiomeIsOneTheRuleGrowsIn()
    {
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 4);
        Assert.True(Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void BiomeFailsWhenTheRuleDoesNotGrowInTheSpotsBiome()
    {
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 8);
        Assert.False(Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void BiomeFailsWhenTheRuleWantsAnotherAreaOfTheBiome()
    {
        // The zone's four corners are one biome: the spot is in its median, so an edge-only rule (1) is refused.
        object zone = RegrowFixtures.FlatZone(biome: 4);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 4);
        PrivateMember.Set(rule, "biomeArea", 1);
        Assert.False(Check("PassesBiome", rule, zone, 0f, 0f));
    }

    [Fact]
    public void AltitudePassesInsideTheRuleRangeAboveTheWater()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minAltitude", 0f);
        PrivateMember.Set(rule, "maxAltitude", 10f);
        Assert.True(Check("PassesAltitude", rule, TerrainService.WaterLevel + 5f));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(-1f)]
    public void AltitudeFailsAboveOrBelowTheRuleRange(float offset)
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minAltitude", 0f);
        PrivateMember.Set(rule, "maxAltitude", 3f);
        Assert.False(Check("PassesAltitude", rule, TerrainService.WaterLevel + offset));
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
        Assert.True(Check("PassesVegetationMask", rule, zone, 0f, 0f));
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
        Assert.False(Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }

    [Fact]
    public void VegetationMaskIsNotCheckedWhenItsRangeIsEmpty()
    {
        // Equal bounds mean "no check" (the rule leaves the mask alone), whatever the mask holds.
        object zone = RegrowFixtures.FlatZone();
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minVegetation", 0.9f);
        PrivateMember.Set(rule, "maxVegetation", 0.9f);
        Assert.True(Check("PassesVegetationMask", rule, zone, 0f, 0f));
    }

    [Fact]
    public void OceanDepthPassesInsideItsRange()
    {
        object zone = RegrowFixtures.FlatZone();
        Array.Fill((float[])PrivateMember.Get(zone, "OceanDepth")!, 5f);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minOceanDepth", 1f);
        PrivateMember.Set(rule, "maxOceanDepth", 10f);
        Assert.True(Check("PassesOceanDepth", rule, zone, 0f, 0f));
        PrivateMember.Set(rule, "maxOceanDepth", 3f);
        Assert.False(Check("PassesOceanDepth", rule, zone, 0f, 0f));
    }

    [Fact]
    public void SlopePassesWhenTheGroundIsWithinTheRuleTilt()
    {
        // A normal tilted 60 degrees from up (Y = 0.5) is within a 90 degree limit, not within 30.
        var normal = new Vector3(0.866f, 0.5f, 0f);
        object rule = RegrowFixtures.OpenRule();
        Assert.True(Check("PassesSlope", rule, normal));
        PrivateMember.Set(rule, "maxTilt", 30f);
        Assert.False(Check("PassesSlope", rule, normal));
    }

    [Fact]
    public void TerrainDeltaIsNotCheckedWithoutARadius()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minTerrainDelta", 5f);
        Assert.True(Check("PassesTerrainDelta", rule, RegrowFixtures.FlatGround(), 0f, 0f));
    }

    [Fact]
    public void TerrainDeltaFailsWhenTheGroundAroundIsFlatterThanTheRuleAllows()
    {
        // Ten samples within 2 m of a flat ground differ by nothing: below the rule's least difference.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "terrainDeltaRadius", 2f);
        PrivateMember.Set(rule, "minTerrainDelta", 0.5f);
        Assert.False(Check("PassesTerrainDelta", rule, RegrowFixtures.FlatGround(3f), 0f, 0f));
    }

    [Fact]
    public void TerrainDeltaPassesWhenTheGroundAroundIsWithinTheRuleRange()
    {
        // A slope of 1 m per metre across a 2 m radius: the height differs by more than 0 and by less than 10.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "terrainDeltaRadius", 2f);
        PrivateMember.Set(rule, "minTerrainDelta", 0f);
        PrivateMember.Set(rule, "maxTerrainDelta", 10f);
        Assert.True(Check("PassesTerrainDelta", rule, RegrowFixtures.SlopedGround(1f), 0f, 0f));
    }

    [Fact]
    public void SurfaceChecksPassWhenEveryCheckPasses()
    {
        object rule = RegrowFixtures.OpenRule();
        object zone = RegrowFixtures.FlatZone(biome: 4);
        PrivateMember.Set(rule, "biome", 4);
        Assert.True(Check("PassesSurfaceChecks", rule, RegrowFixtures.FlatGround(), zone, 0f, TerrainService.WaterLevel + 5f, 0f, Vector3.UnitY));
    }

    [Fact]
    public void SurfaceChecksFailWhenOneCheckFails()
    {
        object rule = RegrowFixtures.OpenRule();
        object zone = RegrowFixtures.FlatZone(biome: 4);
        PrivateMember.Set(rule, "biome", 4);
        PrivateMember.Set(rule, "maxAltitude", 1f);
        Assert.False(Check("PassesSurfaceChecks", rule, RegrowFixtures.FlatGround(), zone, 0f, TerrainService.WaterLevel + 5f, 0f, Vector3.UnitY));
    }

    [Fact]
    public void CenterDistancePassesWhenNoLimitIsSet()
    {
        Assert.True(Check("PassesCenterDistance", RegrowFixtures.OpenRule(), 300f, 400f));
    }

    [Fact]
    public void CenterDistanceFailsCloserThanTheMinimum()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "minDistanceFromCenter", 10f);
        Assert.False(Check("PassesCenterDistance", rule, 3f, 4f));
        Assert.True(Check("PassesCenterDistance", rule, 30f, 40f));
    }

    [Fact]
    public void CenterDistanceFailsFartherThanTheMaximum()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "maxDistanceFromCenter", 20f);
        Assert.False(Check("PassesCenterDistance", rule, 30f, 40f));
        Assert.True(Check("PassesCenterDistance", rule, 3f, 4f));
    }

    [Fact]
    public void ForestIsNotCheckedWhenTheRuleIsNotForestOnly()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "forestTresholdMin", 100f);
        Assert.True(Check("PassesForest", rule, 0f, 0f, 0f));
    }

    [Fact]
    public void ForestPassesWhenTheForestFactorIsWithinTheThresholds()
    {
        // The forest factor is a small number at any spot (GetForestFactor): the wide range holds it, a far one does not.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "inForest", 1);
        PrivateMember.Set(rule, "forestTresholdMin", -10f);
        PrivateMember.Set(rule, "forestTresholdMax", 10f);
        Assert.True(Check("PassesForest", rule, 0f, 0f, 0f));
        PrivateMember.Set(rule, "forestTresholdMin", 100f);
        PrivateMember.Set(rule, "forestTresholdMax", 200f);
        Assert.False(Check("PassesForest", rule, 0f, 0f, 0f));
    }

    [Fact]
    public void RegionChecksFailWhenEitherPartFails()
    {
        object rule = RegrowFixtures.OpenRule();
        Assert.True(Check("PassesRegionChecks", rule, 3f, 0f, 4f));
        PrivateMember.Set(rule, "minDistanceFromCenter", 10f);
        Assert.False(Check("PassesRegionChecks", rule, 3f, 0f, 4f));
    }
}
