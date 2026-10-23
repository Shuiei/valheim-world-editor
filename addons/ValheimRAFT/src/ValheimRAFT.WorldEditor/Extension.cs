using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(ValheimRAFT.WorldEditor.Extension))]

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The add-on's entry: registers what it adds to the editor for ValheimRAFT, beyond its worldeditor.json (which links
/// each vehicle's pieces to it).
/// </summary>
public sealed class Extension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        var ropes = new RopeList();
        registry.AddDecoder(Keys.Ropes, ropes);
        registry.AddCopyRewriter(ropes, Keys.Ropes);
        registry.AddShapes(new RopeLines(), Keys.RopeAnchor, Keys.DockAnchor);
        var sails = new SailCloth();
        registry.AddShapes(sails, Keys.Sail);
        registry.AddDecoder(Keys.SailColour, sails);
        registry.AddDecoder(Keys.PatternColour, sails);
        registry.AddDecoder(Keys.LogoColour, sails);
        registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
    }
}
