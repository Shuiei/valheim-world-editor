using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biome point map with Expand World Data installed: the uniform draw among a set's flags, the mod's indices, and
/// a biome the game does not have listed and drawn from.
/// </summary>
public class SeedLocationsBiomeMapDataTests
{
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    [Fact]
    public void TheUniformDrawPicksAnyFlagOfTheSet()
    {
        ValheimGen.UnityEngine.Random.InitState(7);
        var set = Heightmap.Biome.Meadows | Heightmap.Biome.Swamp | Custom;
        var drawn = Enumerable.Range(0, 300).Select(_ => SeedLocations.BiomeMap.RandomBiome(set, uniform: true)).ToHashSet();
        Assert.Equal([Heightmap.Biome.Meadows, Heightmap.Biome.Swamp, Custom], drawn.OrderBy(b => b));
        // The game's walk never reaches a biome it does not know.
        ValheimGen.UnityEngine.Random.InitState(7);
        Assert.DoesNotContain(Custom, Enumerable.Range(0, 100).Select(_ => SeedLocations.BiomeMap.RandomBiome(set, uniform: false)));
        // A single biome, or none: no draw either way.
        Assert.Equal(Custom, SeedLocations.BiomeMap.RandomBiome(Custom, uniform: true));
        Assert.Equal(Heightmap.Biome.None, SeedLocations.BiomeMap.RandomBiome(Heightmap.Biome.None, uniform: true));
    }

    [Fact]
    public void ACustomBiomeIsIndexedListedAndDrawnFrom()
    {
        var near = new BiomeRule { Biome = Custom, SeedBiome = Custom, MaxDistance = 500f, WiggleDistanceWidth = 0f };
        var rest = new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows, WiggleDistanceWidth = 0f };
        var table = new BiomeTable([.. BiomeTable.Vanilla.Biomes, new BiomeInfo { Value = Custom, Id = "Middle", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows }]);
        var gen = WorldGenerator.Create(new World { Seed = 4242, WorldGenVersion = 2, Rules = new WorldRules { DataInstalled = true, World = [near, rest], Biomes = table, BiomeData = true } });
        var map = new SeedLocations.BiomeMap(gen, true, TestContext.Current.CancellationToken);
        Assert.Equal(11, map.IndexOf(Custom));
        Assert.Equal(-1, SeedLocations.BiomeMap.Index(Custom));
        map.List(11);
        Assert.NotEmpty(map.All[11]);
        ValheimGen.UnityEngine.Random.InitState(4242);
        var (x, z) = map.RandomPoint(Custom, aboveSea: false)!.Value;
        Assert.True(x * x + z * z < 520f * 520f, $"{x}, {z}");
    }
}
