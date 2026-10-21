using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The seed preview, the map's texels and a new world with a world generation mod's rules: the preview covers the
/// rules' radius, a biome the game does not have shows as the one whose ground it borrows, the overview's texels
/// grow with the world, a world is made with the rules (no spawners when they say so), and a search with Riverheim
/// looks at fewer seeds at once.
/// </summary>
public class SeedPreviewRulesTests
{
    [Fact]
    public void ASearchLooksAtTwoRiverheimSeedsAtATime()
    {
        // Riverheim lays each seed's world out whole first: a couple at a time, not one a processor.
        Assert.Equal(2, SeedPreview.SeedsAtOnce(new WorldRules { Generator = new GeneratorRules(Riverheim.PluginId, "Riverheim", "1.2.1", "builtin.amur v2") { Slow = true } }));
        // A generator that lays its worlds out quickly: one seed a processor.
        Assert.Equal(Math.Max(1, Environment.ProcessorCount - 1), SeedPreview.SeedsAtOnce(new WorldRules { Generator = new GeneratorRules("made.up", "Made Up", "1.0.0", "default") }));
        Assert.Equal(Math.Max(1, Environment.ProcessorCount - 1), SeedPreview.SeedsAtOnce(new WorldRules { Radius = 20000f }));
        Assert.Equal(Math.Max(1, Environment.ProcessorCount - 1), SeedPreview.SeedsAtOnce(null));
    }

    [Fact]
    public void APreviewCoversTheRulesRadius()
    {
        var wide = SeedPreview.Make("abc", 16, rules: new WorldRules { Radius = 20000f }, cancel: TestContext.Current.CancellationToken);
        Assert.Equal(20000f, wide.Radius);
        Assert.Equal(2500f, wide.Cell);
        Assert.Equal((-18750f, -18750f), wide.CellCenter(0, 0));
        Assert.Equal(20000f, wide.Generator!.Rules.Radius);
        // The game's own: 10 km, as before.
        var plain = SeedPreview.Make("abc", 16, cancel: TestContext.Current.CancellationToken);
        Assert.Equal(SeedPreview.Radius, plain.Radius);
        Assert.Equal(1250f, plain.Cell);
        // Land past 10 km: the wide world has some, the plain one none (its cells there are outside).
        Assert.Contains(wide.Height, h => !float.IsNegativeInfinity(h));
        Assert.Equal(SeedPreview.Outside, plain.Height[0]);
    }

    [Fact]
    public void ABiomeOfTheFileShowsAsTheGroundItBorrows()
    {
        var custom = (Heightmap.Biome)0x400;
        var table = new BiomeTable([.. BiomeTable.Vanilla.Biomes, new BiomeInfo { Value = custom, Id = "Highlands", Terrain = Heightmap.Biome.Mountain, Nature = Heightmap.Biome.Mountain }]);
        var rules = new WorldRules
        {
            DataInstalled = true,
            BiomeData = true,
            Biomes = table,
            World = [new BiomeRule { Biome = custom, SeedBiome = Heightmap.Biome.Mountain, MaxDistance = 2000f, WiggleDistanceWidth = 0f }, new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows }],
        };
        var p = SeedPreview.Make("abc", 32, rules: rules, cancel: TestContext.Current.CancellationToken);
        int mountain = Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Mountain);
        // The middle cells (within 2 km, on land) are the custom biome: counted as Mountain.
        int middle = 16 * 32 + 16;
        Assert.True(p.Biome[middle] == mountain || p.Biome[middle] == SeedPreview.Ocean);
        Assert.Contains(p.Biome, b => b == mountain);
    }

    [Fact]
    public void TheOverviewsTexelsGrowWithTheWorld()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var plain = new MapData(new TerrainService(world, null, overviewSize: 64), new TerrainEditor.Editing.EditStore(world));
        Assert.Equal(MapData.GlobalPixel, plain.TexelMeters);
        world.Rules = new WorldRules { Radius = 20500f };
        var wide = new MapData(new TerrainService(world, null, overviewSize: 64), new TerrainEditor.Editing.EditStore(world));
        Assert.Equal(24f, wide.TexelMeters);
    }

    [Fact]
    public void ANewWorldFollowsTheRules()
    {
        string folder = Path.Combine(Path.GetTempPath(), "vwe-new-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var made = WorldCreator.Create(folder, "Ruled", "abc", radius: 1, rules: new WorldRules { DataInstalled = true, ZoneSpawners = false });
            Assert.Equal(9, made.Zones);
            var save = WorldSave.Load(folder);
            // No zone control among the objects, and no settings of the mod in the world file.
            Assert.DoesNotContain(save.Objects, o => o.Prefab == StableHash.Of("_ZoneCtrl"));
            Assert.Empty(save.StartingGlobalKeys);
            Assert.True(save.Rules.IsVanilla);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
