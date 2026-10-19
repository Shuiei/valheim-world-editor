using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ground with Expand World Data's biome file (WorldGenerator.Heights.Data.cs): a biome borrows its terrain
/// biome's shape, its height transform lies over it, its paint replaces the mask, its lava is thinned, and an Ashlands
/// or Mistlands biome on other ground keeps its own mask.
/// </summary>
public class WorldGeneratorDataHeightTests
{
    private const int Seed = 12345;
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    private static WorldGenerator Generator(WorldRules? rules = null) =>
        WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla });

    /// <summary>A generator with the biome file on: the game's nine (as given) and one custom biome.</summary>
    private static WorldGenerator WithBiomes(BiomeInfo custom, params BiomeInfo[] games)
    {
        var biomes = BiomeTable.Vanilla.Biomes.Select(b => games.FirstOrDefault(g => g.Value == b.Value) ?? b).Append(custom).ToList();
        return Generator(new WorldRules { DataInstalled = true, BiomeData = true, Biomes = new BiomeTable(biomes) });
    }

    [Fact]
    public void ABiomeBorrowsItsTerrainsGroundAndLaysItsTransformOverIt()
    {
        var highlands = new BiomeInfo { Value = Custom, Id = "Highlands", Terrain = Heightmap.Biome.Mountain, Nature = Heightmap.Biome.Mountain, HasData = true, Height = new HeightTransform { Delta = 20f } };
        var g = WithBiomes(highlands);
        var plain = Generator();
        foreach (var (x, z) in GeneratorPoints.Rings(500, 4000, 700, 8))
        {
            Assert.Equal(plain.GetBiomeHeight(Heightmap.Biome.Mountain, x, z, out _) + 20f, g.GetBiomeHeight(Custom, x, z, out _), 2);
        }
    }

    [Fact]
    public void TheBiomeFilesRoundingIsTheOnlyChangeForABiomeWithoutSettings()
    {
        // The mod's postfix takes the water level off and puts it back: the same height but for its rounding.
        var g = WithBiomes(new BiomeInfo { Value = Custom, Id = "Plain", Terrain = Custom, Nature = Custom });
        var plain = Generator();
        foreach (var (x, z) in GeneratorPoints.Rings(500, 4000, 700, 8))
        {
            Assert.Equal(plain.GetHeight(x, z), g.GetHeight(x, z), 3);
        }
        // A biome with no shape of its own is flat at the water level, as the game's unknown biome is at 0.
        Assert.Equal(0f, g.GetBiomeHeight(Custom, 100f, 100f, out _), 3);
    }

    [Fact]
    public void APaintReplacesTheMask()
    {
        var painted = new BiomeInfo { Value = Custom, Id = "Painted", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, Paint = new Rgba(1f, 0f, 0f, 1f) };
        WithBiomes(painted).GetBiomeHeight(Custom, 100f, 100f, out var mask);
        Assert.Equal(new Rgba(1f, 0f, 0f, 1f).ToColor(), mask);
    }

    [Fact]
    public void LavaIsThinnedByTheLavaAmount()
    {
        var thin = new BiomeInfo { Value = Custom, Id = "Embers", Terrain = Heightmap.Biome.AshLands, Nature = Heightmap.Biome.AshLands, Lava = true, HasData = true, LavaAmount = 0.5f };
        var g = WithBiomes(thin);
        var plain = Generator();
        var points = GeneratorPoints.Rings(8600, 9800, 300, 12).Where(p => p.Z < -8000f).ToList();
        Assert.NotEmpty(points);
        foreach (var (x, z) in points)
        {
            float h = g.GetBiomeHeight(Custom, x, z, out var mask);
            Assert.InRange(mask.A, 0f, 1f);
            Assert.Equal(plain.GetBiomeHeight(Heightmap.Biome.AshLands, x, z, out _) - mask.A, h, 2);
        }
        // Some of the lava is thinned away, some kept.
        Assert.Contains(points, p => { g.GetBiomeHeight(Custom, p.X, p.Z, out var m); return m.A == 0f; });
        Assert.Contains(points, p => { g.GetBiomeHeight(Custom, p.X, p.Z, out var m); return m.A > 0f; });
    }

    [Fact]
    public void TheAshlandsOnOtherGroundKeepTheirOwnMask()
    {
        var ash = new BiomeInfo { Value = Heightmap.Biome.AshLands, Id = "AshLands", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, Lava = true };
        var g = WithBiomes(new BiomeInfo { Value = Custom, Id = "Other", Terrain = Custom, Nature = Custom }, ash);
        var plain = Generator();
        float h = g.GetBiomeHeight(Heightmap.Biome.AshLands, 100f, -9000f, out var mask);
        Assert.Equal(plain.GetBiomeHeight(Heightmap.Biome.Meadows, 100f, -9000f, out _), h, 3);
        plain.GetBiomeHeight(Heightmap.Biome.AshLands, 100f, -9000f, out var own);
        Assert.Equal(own, mask);
    }
}
