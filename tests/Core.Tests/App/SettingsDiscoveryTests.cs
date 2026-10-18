using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What the settings lead the editor to find: the plugin's settings read, the world folders checked and listed, and the
/// BepInEx folders searched.
/// </summary>
[Collection("DataDir")]
public class SettingsDiscoveryTests : AppSettingsTestBase
{
    [Fact]
    public void PluginSettingsAreParsed()
    {
        var b = LocalGame.ParseText("[Http]\nBindAddress = 127.0.0.1\nPort = 5190\nToken = abc123\n", "x.cfg", "test");
        Assert.NotNull(b);
        Assert.Equal(5190, b!.Port);
        Assert.Equal("abc123", b.Token);
        Assert.Null(LocalGame.ParseText("Port = 5182\nToken = \n", "x.cfg", "test"));
        Assert.Equal(5182, LocalGame.ParseText("Token = z", "x.cfg", "test")!.Port);
    }

    [Fact]
    public void WorldFoldersAreChecked()
    {
        using var w = new TempWorld();
        Assert.Null(Worlds.Check(w.Dir));
        Assert.Contains("does not exist", Worlds.Check("/no/such/folder")!);
        Assert.Contains("holds worlds", Worlds.Check(Path.GetDirectoryName(w.Dir)!)!);
        Assert.Contains("No Valheim world", Worlds.Check(Data)!);
    }

    [Fact]
    public void ExtraWorldFoldersAreListed()
    {
        using var w = new TempWorld();
        var settings = new AppSettings { WorldFolders = { Path.GetDirectoryName(w.Dir)! } };
        var found = Worlds.Find(settings);
        var world = Assert.Single(found, x => x.Path == w.Dir);
        Assert.Equal("yours", world.Where);
        Assert.True(world.Usable);
        Assert.Equal(2, world.SaveNumber);
    }

    [Fact]
    public void BepInExFoldersFromSettingsAreSearched()
    {
        string profiles = Path.Combine(Data, "profiles");
        string cfg = Path.Combine(profiles, "Default", "BepInEx", "config");
        Directory.CreateDirectory(cfg);
        Directory.CreateDirectory(Path.Combine(profiles, "Default", "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(cfg, "local.worldeditorbridge.cfg"), "Port = 5191\nToken = tok\n");
        var settings = new AppSettings { BepInExFolders = { profiles } };
        var bridges = LocalGame.FindBridges(settings, out bool bepInEx, out _);
        Assert.True(bepInEx);
        Assert.Contains(bridges, b => b.Port == 5191 && b.Token == "tok");
    }
}
