using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The editor as extensions ask it (EditorHost): the BepInEx folders the settings it is given name, its data folder,
/// and the game's own world rules.
/// </summary>
[Collection("DataDir")]
public sealed class EditorHostTests : AppSettingsTestBase
{
    [Fact]
    public void TheHostGivesTheSettingsFoldersTheDataFolderAndTheGamesRules()
    {
        string bep = Path.Combine(Data, "profile", "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "plugins"));
        var settings = new AppSettings { BepInExFolders = { bep } };
        var host = new EditorHost(() => settings);

        Assert.Contains(host.BepInExFolders, f => f.Path == bep && f.Where != "");
        Assert.Equal(AppSettings.DataDir, host.DataFolder);
        Assert.Same(GameWorldData.Instance, host.World);
        settings.BepInExFolders.Clear();
        Assert.DoesNotContain(host.BepInExFolders, f => f.Path == bep);
    }
}
