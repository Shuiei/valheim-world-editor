using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SeedMapRun;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Desktop with Expand World's rules: a biome of the biome file named and coloured as the file says, the seed map
/// covering a wider world, the New world page saying what its worlds follow, and the start page's tag on worlds a world
/// rules provider says a mod made.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldDesktopTests
{
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    /// <summary>The game's biomes and one of the file's, with these settings.</summary>
    private static BiomeTable Table(BiomeInfo custom) => new([.. BiomeTable.Vanilla.Biomes, custom]);

    [Fact]
    public void ABiomeOfTheFileIsNamedAsTheFileNamesIt()
    {
        var table = Table(new BiomeInfo { Value = Custom, Id = "Lava Fields", Terrain = Heightmap.Biome.AshLands, Nature = Heightmap.Biome.AshLands });
        Assert.Equal("Lava Fields", ZoneFilter.BiomeName(Custom, table).Shown);
        Assert.Equal("Lava Fields", NewWorldPage.Name(Custom, table));
        var named = Table(new BiomeInfo { Value = Custom, Id = "Lava Fields", Name = "The Burning Plains", Terrain = Heightmap.Biome.AshLands, Nature = Heightmap.Biome.AshLands });
        Assert.Equal("The Burning Plains", ZoneFilter.BiomeName(Custom, named).Shown);
        // The game's own keep their names; without the table, a flag is its number.
        Assert.Equal("Meadows", ZoneFilter.BiomeName(Heightmap.Biome.Meadows, table).English);
        Assert.Equal("1024", ZoneFilter.BiomeName(Custom).Shown);
    }

    [Fact]
    public void ABiomesGroundColourComesFromTheFileWhenItHasSettings()
    {
        var coloured = Table(new BiomeInfo { Value = Custom, Id = "Red", Terrain = Heightmap.Biome.Meadows, Nature = Heightmap.Biome.Meadows, HasData = true, TerrainColor = new Rgba(1f, 0f, 0.5f, 1f) });
        Assert.Equal([255, 0, 128, 255], WorldScene.BiomeRgba((int)Custom, coloured));
        // Settings without a colour: black, the Meadows' look (as the mod gives it); no settings: the game's own.
        var plain = Table(new BiomeInfo { Value = Custom, Id = "Plain", Terrain = Heightmap.Biome.Swamp, Nature = Heightmap.Biome.Swamp, HasData = true });
        Assert.Equal([0, 0, 0, 0], WorldScene.BiomeRgba((int)Custom, plain));
        Assert.Equal(WorldScene.BiomeRgba((int)Heightmap.Biome.Swamp), WorldScene.BiomeRgba((int)Heightmap.Biome.Swamp, coloured));
        Assert.Equal([0, 0, 0, 0], WorldScene.BiomeRgba((int)Custom, BiomeTable.Vanilla));
    }

    [AvaloniaFact]
    public void TheSeedMapCoversAWiderWorld()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        Assert.Equal(SeedPreview.Radius, map.Radius);
        map.Show(SeedPreview.Make("abc", 16, rules: new WorldRules { Radius = 20000f }, cancel: TestContext.Current.CancellationToken));
        Assert.Equal(20000f, map.Radius);
        Assert.Equal(2 * 20000f / 300, map.MetersPerPixel, 3);
        // The view stays in the wider world: a look past 10 km is kept, one past 20 km held at the edge.
        map.LookAt(15000f, -15000f, 4);
        Assert.Equal((15000f, -15000f), (map.CenterX, map.CenterZ));
        map.LookAt(25000f, 0f, 4);
        Assert.Equal(20000f, map.CenterX);
    }

    [AvaloniaFact]
    public void TheNewWorldPageSaysWhatItsWorldsFollow()
    {
        using var ruled = new NewWorldPage([], previewSize: 16, rules: new WorldRules { SizeInstalled = true, Radius = 20000f });
        Assert.True(ruled.Generation.IsVisible);
        Assert.Contains("Expand World", ruled.Generation.Text);
        Assert.Contains("radius 20000", ruled.Generation.Text);
        Assert.Equal(20000f, ruled.Rules.Radius);
        using var plain = new NewWorldPage([], previewSize: 16, rules: WorldRules.Vanilla);
        Assert.False(plain.Generation.IsVisible);
    }

    [AvaloniaFact]
    public async Task AWorldMadeWithExpandWorldSizeIsTaggedOnItsCard()
    {
        using var r = new Run();
        string dir = Fixtures.CopyFixture();
        try
        {
            r.P.FindWorlds = _ =>
            [
                new Worlds.Info("Tagged", dir, DateTime.Now, 2, "local", true, null, MadeBy: "Expand World"),
                new Worlds.Info("Plain", dir, DateTime.Now, 2, "local", true, null),
            ];
            r.P.FillWorlds();
            await r.P.WorldsCheck!;
            string[] cards = [.. r.P.WorldCards.Children.OfType<Avalonia.Controls.Button>().Select(Texts)];
            Assert.Equal(2, cards.Length);
            Assert.Contains("Expand World", cards[0]);
            Assert.DoesNotContain("Expand World", cards[1]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
