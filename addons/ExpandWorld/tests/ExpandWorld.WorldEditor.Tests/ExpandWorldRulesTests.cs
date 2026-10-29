using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// The add-on's world rules provider (ExpandWorldRules): a new world's rules from the first BepInEx folder with the
/// mods, a world's from its keys too, the game's own without the mods, and the mod named on the cards of worlds made
/// with Expand World Size.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldRulesTests : AppSettingsTestBase
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

    /// <summary>The provider, with the editor knowing these BepInEx folders.</summary>
    private static ExpandWorldRules Rules(params string[] folders)
    {
        var settings = new AppSettings();
        settings.BepInExFolders.AddRange(folders);
        return new ExpandWorldRules(new EditorHost(() => settings));
    }

    [Fact]
    public async Task ANewWorldsRulesComeFromTheFirstFolderWithTheMods()
    {
        string empty = Path.Combine(Data, "empty", "BepInEx"), bep = Bep("bep", "[1. General]\nWorld radius = 20000\n");
        Directory.CreateDirectory(Path.Combine(empty, "plugins"));

        var rules = await Rules(empty, bep, Bep("later", "[1. General]\nWorld radius = 15000\n")).ForNewWorld();

        Assert.NotNull(rules);
        Assert.Equal(20000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        // No folder with the mods: the game's own values.
        Assert.True((await Rules(empty).ForNewWorld())!.IsVanilla);
    }

    [Fact]
    public async Task AWorldsRulesTakeItsKeys()
    {
        string bep = Bep("bep", "[1. General]\nWorld radius = 15000\n");
        var world = new WorldRulesRequest("w", ["ews_world_radius 25000"], null, 512, null);

        Assert.Equal(25000f, (await Rules(bep).ForWorld(world))!.Radius);
        // The world made with the mod, but no folder has it: the game without it ignores the keys, the editor too.
        Assert.True((await Rules().ForWorld(world))!.IsVanilla);
    }

    [Fact]
    public void WorldsMadeWithExpandWorldSizeAreNamedOnTheirCards()
    {
        var rules = Rules();

        Assert.Equal("Expand World", rules.MadeBy(["ews_world_radius 20000"]));
        Assert.Equal("Expand World", rules.MadeBy(["defeated_eikthyr", "EWS_STRETCH_WORLD 2"]));
        Assert.Null(rules.MadeBy(["defeated_eikthyr"]));
        Assert.Null(rules.MadeBy([]));
    }
}
