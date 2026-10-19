using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The seed preview's cell colours with a world generation mod's rules: a biome of Expand World Data's file and a
/// territory colour their cells as the world's map does, the sea level is the rules', and the game's own rules give no
/// colours of their own.
/// </summary>
public class SeedPreviewColorsTests
{
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    [Fact]
    public void TheGamesOwnRulesGiveNoColours()
    {
        var p = SeedPreview.Make("abc", 16, cancel: TestContext.Current.CancellationToken);
        Assert.Null(p.Color);
        Assert.Null(p.CellColor(0));
        Assert.Equal(30f, p.Water);
        Assert.Null(SeedPreview.SampleCells(SeedPreview.Generator("abc"), -100f, -100f, 10f, 4, 4, parallel: false, TestContext.Current.CancellationToken).Color);
    }

    [Fact]
    public void ABiomeOfTheFileAndATerritoryColourTheirCells()
    {
        var table = new BiomeTable([.. BiomeTable.Vanilla.Biomes, new BiomeInfo { Value = Custom, Id = "Highlands", Terrain = Heightmap.Biome.Mountain, Nature = Heightmap.Biome.Mountain, HasData = true, MapColor = new Rgba(0.75f, 0.85f, 1f, 1f) }]);
        var rules = new WorldRules
        {
            DataInstalled = true,
            BiomeData = true,
            Biomes = table,
            WaterLevel = 27f,
            World =
            [
                new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows, Territory = "red", MaxDistance = 2000f, WiggleDistanceWidth = 0f },
                new BiomeRule { Biome = Custom, SeedBiome = Heightmap.Biome.Mountain, MinDistance = 3000f, MaxDistance = 6000f, WiggleDistanceWidth = 0f },
                new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows },
            ],
            Territories = [new Territory { Name = "red", MapColor = new Rgba(0.85f, 0.3f, 0.2f, 1f) }],
        };
        var p = SeedPreview.Make("abc", 64, rules: rules, cancel: TestContext.Current.CancellationToken);
        Assert.NotNull(p.Color);
        Assert.Equal(27f, p.Water);
        int meadows = Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Meadows), mountain = Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Mountain);
        bool red = false, pale = false;
        for (int j = 0; j < p.Size; j++)
        {
            for (int i = 0; i < p.Size; i++)
            {
                int k = j * p.Size + i;
                var (x, z) = p.CellCenter(i, j);
                float d = MathF.Sqrt(x * x + z * z);
                if (p.Biome[k] == meadows && d < 1800f)
                {
                    Assert.Equal(((byte)217, (byte)77, (byte)51), p.CellColor(k));
                    red = true;
                }
                else if (p.Biome[k] == mountain && d > 3200f && d < 5800f)
                {
                    Assert.Equal(((byte)191, (byte)217, (byte)255), p.CellColor(k));
                    pale = true;
                }
            }
        }
        Assert.True(red, "a meadows cell in the territory");
        Assert.True(pale, "a cell of the file's biome");
    }
}
