using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>What an extension asks of the editor (IEditorHost), with the editor's settings as they are each time.</summary>
/// <param name="settings">Gives the editor's settings (where Valheim and the BepInEx folders are).</param>
internal sealed class EditorHost(Func<AppSettings> settings) : IEditorHost
{
    /// <inheritdoc/>
    public ILiveGame? Live => ModExtensions.Live;

    /// <inheritdoc/>
    public IGameData Game => ModComponents.View;

    /// <inheritdoc/>
    public string? Setting(string file, string section, string key) => ModSettings.Value(file, section, key);

    /// <inheritdoc/>
    public PluginFile? FindPlugin(string dllName) =>
        LocalGame.FindPlugin(settings(), dllName) is var (dll, bep) ? new PluginFile(dll, bep) : null;

    /// <inheritdoc/>
    public IReadOnlyList<BepInExFolder> BepInExFolders => [.. LocalGame.BepInExFolders(settings()).Select(f => new BepInExFolder(f.BepInEx, f.Where))];

    /// <inheritdoc/>
    public string DataFolder => AppSettings.DataDir;

    /// <inheritdoc/>
    public IWorldGameData World => GameWorldData.Instance;

    /// <inheritdoc/>
    public string? GameManagedFolder =>
        GameLook.FindValheim(settings().ValheimPath) is { } valheim && GameLook.DataDir(valheim) is { } data ? Path.Combine(data, "Managed") : null;

    /// <inheritdoc/>
    public void Redraw() => ModExtensions.Redraw();
}
