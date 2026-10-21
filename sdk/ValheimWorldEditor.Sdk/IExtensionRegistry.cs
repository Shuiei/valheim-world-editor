namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Where an extension registers its hooks, during <see cref="IWorldEditorExtension.Register"/>. Data keys and prefab
/// names are given as the mod's code names them (the editor hashes them as the game does). Hooks are called from the
/// editor's background threads as well as its window's, possibly at once: they must not keep state between calls
/// (or must lock it), and must be quick. A hook that throws turns its extension off until the editor starts again.
/// </summary>
public interface IExtensionRegistry
{
    /// <summary>The API version of the editor loading the extension (at least the one it was built against).</summary>
    int ApiVersion { get; }

    /// <summary>The editor's version ("1.33.0").</summary>
    string EditorVersion { get; }

    /// <summary>
    /// Shows a byte-array value readably in the inspector (under the value), for every object holding it.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <param name="decoder">Turns the bytes into text.</param>
    void AddDecoder(string key, IDataDecoder decoder);

    /// <summary>
    /// Lets the extension rewrite each object of a pasted copy (after the editor's own linking: see
    /// <see cref="ICopyRewriter"/>). The keys it names are its own: a manifest's dropOnCopy no longer takes them out of
    /// copies, the extension decides.
    /// </summary>
    /// <param name="rewriter">Rewrites a copy's values.</param>
    /// <param name="keys">The data keys it handles; it is called for objects holding at least one of them.</param>
    void AddCopyRewriter(ICopyRewriter rewriter, params string[] keys);

    /// <summary>
    /// Keeps what the mod holds of a place with the editor's copies, and puts it down where they are pasted (see
    /// <see cref="IAreaCopier"/>): the mod's data that no copied object carries.
    /// </summary>
    /// <param name="id">
    /// Its name among the mod's copiers: letters, digits, dots, dashes and underscores, 1 to 64. Copies and blueprint
    /// files keep what it copies under the mod's name and this, for it to read back.
    /// </param>
    /// <param name="copier">What copies and puts down.</param>
    void AddAreaCopier(string id, IAreaCopier copier);

    /// <summary>
    /// Draws more of an object than its prefab's model: shapes the mod builds in code while the game runs (a sail
    /// from its corners, a rope to another object).
    /// </summary>
    /// <param name="provider">Gives an object's shapes.</param>
    /// <param name="prefabs">The prefabs it draws for.</param>
    void AddShapes(IShapeProvider provider, params string[] prefabs);

    /// <summary>
    /// Leaves the ground undrawn where an object opens it: the mouth of a tunnel or a pit the mod digs while the game
    /// runs, so that what the mod builds below (<see cref="AddShapes"/>) shows.
    /// </summary>
    /// <param name="cutter">Gives an object's cuts.</param>
    /// <param name="prefabs">The prefabs it cuts for.</param>
    void AddGroundCuts(IGroundCutter cutter, params string[] prefabs);

    /// <summary>
    /// Draws what the mod makes of an open area as a whole (rock dug out of a hill from the records of what was dug,
    /// a canal), with the game's ground look or in a colour, and leaves the editor's ground undrawn where those meshes
    /// replace it. Clicks land on the meshes and go through the holes.
    /// </summary>
    /// <param name="provider">Works out what to draw in an area.</param>
    void AddScene(ISceneProvider provider);

    /// <summary>
    /// Puts a tool of the mod's own on the 3D editor's tool rail (in the Mods section): its card of controls, the
    /// pointer and keys while it is chosen, previews, and edits made one step of the history.
    /// </summary>
    /// <param name="tool">The tool.</param>
    void AddTool(IEditorTool tool);

    /// <summary>
    /// Told when the editor connects to a game and when that ends. With the game, the extension can call the routes
    /// its mod's own plugin registered with the WorldEditorBridge (WorldEditorBridge.Api): what only the running game
    /// knows, or work only it can do.
    /// </summary>
    /// <param name="listener">What is told.</param>
    void AddLiveListener(ILiveListener listener);

    /// <summary>The editor, for what an extension asks of it outside its hooks: the game connected, a redraw.</summary>
    IEditorHost Host { get; }

    /// <summary>Writes a line to the editor's log (Settings → Open log), named after the extension.</summary>
    void Log(string message);
}
