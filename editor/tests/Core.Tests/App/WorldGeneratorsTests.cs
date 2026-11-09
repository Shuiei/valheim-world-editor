using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// World generation mods' generators an extension registers (WorldGenerators, ModGenerator), with a made-up one: a world
/// file naming it read and written by it, one Worldhalla keeps read by the one registered for it (a newer Worldhalla's
/// refused), its ground laid out in place of the game's; what it refuses said in its
/// words; a fault turning its extension off and said as the add-on failing; the world list marking its worlds, and
/// refusing those of a generator none draws; new worlds made with it; the rules' few words naming it; the world's Expand
/// World settings as it is told them. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class WorldGeneratorsTests : AppSettingsTestBase, IDisposable
{
    /// <summary>Forgets the extensions in use, so the next test starts without any.</summary>
    public new void Dispose()
    {
        ModExtensions.Forget();
        base.Dispose();
    }

    /// <summary>Puts a Flatland in use and gives its generator.</summary>
    private static ModGenerator Use(FlatlandGenerator flatland)
    {
        ModExtensions.UseForTests(("Flatland", flatland));
        return Assert.IsType<ModGenerator>(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void AWorldItMadeIsReadAndWrittenByItAndLaidOutWithItsGround()
    {
        var g = Use(new FlatlandGenerator());
        var rules = new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(50f), null) };
        Assert.Equal(("Flatland", "2.0.0", "height 50", false), (rules.Generator.Name, rules.Generator.Version, rules.Generator.Settings, rules.Generator.Slow));
        string folder = Path.Combine(Data, "Flat");
        WorldCreator.Create(folder, "Flat", "abcde", rules: rules);
        var head = WorldSave.ReadMetadata(Path.Combine(folder, "_main.1.fwl2"));
        Assert.Equal(("test.flatland", 50f, "Flat", "abcde"), (head.Generator?.Generator.Id, head.Generator?.Settings.Value, head.Name, head.SeedName));
        var gen = WorldGenerator.Create(new World { Seed = 1, Rules = rules });
        Assert.Equal((Heightmap.Biome.Plains, 50f), (gen.GetBiome(100f, 200f), gen.GetHeight(100f, 200f)));
    }

    [Fact]
    public void WhatItRefusesIsSaidInItsWordsAndAFaultTurnsItsExtensionOff()
    {
        var flatland = new FlatlandGenerator();
        var g = Use(flatland);
        string folder = Path.Combine(Data, "Sunk");
        WorldCreator.Create(folder, "Sunk", "abcde", rules: new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(-1f), null) });
        Assert.Equal("A negative height.", Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(Path.Combine(folder, "_main.1.fwl2"))).Message);
        flatland.Faulty = true;
        Assert.Equal(Lang.T("worldgen.failed", "Flatland"), g.Problem(FlatlandGenerator.Settings(50f)));
        Assert.Null(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void AFaultAnsweringForAPointIsSaidAndTurnsItsExtensionOff()
    {
        var g = Use(new FlatlandGenerator());
        var gen = WorldGenerator.Create(new World { Seed = 1, Rules = new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(13f), null) } });
        Assert.Equal(Lang.T("worldgen.failed", "Flatland"), Assert.Throws<InvalidOperationException>(() => gen.GetHeight(100f, 200f)).Message);
        Assert.Null(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void NewWorldsAreMadeWithAGeneratorThatMakesThem()
    {
        var settings = new AppSettings();
        ModExtensions.Forget();
        Assert.True(WorldGenerators.ForNewWorld(settings).IsVanilla);
        Use(new FlatlandGenerator());
        var made = WorldGenerators.ForNewWorld(settings).Generator;
        Assert.Equal(("Flatland", "height 50", 50f), (made?.Name, made?.Settings, (made?.Header as GeneratorHeader)?.Settings.Value));
    }

    [Fact]
    public void AGeneratorIsToldTheWorldsExpandWorldSettings()
    {
        var plain = WorldGenerators.ExpandWorldOf(WorldRules.Vanilla);
        Assert.Equal((1f, 1f, 10500f, false, false, false), (plain.Stretch, plain.BiomeStretch, plain.Radius, plain.ShapesGround, plain.WorldData, plain.Rivers));
        var ew = WorldGenerators.ExpandWorldOf(new WorldRules { SizeInstalled = true, Radius = 20000f, Stretch = 2f, BiomeStretch = 3f, HeightSeed = 7f, World = [], RiversInstalled = true });
        Assert.Equal((2f, 3f, 20500f, true, true, true), (ew.Stretch, ew.BiomeStretch, ew.Radius, ew.ShapesGround, ew.WorldData, ew.Rivers));
    }

    [Fact]
    public void TheWorldListMarksItsWorldsAndSaysWhyOneCannotOpen()
    {
        var g = Use(new FlatlandGenerator());
        string flat = Path.Combine(Data, "flat"), high = Path.Combine(Data, "high");
        WorldCreator.Create(Path.Combine(flat, "Flat"), "Flat", "abcde", rules: new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(50f), null) });
        WorldCreator.Create(Path.Combine(high, "High"), "High", "abcde", rules: new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(2000f), null) });
        var listed = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { flat } }));
        Assert.Equal(("Flatland", true), (listed.Generator, listed.Usable));
        var refused = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { high } }));
        Assert.Equal(("Flatland", false, FlatlandGenerator.TooHigh), (refused.Generator, refused.Usable, refused.Problem));
    }

    [Fact]
    public void AWorldOfAGeneratorNoneDrawsIsListedAsNotUsable()
    {
        ModExtensions.Forget();
        using var w = new TempWorld();
        WorldFileHead.Write(Path.Combine(w.Dir, $"_main.{w.Load().SaveNumber}.fwl2"), 42, [], "made.up.generator");
        var info = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { w.Dir } }));
        Assert.Null(info.Generator);
        Assert.Equal((false, Lang.T("worldgen.unknown", "made.up.generator")), (info.Usable, info.Problem));
    }

    [Fact]
    public void AWorldWorldhallaKeepsIsReadByTheGeneratorRegisteredForIt()
    {
        ModExtensions.UseForTests(("Flatland", new FlatlandGenerator { Id = "worldhalla:test.flatland" }));
        using var w = new TempWorld();
        string file = Path.Combine(w.Dir, $"_main.{w.Load().SaveNumber}.fwl2");
        byte[] height = BitConverter.GetBytes(50f);
        // Worldhalla 1.0 writes its own settings as none, or a count of none.
        foreach (byte[] own in new byte[][] { [], BitConverter.GetBytes(0) })
        {
            WorldFileHead.WriteWorldhalla(file, "test.flatland", height, own);
            var head = WorldSave.ReadMetadata(file);
            Assert.Equal(("worldhalla:test.flatland", 50f, WorldFileHead.Name, WorldFileHead.SeedName), (head.Generator?.Generator.Id, head.Generator?.Settings.Value, head.Name, head.SeedName));
        }
        // A newer Worldhalla's format or settings of its own, as an older Worldhalla refuses them.
        WorldFileHead.WriteWorldhalla(file, "test.flatland", height, format: 1);
        Assert.Equal(Lang.T("worldgen.worldhallaFormat", 1), Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(file)).Message);
        WorldFileHead.WriteWorldhalla(file, "test.flatland", height, own: [1, 0, 0, 0, 1, (byte)'x']);
        Assert.Equal(Lang.T("worldgen.worldhallaSettings"), Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(file)).Message);
        // Settings the generator does not read whole, and a generator none registers for Worldhalla's worlds.
        WorldFileHead.WriteWorldhalla(file, "test.flatland", [.. height, 7]);
        Assert.Equal(Lang.T("worldgen.worldhallaUnread", "Flatland"), Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(file)).Message);
        WorldFileHead.WriteWorldhalla(file, "made.up", height);
        Assert.Equal(Lang.T("worldgen.unknown", "worldhalla:made.up"), Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(file)).Message);
        // Its id is not the one a world names after the -1 a mod marks its own worlds with.
        WorldFileHead.Write(file, 42, [], "test.flatland");
        Assert.Equal(Lang.T("worldgen.unknown", "test.flatland"), Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(file)).Message);
    }

    [Fact]
    public void TheRulesFewWordsNameTheGenerator()
    {
        Assert.Contains("Flatland 2.0.0 (height 50)", WorldRulesText.Describe(new WorldRules { Generator = new GeneratorRules("test.flatland", "Flatland", "2.0.0", "height 50") }));
        Assert.DoesNotContain("Flatland", WorldRulesText.Describe(new WorldRules { Radius = 20000f }));
    }
}
