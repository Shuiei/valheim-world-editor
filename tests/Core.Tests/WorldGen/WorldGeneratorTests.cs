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
        float g = WorldGenerator.GetAshlandsOceanGradient(10, -9000);
        Assert.True(g > 1);
        Assert.Equal(g, WorldGenerator.GetAshlandsOceanGradient(new Vector2(10, -9000)));
        Assert.Equal(g, WorldGenerator.GetAshlandsOceanGradient(new Vector3(10, 7, -9000)));
    }
}
