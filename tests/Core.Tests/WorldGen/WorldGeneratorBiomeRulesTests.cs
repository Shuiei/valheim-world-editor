using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world generator with Expand World Data's world rules (WorldGenerator.BiomeRules.cs): the first rule a point
/// passes names its biome, the rules' distances, centres, sectors, rectangles, noise and boiling, the legacy
/// evaluation's differences, and the game's own layout kept when the mod is installed without rules.
/// </summary>
public class WorldGeneratorBiomeRulesTests
{
    private const int Seed = 12345;

    private static WorldGenerator Generator(WorldRules? rules = null) =>
        WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla });

    /// <summary>A generator with world rules (the mod installed), no distance waves unless a rule asks.</summary>
    private static WorldGenerator WithRules(bool legacy = false, bool sectors = false, params BiomeRule[] rules) =>
        Generator(new WorldRules { DataInstalled = true, World = rules, LegacyGeneration = legacy, CheckSectors = sectors });

    /// <summary>A rule for a biome everywhere (no waves), to be narrowed.</summary>
    private static BiomeRule Rule(Heightmap.Biome biome) => new() { Biome = biome, SeedBiome = biome, WiggleDistanceWidth = 0f, WiggleSectorWidth = 0f, WiggleRectangleWidth = 0f };

    [Fact]
    public void TheFirstRuleAPointPassesNamesItsBiomeAndNoneTheOcean()
    {
        var g = WithRules(false, false, Rule(Heightmap.Biome.Plains) with { MinAltitude = 1000f }, Rule(Heightmap.Biome.Swamp) with { MaxDistance = 3000f }, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, g.GetBiome(1000f, 0f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(5000f, 0f));
        // Past the edge of the world no rule applies.
        Assert.Equal(Heightmap.Biome.Ocean, g.GetBiome(0f, 11000f));
        // No rule at all: the ocean everywhere.
        Assert.Equal(Heightmap.Biome.Ocean, WithRules().GetBiome(0f, 0f));
    }

    [Fact]
    public void TheGamesLayoutIsKeptWhenTheModIsInstalledWithoutRules()
    {
        // The mod's four extra draws leave the seed's seven as they are.
        var plain = Generator();
        var installed = Generator(new WorldRules { DataInstalled = true });
        foreach (var (x, z) in GeneratorPoints.Rings(200, 9800, 400, 24))
        {
            Assert.Equal(plain.GetBiome(x, z), installed.GetBiome(x, z));
            Assert.Equal(plain.GetHeight(x, z), installed.GetHeight(x, z));
        }
    }

    [Fact]
    public void TheModsDefaultRulesGiveTheGamesLayout()
    {
        // The mod's default rules say the game's own layout in its words (the same circles, waves, bands, noise
        // fields and thresholds, in the same order): the biomes come out the same, but for a point on an exact edge.
        var (rules, sectors) = TerrainEditor.App.ExpandWorld.WorldOf(TerrainEditor.App.ExpandWorld.DefaultWorld, BiomeTable.Vanilla, 10000f, TerrainEditor.App.ExpandWorld.Wiggles.Default, []);
        var g = Generator(new WorldRules { DataInstalled = true, World = rules, CheckSectors = sectors });
        var plain = Generator();
        Assert.False(sectors);
        var points = GeneratorPoints.Rings(200, 9800, 400, 24).ToList();
        Assert.All(points, p => Assert.Equal(plain.GetBiome(p.X, p.Z), g.GetBiome(p.X, p.Z)));
        Assert.Contains(points, p => g.GetBiome(p.X, p.Z) == Heightmap.Biome.Mountain);
        Assert.Contains(points, p => g.GetBiome(p.X, p.Z) == Heightmap.Biome.Swamp);
        // The Ashlands' rule: 12 000 to 16 000 m from a middle 4 000 m north, first in the list.
        Assert.Equal(Heightmap.Biome.AshLands, g.GetBiome(0f, -8500f));
        Assert.True(g.IsAshlands(0f, -8500f));
    }

    [Fact]
    public void TheOuterEdgeIsOutsideUnlessLegacy()
    {
        // 0.3 of the radius is 3000 m to the bit, where the point (3000, 0) lies.
        var swamp = Rule(Heightmap.Biome.Swamp) with { MaxDistance = 3000f };
        Assert.Equal(Heightmap.Biome.Meadows, WithRules(false, false, swamp, Rule(Heightmap.Biome.Meadows)).GetBiome(3000f, 0f));
        Assert.Equal(Heightmap.Biome.Swamp, WithRules(true, false, swamp, Rule(Heightmap.Biome.Meadows)).GetBiome(3000f, 0f));
        Assert.Equal(Heightmap.Biome.Swamp, WithRules(false, false, swamp, Rule(Heightmap.Biome.Meadows)).GetBiome(2999f, 0f));
    }

    [Fact]
    public void ACentredRuleMeasuresFromItsCentre()
    {
        var g = WithRules(false, false, Rule(Heightmap.Biome.Swamp) with { CenterX = 2000f, MaxDistance = 500f }, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, g.GetBiome(2000f, 0f));
        Assert.Equal(Heightmap.Biome.Swamp, g.GetBiome(2300f, 300f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(0f, 0f));
    }

    [Fact]
    public void SectorsAreCheckedOnlyWhenARuleHasOne()
    {
        // From south clockwise: west is a quarter of the way round, east three quarters.
        var west = Rule(Heightmap.Biome.Swamp) with { MinSector = 0.25f, MaxSector = 0.5f };
        var g = WithRules(false, true, west, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, g.GetBiome(-100f, 0f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(100f, 0f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(0f, -100f));
        // Without the check, the rule passes everywhere.
        Assert.Equal(Heightmap.Biome.Swamp, WithRules(false, false, west, Rule(Heightmap.Biome.Meadows)).GetBiome(100f, 0f));
        // A sector past 1 wraps round: from 0.9 to 0.1 holds the south.
        var south = Rule(Heightmap.Biome.Swamp) with { MinSector = 0.9f, MaxSector = 0.1f };
        var h = WithRules(false, true, south, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, h.GetBiome(0f, -100f));
        Assert.Equal(Heightmap.Biome.Meadows, h.GetBiome(0f, 100f));
    }

    [Fact]
    public void ARectangleRuleKeepsItsBoxUnlessLegacy()
    {
        var box = Rule(Heightmap.Biome.Swamp) with { HasRectangle = true, HalfSizeX = 500f, HalfSizeY = 200f };
        var g = WithRules(false, false, box, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, g.GetBiome(400f, 100f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(600f, 0f));
        Assert.Equal(Heightmap.Biome.Meadows, g.GetBiome(0f, 300f));
        // Turned a quarter: the long side runs north-south.
        var turned = box with { RotationCos = 0f, RotationSin = 1f };
        var t = WithRules(false, false, turned, Rule(Heightmap.Biome.Meadows));
        Assert.Equal(Heightmap.Biome.Swamp, t.GetBiome(0f, 400f));
        Assert.Equal(Heightmap.Biome.Meadows, t.GetBiome(400f, 0f));
        // The legacy evaluation knows no rectangles.
        Assert.Equal(Heightmap.Biome.Swamp, WithRules(true, false, box, Rule(Heightmap.Biome.Meadows)).GetBiome(600f, 0f));
    }

    [Fact]
    public void TheAmountDrawsFromTheRulesSeed()
    {
        var points = GeneratorPoints.Rings(200, 9800, 400, 24).ToList();
        var some = Rule(Heightmap.Biome.Swamp) with { Amount = 0.5f, Seed = 123 };
        var a = WithRules(false, false, some, Rule(Heightmap.Biome.Meadows));
        var b = WithRules(false, false, some with { Seed = 456 }, Rule(Heightmap.Biome.Meadows));
        var c = WithRules(false, false, some with { Seed = null, SeedBiome = Heightmap.Biome.Plains }, Rule(Heightmap.Biome.Meadows));
        Assert.Contains(points, p => a.GetBiome(p.X, p.Z) == Heightmap.Biome.Swamp);
        Assert.Contains(points, p => a.GetBiome(p.X, p.Z) == Heightmap.Biome.Meadows);
        Assert.Contains(points, p => a.GetBiome(p.X, p.Z) != b.GetBiome(p.X, p.Z));
        Assert.Contains(points, p => a.GetBiome(p.X, p.Z) != c.GetBiome(p.X, p.Z));
        // An amount of 1 passes everywhere, seed or no seed.
        var all = WithRules(false, false, some with { Amount = 1f });
        Assert.All(points, p => Assert.Equal(Heightmap.Biome.Swamp, all.GetBiome(p.X, p.Z)));
    }

    [Fact]
    public void BoilingMakesTheAshlandsAndTheSeasHeat()
    {
        var g = WithRules(false, false, Rule(Heightmap.Biome.AshLands) with { MinDistance = 1000f, Boiling = 1f }, Rule(Heightmap.Biome.Meadows));
        Assert.True(g.IsAshlands(0f, 1300f));
        Assert.False(g.IsAshlands(0f, 500f));
        // 300 m past the rule's inner edge: a heat of 1.
        Assert.Equal(1f, g.GetAshlandsOceanGradient(0f, 1300f), 4);
        Assert.Equal(0.5f, g.GetAshlandsOceanGradient(0f, 1150f), 4);
        Assert.Equal(-1f, g.GetAshlandsOceanGradient(0f, 500f));
        // A boiling of 2 is twice as hot.
        var hot = WithRules(false, false, Rule(Heightmap.Biome.AshLands) with { MinDistance = 1000f, Boiling = 2f });
        Assert.Equal(2f, hot.GetAshlandsOceanGradient(0f, 1300f), 4);
    }

    [Fact]
    public void ATerritoryRuleNamesNoBiomeAndReshapesTheGround()
    {
        var hot = new BiomeRule { Territory = "hot", MaxDistance = 500f, WiggleDistanceWidth = 0f };
        var rules = new WorldRules { DataInstalled = true, World = [hot, Rule(Heightmap.Biome.Meadows)] };
        var plain = Generator(rules);
        Assert.Equal(Heightmap.Biome.Meadows, plain.GetBiome(0f, 0f));
        Assert.Null(plain.TerritoryAt(0f, 0f));
        var raised = Generator(rules with { Territories = [new Territory { Name = "hot", Height = new HeightTransform { Delta = 10f } }] });
        Assert.Equal("hot", raised.TerritoryAt(0f, 0f)!.Name);
        Assert.Null(raised.TerritoryAt(1000f, 0f));
        Assert.Equal(plain.GetHeight(100f, 100f) + 10f, raised.GetHeight(100f, 100f), 2);
        Assert.Equal(plain.GetHeight(1000f, 0f), raised.GetHeight(1000f, 0f), 3);
    }

    [Fact]
    public void TheWiggleFrequencyChangesTheEdgesWave()
    {
        var plain = Generator();
        var waved = Generator(new WorldRules { WiggleFrequency = 40f });
        // Points on the Deep North's edge circle (12 000 m round a middle 4 000 m south), 7 degrees apart: twenty and
        // forty swings differ at most of them (at multiples of 15 degrees they happen to agree).
        var edge = Enumerable.Range(-5, 11).Select(k => k * 7.0 * Math.PI / 180.0).Select(a => (X: (float)(12000.0 * Math.Sin(a)), Z: (float)(-4000.0 + 12000.0 * Math.Cos(a)))).ToList();
        Assert.Contains(edge, p => plain.CreateDeepNorthGap(p.X, p.Z) != waved.CreateDeepNorthGap(p.X, p.Z));
        Assert.Contains(edge, p => plain.IsDeepnorth(p.X, p.Z) != waved.IsDeepnorth(p.X, p.Z));
        // The game's twenty swings: the static wave and the generator's are one.
        Assert.Equal(WorldGenerator.WorldAngle(123f, 456f), PrivateMember.Call<float>(typeof(WorldGenerator), "Wave", plain, 123f, 456f));
    }
}
