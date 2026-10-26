using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>The game the editor is connected to, as extensions see it (ILiveGame): a LiveBridge and what it reported.</summary>
/// <param name="live">The connection.</param>
/// <param name="mods">The mods registered with the bridge.</param>
public sealed class LiveGame(LiveBridge live, IReadOnlyList<string> mods) : ILiveGame
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Mods => mods;

    /// <inheritdoc/>
    public Task<byte[]> CallAsync(string modId, string name, byte[] body, CancellationToken cancel = default) => live.Call(modId, name, body ?? [], cancel);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GamePlugin>?> Plugins(CancellationToken cancel = default) =>
        (await live.Plugins().WaitAsync(cancel))?.Select(p => new GamePlugin(p.Id, p.Name, p.Version)).ToList();

    /// <inheritdoc/>
    public Task<byte[]?> ConfigFile(string path, CancellationToken cancel = default) => live.ConfigFile(path).WaitAsync(cancel);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConfigEntry>?> ConfigFolder(string path, CancellationToken cancel = default) =>
        (await live.ConfigFolder(path).WaitAsync(cancel))?.Select(e => new ConfigEntry(e.Name, e.Folder, e.Size, e.Modified)).ToList();
}
