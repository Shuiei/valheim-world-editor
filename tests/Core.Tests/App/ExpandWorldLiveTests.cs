using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A live world's rules (ExpandWorld.ResolveLive): the mods' settings and data files as the game's bridge serves them,
/// the world's keys from its file on the game's disk, the blueprint files the location file names; the local BepInEx
/// folders with the switch off, with an older bridge, or when the bridge fails.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldLiveTests : AppSettingsTestBase
{
    public ExpandWorldLiveTests()
    {
        AppSettings.DataDirOverride = Path.Combine(Data, "editor");
    }

    /// <summary>A local BepInEx folder with Expand World Size's settings file and the YamlDotNet library under plugins.</summary>
    private string Local(string sizeCfg)
    {
        string bep = Path.Combine(Data, "local", "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        YamlStandIn.Install(bep);
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_size.cfg"), sizeCfg);
        return bep;
    }

    /// <summary>A world file's bytes with these starting global keys.</summary>
    private byte[] Meta(params string[] keys)
    {
        string path = Path.Combine(Data, "meta.fwl");
        WorldSaveLoadTests.WriteMetadata(path, 32, keys);
        return File.ReadAllBytes(path);
    }

    /// <summary>Expand World Size as the bridge lists it among the game's plugins.</summary>
    private static readonly (string, string, string) Size = ("expand_world_size", "Expand World Size", "1.45.0");

    private const string LocalCfg = "[1. General]\nWorld radius = 15000\n";

    /// <summary>The world's rules with the switch on and the local folder of <see cref="LocalCfg"/>, through this game's bridge.</summary>
    private async Task<WorldRules> Resolve(WorldSave save, FakeGame game)
    {
        using var live = new LiveBridge(game.Url, game.Token);
        return await ExpandWorld.ResolveLive(save, new AppSettings { ExpandWorld = true, ExpandWorldFolder = Local(LocalCfg) }, live);
    }

    [Fact]
    public async Task TheSwitchOffGivesTheGamesOwnButTakesTheWorldsKeys()
    {
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame { Plugins = [Size], WorldMeta = Meta("ews_world_radius 25000") };
        using var live = new LiveBridge(game.Url, game.Token);
        var rules = await ExpandWorld.ResolveLive(save, new AppSettings { ExpandWorldFolder = Local(LocalCfg) }, live);
        Assert.True(rules.IsVanilla);
        Assert.Equal(["ews_world_radius 25000"], save.StartingGlobalKeys);
        Assert.Equal(0, game.ConfigReads);
    }

    [Fact]
    public async Task AnOlderBridgeLeavesTheLocalFolders()
    {
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame();
        var rules = await Resolve(save, game);
        Assert.Equal(15000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        Assert.Empty(save.StartingGlobalKeys);
    }

    [Fact]
    public async Task TheGamesFilesAreReadThroughTheBridge()
    {
        Fixtures.UseLocations();
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame
        {
            Plugins = [Size, ("expand_world_data", "Expand World Data", "1.79.0")],
            WorldMeta = Meta("ews_world_radius 25000"),
        };
        game.AddConfig("expand_world_size.cfg", "[1. General]\nWorld radius = 20000\n");
        // Rivers is not loaded: its settings file is not read.
        game.AddConfig("expand_world_rivers.cfg", "[1. General]\nWater level = 40\n");
        game.AddConfig("expand_world_data.cfg", "[3. Data]\nBiome data = true\nLocation data = true\nBlueprint folder = Prints\n");
        game.AddConfig("expand_world/expand_biomes.yaml", "- biome: Highlands\n  terrain: mountain\n");
        game.AddConfig("expand_world/more/expand_locations.yaml", "- prefab: MyHut\n  biome: Meadows\n  quantity: 5\n");
        game.AddConfig("Prints/MyHut.vbuild", "woodwall 0 0 0 1 10 0 -10\nwood_floor 0 0 0 1 -10 3 10\n");
        game.AddConfig("Prints/Other.vbuild", "woodwall 0 0 0 1 0 0 0\n");
        var rules = await Resolve(save, game);
        // The world's keys win over the settings file, as for a world opened from its files.
        Assert.Equal(25000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        Assert.True(rules.DataInstalled);
        Assert.False(rules.RiversInstalled);
        Assert.Equal(30f, rules.WaterLevel);
        // The data files, read through the local library.
        Assert.True(rules.BiomeData);
        Assert.Contains(rules.Biomes.Biomes, b => b.Id == "Highlands");
        Assert.NotNull(rules.Locations);
        Assert.True(Assert.Single(rules.Locations.Locations, l => l.Prefab == "MyHut").Blueprint);
        // Copied as a BepInEx folder: the loaded mods' settings, the yaml files, the blueprint named (not the other).
        string config = Path.Combine(ExpandWorld.LiveFolder, "config");
        Assert.True(File.Exists(Path.Combine(config, "expand_world_size.cfg")));
        Assert.False(File.Exists(Path.Combine(config, "expand_world_rivers.cfg")));
        Assert.True(File.Exists(Path.Combine(config, "expand_world", "more", "expand_locations.yaml")));
        Assert.True(File.Exists(Path.Combine(config, "Prints", "MyHut.vbuild")));
        Assert.False(File.Exists(Path.Combine(config, "Prints", "Other.vbuild")));
    }

    [Fact]
    public async Task AWorldFileTheBridgeCannotServeLeavesTheKeysButNotTheFiles()
    {
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame { Plugins = [Size] };
        game.AddConfig("expand_world_size.cfg", "[1. General]\nWorld radius = 20000\n");
        game.Fail["/world/meta"] = 500;
        var rules = await Resolve(save, game);
        // The keys stay as the game loaded them (none here); the settings file is still the game's, not the local one.
        Assert.Equal(20000f, rules.Radius);
        Assert.True(File.Exists(Path.Combine(ExpandWorld.LiveFolder, "config", "expand_world_size.cfg")));
    }

    [Fact]
    public async Task ABridgeThatFailsLeavesTheLocalFolders()
    {
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame { Plugins = [Size] };
        game.Fail["/config"] = 500;
        var rules = await Resolve(save, game);
        Assert.Equal(15000f, rules.Radius);
    }
}
