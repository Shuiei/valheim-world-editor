using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The game the editor is connected to (live mode), as far as mods' plugins take part in it through the
/// WorldEditorBridge (WorldEditorBridge.Api): which mods registered with the bridge, and calls to their routes.
/// </summary>
public interface ILiveGame
{
    /// <summary>The ids of the mods registered with the bridge (BridgeApi.AddRoutes / AddListener), as reported when the game was opened.</summary>
    IReadOnlyList<string> Mods { get; }

    /// <summary>
    /// Calls a mod's route in the game (<c>POST /mods/&lt;modId&gt;/&lt;name&gt;</c>, answered by its IBridgeRoutes on the
    /// game's main thread) and gives back its answer. Throws when the mod or the route is not registered, when the
    /// route failed (with the game's message), or when the game cannot be reached.
    /// </summary>
    /// <param name="modId">The mod's id, as it registered.</param>
    /// <param name="name">The route's name.</param>
    /// <param name="body">What to send (empty for nothing).</param>
    /// <param name="cancel">Gives up waiting.</param>
    Task<byte[]> CallAsync(string modId, string name, byte[] body, CancellationToken cancel = default);

    /// <summary>
    /// The BepInEx plugins the game loaded (its bridge's /plugins); null from a bridge too old to list them (before
    /// WorldEditorBridge 1.54.0).
    /// </summary>
    /// <param name="cancel">Stops waiting.</param>
    Task<IReadOnlyList<GamePlugin>?> Plugins(CancellationToken cancel = default);

    /// <summary>
    /// A file under the game's BepInEx/config, by its path there ("/" between parts), as its bytes; null when there is
    /// no such file, or the bridge is older than 1.54.0. Throws <see cref="System.InvalidOperationException"/> when the
    /// bridge refuses it (out of the folder, over 4 MB) or fails.
    /// </summary>
    /// <param name="path">The file's path under BepInEx/config.</param>
    /// <param name="cancel">Stops waiting.</param>
    Task<byte[]?> ConfigFile(string path, CancellationToken cancel = default);

    /// <summary>
    /// The entries of a folder under the game's BepInEx/config ("" for the folder itself); null when there is no such
    /// folder, or the bridge is older than 1.54.0. Throws <see cref="System.InvalidOperationException"/> when the bridge
    /// refuses it or fails.
    /// </summary>
    /// <param name="path">The folder's path under BepInEx/config.</param>
    /// <param name="cancel">Stops waiting.</param>
    Task<IReadOnlyList<ConfigEntry>?> ConfigFolder(string path, CancellationToken cancel = default);
}
