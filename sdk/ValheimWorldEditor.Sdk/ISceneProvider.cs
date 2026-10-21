namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Draws what a mod makes of an area as a whole (the rock dug out of a hillside, a canal), from its objects' data and
/// the ground (see <see cref="IExtensionRegistry.AddScene"/>): meshes, and holes in the editor's ground where they
/// replace it.
/// </summary>
public interface ISceneProvider
{
    /// <summary>
    /// What to draw in an area; null for nothing. Called on a worker thread when an area opens and again whenever its
    /// objects, its ground or the game connected change (or <see cref="IEditorHost.Redraw"/> is asked): work it all
    /// out from the area each time.
    /// </summary>
    /// <param name="area">The area open.</param>
    SceneLayer? Build(IAreaView area);
}
