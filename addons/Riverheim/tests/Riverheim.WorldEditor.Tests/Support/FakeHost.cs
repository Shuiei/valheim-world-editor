using ValheimWorldEditor.Sdk;

namespace Riverheim.WorldEditor.Tests;

/// <summary>
/// The editor as the add-on sees it: the player's Riverheim where set (none by default) and the game's Managed folder;
/// no game connected, no game components, no settings, no BepInEx folders, none of the game's world rules.
/// </summary>
internal sealed class FakeHost : IEditorHost, IGameData, IWorldGameData
{
    /// <summary>The player's Riverheim; null: not installed.</summary>
    public PluginFile? Riverheim { get; init; }

    public string? GameManagedFolder { get; init; }

    public ILiveGame? Live => null;

    public IGameData Game => this;

    public IReadOnlyList<BepInExFolder> BepInExFolders => [];

    public string DataFolder => "";

    public IWorldGameData World => this;

    public bool LocationsRead => false;

    public ValheimGen.LocationRuleSet Locations { get; } = new();

    public IReadOnlyList<ValheimGen.VegetationRule> Vegetation => [];

    public PluginFile? FindPlugin(string dllName) => dllName == RiverheimMod.DllName ? Riverheim : null;

    public string? Setting(string file, string section, string key) => null;

    public void Redraw()
    {
    }

    public IReadOnlyList<GameComponent> Components(string scriptClass) => [];

    public Task ReadLocations() => Task.CompletedTask;

    public bool HasPrefab(string name) => false;

    public bool IsGameRoom(string name) => false;
}
