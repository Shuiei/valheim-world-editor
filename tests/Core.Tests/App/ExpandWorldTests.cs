using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World's settings as the editor reads them: the world file's keys, the mods' settings files, which mods a
/// BepInEx folder has, the rules made from them, and the Settings switch that keeps every world vanilla while it is
/// off.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldTests : AppSettingsTestBase
{
    [Fact]
    public void TheWorldFilesKeysAreTheModsSettings()
    {
        var keys = ExpandWorld.Keys(["defeated_eikthyr", "ews_world_radius 20000", "EWS_Stretch_World 1.5", "ews_gridwidth 1024", "ews_flag", "ews_world_radius 25000"]);
        Assert.Equal(4, keys.Count);
        // The last of two the same wins, as the mod reads them; the name is lower-cased; a key without a value is empty.
        Assert.Equal("25000", keys["ews_world_radius"]);
        Assert.Equal("1.5", keys["ews_stretch_world"]);
        Assert.Equal("1024", keys["ews_gridwidth"]);
        Assert.Equal("", keys["ews_flag"]);
        Assert.Equal("ews_world_edge_size", ExpandWorld.KeyOf("World edge size"));
    }

    [Fact]
    public void TheSavedGridIsReadFromTheKeysWithinWhatAChunkIdCanName()
    {
        Assert.Equal(512, ExpandWorld.SavedGridWidth([], "w"));
        Assert.Equal(1024, ExpandWorld.SavedGridWidth(["ews_gridwidth 1024"], "w"));
        Assert.Equal(2048, ExpandWorld.SavedGridWidth(["ews_gridwidth 2048"], "w"));
        Assert.Equal(512, ExpandWorld.SavedGridWidth(["ews_gridwidth 256"], "w"));
        Assert.Equal(512, ExpandWorld.SavedGridWidth(["ews_gridwidth 4096"], "w"));
        Assert.Equal(512, ExpandWorld.SavedGridWidth(["ews_gridwidth wide"], "w"));
    }

    [Fact]
    public void ASettingsFileIsReadBySettingName()
    {
        var cfg = ExpandWorld.ReadCfg("## Settings file was created by plugin\n\n[1. General]\n\n## Radius of the world.\n# Setting type: String\nWorld radius = 20000\nStretch world = 2\n\n[2. Poles]\nAshlands gap = false\nBroken line\n= nothing\nWorld radius = 30000\n");
        Assert.Equal("30000", cfg["World radius"]);
        Assert.Equal("2", cfg["Stretch world"]);
        Assert.Equal("false", cfg["Ashlands gap"]);
        Assert.Equal(3, cfg.Count);
    }

    [Fact]
    public void ValuesAreReadAsTheModReadsThem()
    {
        Assert.Equal(1.5f, ExpandWorld.Float("1.5", 1f));
        Assert.Equal(1f, ExpandWorld.Float("", 1f));
        Assert.Equal(1f, ExpandWorld.Float("abc", 1f));
        Assert.Equal(1f, ExpandWorld.Float("1,5", 1f));
        Assert.Null(ExpandWorld.Float(null));
        Assert.Null(ExpandWorld.Float("NaN"));
        Assert.Equal(-20f, ExpandWorld.Float("-20"));
        Assert.Equal(7, ExpandWorld.Int("7"));
        Assert.Null(ExpandWorld.Int("7.5"));
        Assert.False(ExpandWorld.Bool("false", true));
        Assert.True(ExpandWorld.Bool("maybe", true));
    }

    [Theory]
    [InlineData(true, 0, 10500f, 512)]
    [InlineData(true, 0, 16384f, 512)]
    [InlineData(true, 0, 16385f, 576)]
    [InlineData(true, 0, 20500f, 704)]
    [InlineData(true, 0, 70000f, 2048)]
    [InlineData(true, 4, 10500f, 512)]
    [InlineData(true, 16, 10500f, 1024)]
    [InlineData(true, 100, 10500f, 2048)]
    [InlineData(false, 16, 70000f, 512)]
    public void TheSaveGridFollowsTheModsRule(bool sizeInstalled, int saveSystemSize, float total, int expected)
    {
        Assert.Equal(expected, ExpandWorld.GridWidthFor(sizeInstalled, saveSystemSize, total));
    }

    [Fact]
    public void RulesFromTheWorldFilesKeysTakeTheDefaultsForWhatIsNotKept()
    {
        var keys = ExpandWorld.Keys(["ews_world_radius 20000", "ews_altitude_delta 10", "ews_offset_x -500", "ews_ashlands_gap false", "ews_gridwidth 1024"]);
        var r = ExpandWorld.Rules(keys, ExpandWorld.Found.None);
        Assert.Equal(20000f, r.Radius);
        Assert.Equal(500f, r.EdgeSize);
        Assert.Equal(10f, r.AltitudeDelta);
        Assert.Equal(-500f, r.OffsetX);
        Assert.Null(r.OffsetY);
        Assert.False(r.AshlandsGap);
        Assert.Equal(1024, r.GridWidth);
        // A world with the keys was made with Expand World Size, folder or no folder.
        Assert.True(r.SizeInstalled);
        Assert.False(r.RiversInstalled);
        Assert.Equal(30f, r.WaterLevel);
        Assert.False(r.IsVanilla);
    }

    [Fact]
    public void RulesFromTheFoldersFilesWhenTheWorldKeepsNone()
    {
        string bep = Path.Combine(Data, "bep");
        string cfg = Path.Combine(bep, "config");
        Directory.CreateDirectory(cfg);
        Directory.CreateDirectory(Path.Combine(bep, "plugins", "JereKuusela-Expand_World_Size"));
        File.WriteAllText(Path.Combine(bep, "plugins", "JereKuusela-Expand_World_Size", "ExpandWorldSize.dll"), "");
        File.WriteAllText(Path.Combine(cfg, "expand_world_size.cfg"), "[1. General]\nWorld radius = 15000\nStretch world = 1.5\nSave system size = 0\n");
        File.WriteAllText(Path.Combine(cfg, "expand_world_rivers.cfg"), "[1. General]\nWater level = 25\nStreams = false\nStream max amount = 100\nRiver seed = 42\n");
        var found = ExpandWorld.Look(bep, "test");
        Assert.True(found.Size);
        Assert.True(found.Rivers);
        Assert.False(found.Data);
        Assert.NotNull(found.SizeCfg);
        Assert.Null(found.DataFiles);
        var r = ExpandWorld.Rules(new Dictionary<string, string>(), found);
        Assert.Equal(15000f, r.Radius);
        Assert.Equal(1.5f, r.Stretch);
        Assert.Equal(25f, r.WaterLevel);
        Assert.False(r.Streams);
        Assert.Equal(100, r.StreamMaxAmount);
        Assert.Equal(42, r.RiverSeed);
        Assert.Null(r.StreamSeed);
        // 15500 m: 485 zones of 32 m, rounded up to 512.
        Assert.Equal(512, r.GridWidth);
        // The world's keys win over the file for the Size settings; the Rivers settings still come from their file.
        var fromKeys = ExpandWorld.Rules(ExpandWorld.Keys(["ews_world_radius 20000"]), found);
        Assert.Equal(20000f, fromKeys.Radius);
        Assert.Equal(1f, fromKeys.Stretch);
        Assert.Equal(25f, fromKeys.WaterLevel);
        Assert.Equal(704, fromKeys.GridWidth);
    }

    [Fact]
    public void AStretchOfZeroIsTakenAsOne()
    {
        var r = ExpandWorld.Rules(ExpandWorld.Keys(["ews_stretch_world 0", "ews_stretch_biomes -1"]), ExpandWorld.Found.None);
        Assert.Equal(1f, r.Stretch);
        Assert.Equal(1f, r.BiomeStretch);
    }

    [Fact]
    public void AFolderWithoutTheModsHasNone()
    {
        string bep = Path.Combine(Data, "plain");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        Directory.CreateDirectory(Path.Combine(bep, "plugins"));
        var found = ExpandWorld.Look(bep, "test");
        Assert.False(found.Any);
        Assert.False(ExpandWorld.Find(new AppSettings { BepInExFolders = { bep } }).Any);
        // A folder that is not there holds none either.
        Assert.False(ExpandWorld.Look(Path.Combine(Data, "nowhere"), "test").Any);
        Assert.True(ExpandWorld.Rules(new Dictionary<string, string>(), ExpandWorld.Found.None).IsVanilla);
    }

    [Fact]
    public void TheSettingsSwitchKeepsEveryWorldVanillaWhileOff()
    {
        using var w = new TempWorld();
        var world = w.Load();
        world.StartingGlobalKeys.Add("ews_world_radius 20000");
        Assert.True(ExpandWorld.Resolve(world, new AppSettings()).IsVanilla);
        var on = ExpandWorld.Resolve(world, new AppSettings { ExpandWorld = true });
        Assert.Equal(20000f, on.Radius);
        Assert.False(on.IsVanilla);
        // A world with no keys and no folder: the game's own values, switch or no switch.
        world.StartingGlobalKeys.Clear();
        Assert.True(ExpandWorld.Resolve(world, new AppSettings { ExpandWorld = true }).IsVanilla);
    }

    [Fact]
    public void TheRulesAreDescribedInAFewWords()
    {
        Assert.Equal("the game's own values", ExpandWorld.Describe(WorldRules.Vanilla));
        string text = ExpandWorld.Describe(new WorldRules { Radius = 20000f, Stretch = 2f, AltitudeDelta = 5f, Rivers = false });
        Assert.Contains("radius 20000 + 500 m", text);
        Assert.Contains("stretch 2", text);
        Assert.Contains("+5 m", text);
        Assert.Contains("rivers off", text);
    }
}
