using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Expand World folder in Settings, the rules a new world is made with, and the worlds list's tag for worlds made
/// with Expand World Size.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldFolderTests : AppSettingsTestBase
{
    /// <summary>A profile's BepInEx folder with Expand World Size installed and its settings file (a folder added in Settings must be one).</summary>
    private string Bep(string name, string cfg)
    {
        string bep = Path.Combine(Data, name, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        TestMods.Install(bep, TestMods.Size);
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_size.cfg"), cfg);
        return bep;
    }

    [Fact]
    public void TheFolderInSettingsComesFirstWhenItHasTheMods()
    {
        string first = Bep("first", "[1. General]\nWorld radius = 15000\n"), chosen = Bep("chosen", "[1. General]\nWorld radius = 20000\n");
        var settings = new AppSettings { BepInExFolders = { first }, ExpandWorldFolder = chosen };
        Assert.Equal(chosen, ExpandWorld.Find(settings).Folder);
        Assert.Equal("the Expand World folder in Settings", ExpandWorld.Find(settings).Where);
        // A folder in Settings without the mods: the others are looked at.
        string empty = Path.Combine(Data, "empty");
        Directory.CreateDirectory(empty);
        Assert.Equal(first, ExpandWorld.Find(new AppSettings { BepInExFolders = { first }, ExpandWorldFolder = empty }).Folder);
        Assert.Equal(first, ExpandWorld.Find(new AppSettings { BepInExFolders = { first }, ExpandWorldFolder = "" }).Folder);
    }

    [Fact]
    public void ANewWorldsRulesComeFromTheFolderWhenTheSwitchIsOn()
    {
        string bep = Bep("bep", "[1. General]\nWorld radius = 20000\n");
        Assert.True(ExpandWorld.ForNewWorld(new AppSettings { ExpandWorldFolder = bep }).IsVanilla);
        var rules = ExpandWorld.ForNewWorld(new AppSettings { ExpandWorld = true, ExpandWorldFolder = bep });
        Assert.Equal(20000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        // No folder with the mods: the game's own values.
        Assert.True(ExpandWorld.ForNewWorld(new AppSettings { ExpandWorld = true, ExpandWorldFolder = Path.Combine(Data, "nowhere") }).IsVanilla);
    }

    [Fact]
    public void TheWorldsListTellsWorldsMadeWithExpandWorldSize()
    {
        using var w = new TempWorld();
        var plain = Worlds.Find(new AppSettings { WorldFolders = { w.Dir } });
        Assert.False(Assert.Single(plain).ExpandWorld);
        // The world file's head read on its own: the keys the mod keeps.
        var loaded = w.Load();
        int save = loaded.SaveNumber;
        var head = WorldSave.ReadMetadata(Path.Combine(w.Dir, $"_main.{save}.fwl2"));
        Assert.Equal("CITest", head.Name);
        Assert.Equal(loaded.Seed, head.Seed);
        Assert.Empty(head.StartingGlobalKeys);
        // The same world with the mod's keys in its file.
        using var tagged = new TempWorld();
        WorldFileHead.Write(Path.Combine(tagged.Dir, $"_main.{save}.fwl2"), 32, ["ews_world_radius 20000"]);
        var info = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { tagged.Dir } }));
        Assert.True(info.Usable, info.Problem);
        Assert.True(info.ExpandWorld);
    }
}
