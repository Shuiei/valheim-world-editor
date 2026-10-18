using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world generator with a world generation mod's rules (WorldGenerator.Rules.cs): the game's own values tell no
/// difference, and each setting moves what it says: the radius, the stretch, the altitude and depth, the water level,
/// the rivers and streams, the forest, the edge circles and their gaps.
/// </summary>
public class WorldGeneratorRulesTests
{
    private const int Seed = 12345;

    private static WorldGenerator Generator(WorldRules? rules = null) =>
        WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla });

    private static double AshlandsGap(WorldGenerator g, float x, float z) =>
        PrivateMember.Call<double>(typeof(WorldGenerator), "AshlandsGap", g, x, z);

    /// <summary>The sea's shape is the continents' height itself (no hills, no rivers): a setting on it shows whole.</summary>
    private static float Sea(WorldGenerator g, float x, float z) => g.GetBiomeHeight(Heightmap.Biome.Ocean, x, z, out _);

    [Fact]
    public void TheRulesAreTheGeneratorsAndTellGeneratorsApart()
    {
        var plain = Generator();
        Assert.Same(WorldRules.Vanilla, plain.Rules);
        Assert.Equal(10000f, plain.WorldSize);
        Assert.Equal("", plain.Identity.Rules);
        var rules = new WorldRules { Radius = 20000f, Stretch = 2f };
        var big = Generator(rules);
        Assert.Equal(10000f, big.WorldSize);
        Assert.Equal(1000f, big.Stretched(2000f));
        Assert.NotEqual(plain.Identity, big.Identity);
        Assert.Equal(big.Identity, Generator(rules with { }).Identity);
    }

    [Fact]
    public void AStretchedWorldTwiceAsWideHasTheBiomesOfThePlainOneAtHalfTheDistance()
    {
        // Radius 20 000 m stretched by 2: every formula sees the plain world's coordinates, so the biome at (2x, 2z)
        // is the plain world's at (x, z) (the ground differs: the lakes are scanned over the wider world).
        var plain = Generator();
        var stretched = Generator(new WorldRules { Radius = 20000f, Stretch = 2f });
        var points = GeneratorPoints.Rings(200, 9800, 400, 36).ToList();
        Assert.All(points, p => Assert.Equal(plain.GetBiome(p.X, p.Z), stretched.GetBiome(2 * p.X, 2 * p.Z)));
        Assert.Contains(points, p => plain.GetBiome(p.X, p.Z) == Heightmap.Biome.Plains);
    }

    [Fact]
    public void AWiderWorldKeepsLandPastTheGamesEdge()
    {
        var plain = Generator();
        var wide = Generator(new WorldRules { Radius = 20000f });
        // Past 10 500 m the plain world is the flat edge, -400 m; the wide one still has ground there.
        Assert.Equal(-400f, plain.GetHeight(0, 12000));
        Assert.NotEqual(-400f, wide.GetHeight(0, 12000));
        Assert.Equal(-400f, wide.GetHeight(0, 21000));
        Assert.Equal(20000f, wide.WorldSize);
    }

    [Fact]
    public void TheAltitudeDeltaRaisesTheContinents()
    {
        var plain = Generator();
        var raised = Generator(new WorldRules { AltitudeDelta = 10f });
        var (x, z) = GeneratorPoints.Rings(500, 3000, 500, 8).First(p => Sea(plain, p.X, p.Z) is > 35f and < 60f);
        Assert.Equal(Sea(plain, x, z) + 10f, Sea(raised, x, z), 2);
    }

    [Fact]
    public void TheDepthMultiplierDeepensTheGroundUnderTheWaterOnly()
    {
        var plain = Generator();
        var deeper = Generator(new WorldRules { WaterDepthMultiplier = 2f });
        var (x, z) = GeneratorPoints.Rings(500, 3000, 500, 8).First(p => Sea(plain, p.X, p.Z) is > 35f and < 60f);
        Assert.Equal(Sea(plain, x, z), Sea(deeper, x, z));
        var (sx, sz) = GeneratorPoints.Rings(500, 3000, 500, 8).First(p => Sea(plain, p.X, p.Z) is > 10f and < 28f);
        float under = Sea(plain, sx, sz);
        Assert.Equal((under - 30f) * 2f + 30f, Sea(deeper, sx, sz), 3);
    }

    [Fact]
    public void TheWaterLevelMovesTheStreamsWindow()
    {
        // The streams' start and end windows follow the water level: with the water 10 m lower (and nothing else
        // changed) other streams are laid out than at 30 m.
        var plain = Generator();
        var low = Generator(new WorldRules { WaterLevel = 20f });
        Assert.NotEmpty(plain.GetStreams());
        Assert.NotEqual(plain.GetStreams().Select(s => s.P0), low.GetStreams().Select(s => s.P0));
    }

    [Fact]
    public void RiversAndStreamsCanBeTurnedOff()
    {
        var none = Generator(new WorldRules { Rivers = false, Streams = false });
        Assert.Empty(none.GetRivers());
        Assert.Empty(none.GetStreams());
        Assert.NotEmpty(Generator().GetRivers());
    }

    [Fact]
    public void TheForestMultiplierDividesTheFactorAndZeroMeansOne()
    {
        var pos = new Vector3(1000f, 0f, -2000f);
        float plain = Generator().GetForestFactor(pos);
        Assert.Equal(plain / 2f, Generator(new WorldRules { ForestMultiplier = 2f }).GetForestFactor(pos));
        // The mod takes a multiplier of 0 as 1.
        Assert.Equal(plain, Generator(new WorldRules { ForestMultiplier = 0f }).GetForestFactor(pos));
    }

    [Fact]
    public void TheEdgeCirclesScaleWithTheRadius()
    {
        // Twice the radius: the Deep North's edge is 24 000 m from a middle 8 000 m north, so 16 000 m up the axis.
        var g = Generator(new WorldRules { Radius = 20000f });
        Assert.Equal(0.0, g.CreateDeepNorthGap(0f, 16000f), 6);
        Assert.Equal(1.0, g.CreateDeepNorthGap(0f, 16800f), 6);
        Assert.Equal(0.0, AshlandsGap(g, 0f, -16000f), 6);
        Assert.True(g.IsDeepnorth(0f, 16100f));
        Assert.False(g.IsDeepnorth(0f, 15900f));
        Assert.True(g.IsAshlands(0f, -16100f));
        Assert.False(g.IsAshlands(0f, -15900f));
    }

    [Fact]
    public void TheGapsCanBeTurnedOff()
    {
        var g = Generator(new WorldRules { AshlandsGap = false, DeepNorthGap = false });
        Assert.Equal(1.0, g.CreateDeepNorthGap(0f, 8000f));
        Assert.Equal(1.0, AshlandsGap(g, 0f, -8000f));
        // The edge itself is where it was: only the ground stops sinking along it.
        Assert.True(g.IsDeepnorth(0f, 8100f));
    }
}
