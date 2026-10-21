using TerrainEditor.App;
using TerrainEditor.Tunnelheim;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Tunnelheim as the editor finds it: its data objects told apart, its plugin found in a BepInEx folder (a mod
/// manager's turned-off file not counting), and the building height read from its settings file or the mod's default.
/// </summary>
[Collection("DataDir")]
public class TunnelheimModTests : AppSettingsTestBase
{
    [Fact]
    public void ItsDataObjectsAreToldApart()
    {
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.RecordPrefab));
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.CheckpointPrefab));
        Assert.Equal(1906006406, TunnelheimMod.RecordPrefab);
        Assert.False(TunnelheimMod.IsData(0));
    }

    [Fact]
    public void ItsPluginIsFoundInAKnownBepInExFolder()
    {
        string off = Bep("off", TunnelheimMod.DllName + ".old"), on = Bep("on", TunnelheimMod.DllName);
        Assert.Equal(on, TunnelheimMod.Find(new AppSettings { BepInExFolders = { off, on } }));
        Assert.Null(TunnelheimMod.Find(new AppSettings { BepInExFolders = { off } }));
    }

    [Fact]
    public void TheBuildingHeightComesFromItsSettingsFile()
    {
        string bep = Path.Combine(Data, "cfg", "BepInEx");
        Assert.Equal(RockRules.DefaultMaxBuildHeight, TunnelheimMod.MaxBuildHeight(null));
        Assert.Equal(RockRules.DefaultMaxBuildHeight, TunnelheimMod.MaxBuildHeight(bep));
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        string cfg = Path.Combine(bep, "config", TunnelheimMod.PluginId + ".cfg");
        File.WriteAllText(cfg, "[Building]\n# Add Earth ...\nMaxHeightAboveGround = 12.5\n");
        Assert.Equal(12.5f, TunnelheimMod.MaxBuildHeight(bep));
        File.WriteAllText(cfg, "[Building]\nMaxHeightAboveGround = lots\n");
        Assert.Equal(RockRules.DefaultMaxBuildHeight, TunnelheimMod.MaxBuildHeight(bep));
    }

    /// <summary>A BepInEx folder with this file in a mod manager's plugin folder.</summary>
    private string Bep(string name, string file)
    {
        string plugins = Path.Combine(Data, name, "BepInEx", "plugins", "Tunnelheim-Tunnelheim");
        Directory.CreateDirectory(plugins);
        File.WriteAllBytes(Path.Combine(plugins, file), []);
        return Path.Combine(Data, name, "BepInEx");
    }
}
