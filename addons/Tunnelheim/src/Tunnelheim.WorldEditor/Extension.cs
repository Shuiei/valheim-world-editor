using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(Tunnelheim.WorldEditor.Extension))]

namespace Tunnelheim.WorldEditor;

/// <summary>The add-on's entry: registers what it adds to the editor for Tunnelheim.</summary>
public sealed class Extension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        registry.AddDecoder(TunnelheimMod.RecordKeyName, new RecordDecoder());
        registry.AddScene(new TunnelScene(registry.Host, registry.Log));
        registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
    }
}
