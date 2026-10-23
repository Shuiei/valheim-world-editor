using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The editor as the add-on sees it outside the hooks: no game connected, no game components or settings; the mod's
/// plugin where the test puts it (none by default), each look for it counted.
/// </summary>
internal sealed class FakeHost : IEditorHost, IGameData
{
    /// <summary>The mod's plugin file the editor finds; null: none.</summary>
    public string? Plugin { get; set; }

    /// <summary>How often the add-on looked for the mod's plugin.</summary>
    public int Looks { get; private set; }

    public ILiveGame? Live => null;

    public string? Setting(string file, string section, string key) => null;

    public PluginFile? FindPlugin(string dllName)
    {
        Looks++;
        return dllName == RailroadsMod.DllName && Plugin != null ? new PluginFile(Plugin, Path.GetDirectoryName(Plugin)!) : null;
    }

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
