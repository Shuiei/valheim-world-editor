using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(ValheimRAFT.WorldEditor.Extension))]

namespace ValheimRAFT.WorldEditor;

/// <summary>The extension's entry: registers the ropes' and the sails' hooks.</summary>
public sealed class Extension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        var ropes = new Ropes();
        registry.AddDecoder(Ropes.Key, ropes);
        registry.AddCopyRewriter(ropes, Ropes.Key);
        registry.AddShapes(new Sails(), Sails.Prefab);
    }
}
