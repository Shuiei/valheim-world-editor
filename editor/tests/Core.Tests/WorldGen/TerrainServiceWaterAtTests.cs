using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// <see cref="TerrainService.WaterAt"/>: the water's colours at a point as Expand World Data gives them, only with its
/// "Custom water color" on, a biome's only when the file gives it settings, the territory's over them.
/// </summary>
[Collection("World files")]
public class TerrainServiceWaterAtTests
{
    private static readonly Rgba Red = new(1f, 0f, 0f, 1f), Blue = new(0f, 0f, 1f, 1f);

    private static readonly BiomeInfo RedMeadows = new()
    {
        Value = Heightmap.Biome.Meadows,
        Id = "Meadows",
        Terrain = Heightmap.Biome.Meadows,
        Nature = Heightmap.Biome.Meadows,
        HasData = true,
        Water = new WaterColours(Top: Red, Bottom: Red),
    };

    /// <summary>Meadows everywhere with red water, a territory 500 m round the middle with a blue bottom.</summary>
    private static readonly WorldRules Rules = new()
    {
        DataInstalled = true,
        BiomeData = true,
        CustomWaterColor = true,
        Biomes = new BiomeTable([.. BiomeTable.Vanilla.Biomes.Select(b => b.Value == Heightmap.Biome.Meadows ? RedMeadows : b)]),
        World = [new BiomeRule { Territory = "blue", MaxDistance = 500f, WiggleDistanceWidth = 0f }, new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows }],
        Territories = [new Territory { Name = "blue", Water = new WaterColours(Bottom: Blue) }],
    };

    private static TerrainService Terrain(TempWorld w, WorldRules rules)
    {
        var world = w.Load();
        world.Rules = rules;
        return new TerrainService(world, null);
    }

    [Fact]
    public void TheTerritorysColoursCoverTheBiomes()
    {
        using var w = new TempWorld();
        var terrain = Terrain(w, Rules);
        Assert.Equal(new WaterColours(Top: Red, Bottom: Blue), terrain.WaterAt(100f, 100f));
        Assert.Equal(RedMeadows.Water, terrain.WaterAt(2000f, 0f));
    }

    [Fact]
    public void ABiomeWithoutSettingsGivesNoColour()
    {
        using var w = new TempWorld();
        var plain = Rules with { Biomes = new BiomeTable([.. BiomeTable.Vanilla.Biomes.Select(b => b.Value == Heightmap.Biome.Meadows ? RedMeadows with { HasData = false } : b)]) };
        var terrain = Terrain(w, plain);
        Assert.Equal(WaterColours.None, terrain.WaterAt(2000f, 0f));
        Assert.Equal(new WaterColours(Bottom: Blue), terrain.WaterAt(100f, 100f));
    }

    [Fact]
    public void WithTheSwitchOffTheWaterIsTheGames()
    {
        using var w = new TempWorld();
        Assert.Same(WaterColours.None, Terrain(w, Rules with { CustomWaterColor = false }).WaterAt(100f, 100f));
    }
}
