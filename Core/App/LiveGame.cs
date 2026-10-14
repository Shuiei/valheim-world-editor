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
}
