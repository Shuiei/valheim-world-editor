using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Expand World add-on in the editor, installed as a player installs it, with Expand World Size in the BepInEx
/// folder: new worlds follow the mod's settings file, worlds whose file keeps the mod's keys are named on their cards,
/// and with the add-on's switch off in Settings, every world is the game's own again.
/// </summary>
[Collection("AddOns")]
public sealed class ExpandWorldInTheEditorTests : IDisposable
{
    private readonly AddOnSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    /// <summary>Expand World Size in the sandbox's BepInEx folder (a stand-in for its DLL), with this world radius in its settings file.</summary>
    private void InstallSize(int radius)
    {
        string bep = _sandbox.Settings.BepInExFolders[0];
        Directory.CreateDirectory(Path.Combine(bep, "plugins", "JereKuusela-Expand_World_Size"));
        File.WriteAllBytes(Path.Combine(bep, "plugins", "JereKuusela-Expand_World_Size", "ExpandWorldSize.dll"), [0]);
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_size.cfg"), $"[1. General]\nWorld radius = {radius}\n");
    }

    [Fact]
    public void WorldsFollowTheModWhileTheAddOnIsOn()
    {
        InstallSize(20000);
        _sandbox.Run("ExpandWorld");

        var rules = WorldRulesSource.ForNewWorld();
        Assert.Equal(20000f, rules.Radius);
        Assert.True(rules.SizeInstalled);
        Assert.Equal("Expand World", ModExtensions.MadeBy(["ews_world_radius 20000"]));
        Assert.Null(ModExtensions.MadeBy(["defeated_eikthyr"]));

        _sandbox.Settings.AddOnsOff = ["ExpandWorld"];
        ModManifests.Refresh(_sandbox.Settings);
        ModExtensions.Refresh(_sandbox.Settings);
        Assert.True(WorldRulesSource.ForNewWorld().IsVanilla);
        Assert.Null(ModExtensions.MadeBy(["ews_world_radius 20000"]));
    }
}
