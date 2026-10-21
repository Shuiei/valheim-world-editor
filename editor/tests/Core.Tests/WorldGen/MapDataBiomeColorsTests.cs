using TerrainEditor.Editing;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The map's colours with Expand World Data's biome file: a biome's map colour from the file, a territory's over it,
/// the height texture's multiplier, the mask by the terrain biome, and the overview's colour for a custom biome.
/// </summary>
public class MapDataBiomeColorsTests
{
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    /// <summary>The test world's map data under the rules given.</summary>
    private static MapData Map(TempWorld w, WorldRules rules)
    {
        var world = w.Load();
        world.Rules = rules;
        return new MapData(new TerrainService(world, null, overviewSize: 64), new EditStore(world));
    }

    private static BiomeTable Table(params BiomeInfo[] extra) => new([.. BiomeTable.Vanilla.Biomes.Select(b => extra.FirstOrDefault(e => e.Value == b.Value) ?? b), .. extra.Where(e => !BiomeTable.GamesBiomes.Any(g => g.Value == e.Value))]);

    [Fact]
    public void ABiomesMapColourComesFromTheFileWhenItHasSettings()
    {
        using var w = new TempWorld();
        var coloured = new BiomeInfo { Value = Custom, Id = "Violet", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, HasData = true, MapColor = new Rgba(0.5f, 0f, 1f, 1f) };
        var plainMeadows = new BiomeInfo { Value = Heightmap.Biome.Meadows, Id = "Meadows", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, MapColor = new Rgba(0f, 0f, 0f, 1f) };
        var map = Map(w, new WorldRules { DataInstalled = true, BiomeData = true, Biomes = Table(coloured, plainMeadows) });
        Assert.Equal(((byte)128, (byte)0, (byte)255), map.Pixel(Custom, 0f, 0f));
        // A colour given to a biome with no other setting is not applied (the mod only reads the colour with them).
        Assert.Equal(MapData.PixelColor(Heightmap.Biome.Meadows), map.Pixel(Heightmap.Biome.Meadows, 0f, 0f));
        // A biome the file gives nothing to keeps the game's colour; one the game does not know is white.
        Assert.Equal(MapData.PixelColor(Heightmap.Biome.Swamp), map.Pixel(Heightmap.Biome.Swamp, 0f, 0f));
        Assert.Equal(((byte)255, (byte)255, (byte)255), map.Pixel((Heightmap.Biome)0x800, 0f, 0f));
    }

    [Fact]
    public void ATerritorysMapColourCoversTheBiomes()
    {
        using var w = new TempWorld();
        var rules = new WorldRules
        {
            DataInstalled = true,
            World = [new BiomeRule { Territory = "red", MaxDistance = 500f, WiggleDistanceWidth = 0f }, new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows }],
            Territories = [new Territory { Name = "red", MapColor = new Rgba(1f, 0f, 0f, 1f) }],
        };
        var map = Map(w, rules);
        Assert.Equal(((byte)255, (byte)0, (byte)0), map.Pixel(Heightmap.Biome.Meadows, 100f, 100f));
        Assert.Equal(MapData.PixelColor(Heightmap.Biome.Meadows), map.Pixel(Heightmap.Biome.Meadows, 2000f, 0f));
    }

    [Fact]
    public void TheHeightTextureFollowsTheMapColourMultiplierAboveTheWater()
    {
        using var w = new TempWorld();
        var meadows = new BiomeInfo { Value = Heightmap.Biome.Meadows, Id = "Meadows", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, HasData = true, MapColorMultiplier = 2f };
        var map = Map(w, new WorldRules { DataInstalled = true, BiomeData = true, Biomes = Table(meadows) });
        Assert.Equal(70f, map.MapHeight(50f, Heightmap.Biome.Meadows));
        Assert.Equal(10f, map.MapHeight(10f, Heightmap.Biome.Meadows));
        Assert.Equal(50f, map.MapHeight(50f, Heightmap.Biome.Swamp));
        Assert.Equal(50f, Map(w, WorldRules.Vanilla).MapHeight(50f, Heightmap.Biome.Meadows));
    }

    [Fact]
    public void TheMaskFollowsTheTerrainBiome()
    {
        using var w = new TempWorld();
        var dark = new BiomeInfo { Value = Custom, Id = "Dark", Terrain = Heightmap.Biome.BlackForest, Nature = Heightmap.Biome.BlackForest };
        var map = Map(w, new WorldRules { DataInstalled = true, BiomeData = true, Biomes = Table(dark) });
        Assert.Equal(((byte)255, (byte)0, (byte)0), map.MaskColor(0f, 0f, 50f, Custom));
        // Without the biome file, an unknown biome has no mask.
        Assert.Equal(((byte)0, (byte)0, (byte)0), Map(w, WorldRules.Vanilla).MaskColor(0f, 0f, 50f, Custom));
    }

    [Fact]
    public void TheOverviewColoursACustomBiomeByItsMapColourOrItsTerrain()
    {
        var violet = new BiomeInfo { Value = Custom, Id = "Violet", Terrain = Heightmap.Biome.Mountain, Nature = Heightmap.Biome.Mountain, HasData = true, MapColor = new Rgba(0.5f, 0f, 1f, 1f) };
        Assert.Equal((127.5f, 0f, 255f), TerrainService.BiomeColor(Custom, 50f, 30f, Table(violet)));
        Assert.Equal(TerrainService.BiomeColor(Heightmap.Biome.Mountain, 50f, 30f), TerrainService.BiomeColor(Custom, 50f, 30f, Table(violet with { MapColor = null })));
        // One of the game's biomes takes the file's colour too, with other settings; without them it keeps the palette.
        var meadows = violet with { Value = Heightmap.Biome.Meadows, Id = "Meadows", Terrain = Heightmap.Biome.Meadows };
        Assert.Equal((127.5f, 0f, 255f), TerrainService.BiomeColor(Heightmap.Biome.Meadows, 50f, 30f, Table(meadows)));
        Assert.Equal((112f, 158f, 72f), TerrainService.BiomeColor(Heightmap.Biome.Meadows, 50f, 30f, Table(meadows with { HasData = false })));
        Assert.Equal((150f, 150f, 150f), TerrainService.BiomeColor((Heightmap.Biome)0x800, 50f, 30f, Table(violet)));
    }

    [Fact]
    public void TheMapDrawsTheSeaAtTheWorldsRealWaterLevel()
    {
        using var w = new TempWorld();
        var vanilla = Map(w, WorldRules.Vanilla);
        var lower = Map(w, WorldRules.Vanilla with { WaterLevel = 27f });
        // The sea's line half a metre under the water, as the game's map draws its own at 29.5 m.
        Assert.Equal(29.5f, vanilla.SeaLine);
        Assert.Equal(26.5f, lower.SeaLine);
        // Ground at 28 m: under the game's sea, above a sea Expand World Rivers lowers to 27 m (its forest shows).
        Assert.Equal((byte)0, vanilla.MaskColor(0f, 0f, 28f, Heightmap.Biome.BlackForest).Item1);
        Assert.Equal(((byte)255, (byte)0, (byte)0), lower.MaskColor(0f, 0f, 28f, Heightmap.Biome.BlackForest));
    }
}
