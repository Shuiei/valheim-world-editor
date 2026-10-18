using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world generator's rules for older worlds (version 0: mountains kept further from the middle; version 1: swamps
/// further out), the main menu's world (mountains and black forest only), and its small helpers: heights at a point
/// given every way the game gives it, and the edges' gradients.
/// </summary>
public class WorldGeneratorTests
{
    private const int Seed = 12345;

    /// <summary>
    /// A generator for the seed, of a world generator version, or the main menu's world.
    /// </summary>
    private static WorldGenerator Generator(int version = 2, bool menu = false) =>
        WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = version, Menu = menu });

    /// <summary>
    /// Points on rings round the world's middle, from r0 up to r1 (step metres apart), at n angles.
    /// </summary>
    private static IEnumerable<(float X, float Z)> Rings(float r0, float r1, float step, int n) =>
        from a in Enumerable.Range(0, n)
        from r in Enumerable.Range(0, (int)((r1 - r0) / step)).Select(i => r0 + i * step)
        select (r * MathF.Cos(a * 2 * MathF.PI / n), r * MathF.Sin(a * 2 * MathF.PI / n));

    [Fact]
    public void VersionOneWorldsHaveSwampsFurtherOut()
    {
        var older = Generator(1);
        var now = Generator();
        var ring = Rings(6200, 8000, 150, 720).ToList();
        // Since version 2 no swamp lies past 6 km; before, up to 8 km.
        Assert.DoesNotContain(ring, p => now.GetBiome(p.X, p.Z) == Heightmap.Biome.Swamp);
        Assert.Contains(ring, p => older.GetBiome(p.X, p.Z) == Heightmap.Biome.Swamp);
    }

    [Fact]
    public void VersionZeroWorldsKeepMountainsFurtherFromTheMiddle()
    {
        var older = Generator(0);
        var now = Generator();
        var ring = Rings(1000, 1400, 50, 720).ToList();
        int Mountains(WorldGenerator g) => ring.Count(p => g.GetBiome(p.X, p.Z) == Heightmap.Biome.Mountain);
        // Within 1.5 km (1 km since version 1) the ground is held below the mountains' height.
        Assert.True(Mountains(older) < Mountains(now), $"{Mountains(older)} >= {Mountains(now)}");
        Assert.Contains(ring, p => older.GetHeight(p.X, p.Z) != now.GetHeight(p.X, p.Z));
        Assert.Equal(Seed, older.GetSeed());
    }

    [Fact]
    public void TheMenusWorldIsMountainsAndBlackForest()
    {
        var menu = Generator(menu: true);
        var points = Enumerable.Range(0, 2000).Select(i => ((i % 50) * 200f - 5000, (i / 50) * 200f - 4000)).ToList();
        var biomes = points.Select(p => menu.GetBiome(p.Item1, p.Item2)).ToHashSet();
        Assert.True(biomes.SetEquals([Heightmap.Biome.BlackForest, Heightmap.Biome.Mountain]), string.Join(", ", biomes));
        Assert.All(points, p => Assert.True(float.IsFinite(menu.GetHeight(p.Item1, p.Item2))));
        // Its mountains have their own shape.
        Assert.NotEqual(menu.GetBiomeHeight(Heightmap.Biome.Meadows, 100, 100, out _), menu.GetBiomeHeight(Heightmap.Biome.Mountain, 100, 100, out _));
    }

    [Fact]
    public void HeightsAreTheSameHoweverThePointIsGiven()
    {
        var g = Generator();
        float h = g.GetHeight(523, -311);
        Assert.Equal(h, g.GetHeight(new Vector3(523, 5, -311)));
        Assert.Equal(h, g.GetHeight(new Vector2(523, -311)));
        // A biome the generator has no shape for: flat at 0.
        Assert.Equal(0, g.GetBiomeHeight(Heightmap.Biome.None, 100, 100, out _));
    }

    [Fact]
    public void TheEdgesGradientsRiseAwayFromTheMiddle()
    {
        Assert.Equal(0, WorldGenerator.DeepNorthWaveFade(0, 0));
        Assert.Equal(1, WorldGenerator.DeepNorthWaveFade(0, 10000));
        var gen = Generator();
        float g = gen.GetAshlandsOceanGradient(10, -9000);
        Assert.True(g > 1);
        Assert.Equal(g, gen.GetAshlandsOceanGradient(new Vector2(10, -9000)));
        Assert.Equal(g, gen.GetAshlandsOceanGradient(new Vector3(10, 7, -9000)));
    }

    [Fact]
    public void TheRulesAreTheGeneratorsAndTellGeneratorsApart()
    {
        var plain = Generator();
        Assert.Same(WorldRules.Vanilla, plain.Rules);
        Assert.Equal(10000f, plain.WorldSize);
        Assert.Equal("", plain.Identity.Rules);
        var rules = new WorldRules { Radius = 20000f, Stretch = 2f };
        var big = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = rules });
        Assert.Equal(10000f, big.WorldSize);
        Assert.Equal(1000f, big.Stretched(2000f));
        Assert.NotEqual(plain.Identity, big.Identity);
        Assert.Equal(big.Identity, WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = rules with { } }).Identity);
    }

    [Fact]
    public void AStretchedWorldTwiceAsWideHasTheBiomesOfThePlainOneAtHalfTheDistance()
    {
        // Radius 20 000 m stretched by 2: every formula sees the plain world's coordinates, so the biome at (2x, 2z)
        // is the plain world's at (x, z) (the ground differs: the lakes are scanned over the wider world).
        var plain = Generator();
        var stretched = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { Radius = 20000f, Stretch = 2f } });
        var points = Rings(200, 9800, 400, 36).ToList();
        Assert.All(points, p => Assert.Equal(plain.GetBiome(p.X, p.Z), stretched.GetBiome(2 * p.X, 2 * p.Z)));
        Assert.Contains(points, p => plain.GetBiome(p.X, p.Z) == Heightmap.Biome.Plains);
    }

    [Fact]
    public void AWiderWorldKeepsLandPastTheGamesEdge()
    {
        var plain = Generator();
        var wide = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { Radius = 20000f } });
        // Past 10 500 m the plain world is the flat edge, -400 m; the wide one still has ground there.
        Assert.Equal(-400f, plain.GetHeight(0, 12000));
        Assert.NotEqual(-400f, wide.GetHeight(0, 12000));
        Assert.Equal(-400f, wide.GetHeight(0, 21000));
        Assert.Equal(20000f, wide.WorldSize);
    }

    [Fact]
    public void TheAltitudeAndDepthSettingsMoveTheGround()
    {
        var plain = Generator();
        var raised = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { AltitudeDelta = 10f } });
        var deeper = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { WaterDepthMultiplier = 2f } });
        // The sea's shape is the continents' height itself (no hills, no rivers): the altitude delta shows there whole.
        static float Sea(WorldGenerator g, float x, float z) => g.GetBiomeHeight(Heightmap.Biome.Ocean, x, z, out _);
        var (x, z) = Rings(500, 3000, 500, 8).First(p => Sea(plain, p.X, p.Z) is > 35f and < 60f);
        float h = Sea(plain, x, z);
        Assert.Equal(h + 10f, Sea(raised, x, z), 2);
        // The depth multiplier leaves ground above the water alone and doubles the depth of ground under it.
        Assert.Equal(h, Sea(deeper, x, z));
        var (sx, sz) = Rings(500, 3000, 500, 8).First(p => Sea(plain, p.X, p.Z) is > 10f and < 28f);
        float under = Sea(plain, sx, sz);
        Assert.Equal((under - 30f) * 2f + 30f, Sea(deeper, sx, sz), 3);
    }

    [Fact]
    public void TheWaterLevelMovesTheStreamsWindow()
    {
        // The streams' start and end windows follow the water level: with the water 10 m lower (and nothing else
        // changed) other streams are laid out than at 30 m.
        var plain = Generator();
        var low = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { WaterLevel = 20f } });
        Assert.NotEmpty(plain.GetStreams());
        Assert.NotEqual(plain.GetStreams().Select(s => s.P0), low.GetStreams().Select(s => s.P0));
        var none = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { Rivers = false, Streams = false } });
        Assert.Empty(none.GetRivers());
        Assert.Empty(none.GetStreams());
        Assert.NotEmpty(plain.GetRivers());
    }
}
