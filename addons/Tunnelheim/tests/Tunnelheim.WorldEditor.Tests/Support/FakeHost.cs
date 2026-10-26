using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The editor as the add-on sees it outside the hooks: no game connected, no game components (no hoe strokes), the
/// mod's settings as set (none by default: the mod's defaults); no BepInEx folders, none of the game's world rules.
/// </summary>
internal sealed class FakeHost : IEditorHost, IGameData
{
    public ILiveGame? Live => null;

    /// <summary>The mod's settings file's settings, by section and name.</summary>
    public Dictionary<(string Section, string Key), string> Settings { get; init; } = [];

    public string? Setting(string file, string section, string key) =>
        file == TunnelheimMod.SettingsFile && Settings.TryGetValue((section, key), out string? value) ? value : null;

    public PluginFile? FindPlugin(string dllName) => null;

    public string? GameManagedFolder => null;

    public IGameData Game => this;

    public IReadOnlyList<BepInExFolder> BepInExFolders => [];

    public string DataFolder => "";

    public IWorldGameData World { get; } = new NoWorldGameData();

    public void Redraw()
    {
    }

    public IReadOnlyList<GameComponent> Components(string scriptClass) => [];
}
