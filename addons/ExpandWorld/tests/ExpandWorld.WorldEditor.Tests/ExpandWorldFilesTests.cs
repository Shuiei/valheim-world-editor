using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// WorldCheck's contents and vegetation checks read a BepInEx folder's Expand World files with the add-on's readers
/// (ExpandWorldFiles, tools/WorldCheck): the mods' settings and data files as a new world's rules, converted against the
/// editor's game rules, with the world's global keys.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldFilesTests : AppSettingsTestBase
{
    [Fact]
    public void TheChecksReadAFoldersFilesWithTheAddOnsReaders()
    {
        string bep = Path.Combine(Data, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        TestMods.Install(bep, TestMods.Size);
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_size.cfg"), "[1. General]\nWorld radius = 20000\n");

        var rules = ExpandWorldFiles.Rules(bep, ["defeated_eikthyr"]);

        Assert.Equal(20000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        Assert.Same(GameWorldData.Instance, ExpandWorldMod.Game);
        // A folder without the mods: the game's own values.
        Assert.True(ExpandWorldFiles.Rules(Path.Combine(Data, "nowhere"), []).IsVanilla);
    }
}
