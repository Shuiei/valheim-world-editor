namespace WorldEditorBridge.Api;

/// <summary>
/// What answers the editor's calls on a mod's routes, <c>POST /mods/&lt;modId&gt;/&lt;name&gt;</c>: the editor side of the
/// mod (its editor extension) asks, this answers, in whatever form the two agree on. Called on the game's main thread,
/// while the editor waits (30 seconds at most): be quick, or answer "started" and let the editor ask again.
/// </summary>
public interface IBridgeRoutes
{
    /// <summary>
    /// Answers a call; null for a name this mod has no route of (the editor gets 404). An exception answers 500
    /// with its message.
    /// </summary>
    /// <param name="name">The route's name, as in the path (letters, digits, dots, dashes and underscores).</param>
    /// <param name="body">What the editor sent (empty when nothing).</param>
    /// <returns>The answer's bytes (empty when there is nothing to say).</returns>
    byte[]? Handle(string name, byte[] body);
}
