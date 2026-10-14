namespace ValheimWorldEditor.Sdk;

/// <summary>What an extension can ask of the editor at any time (see <see cref="IExtensionRegistry.Host"/>).</summary>
public interface IEditorHost
{
    /// <summary>The game the editor is connected to; null when it is not (a saved world, the Workshop, or nothing open).</summary>
    ILiveGame? Live { get; }

    /// <summary>
    /// Asks for the extensions' shapes and ground cuts to be made again (after an answer from the game changed what
    /// they are). Cheap to call; the work happens at the next frame.
    /// </summary>
    void Redraw();
}
