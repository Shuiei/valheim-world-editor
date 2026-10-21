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
}
