namespace ValheimWorldEditor.Sdk;

/// <summary>
/// An extension's entry: made once when the editor loads the extension, then asked to register its hooks.
/// </summary>
public interface IWorldEditorExtension
{
    /// <summary>Registers the extension's hooks. Called once, before any hook is used.</summary>
    /// <param name="registry">Where hooks are registered.</param>
    void Register(IExtensionRegistry registry);
}
