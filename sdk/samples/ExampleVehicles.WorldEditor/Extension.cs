using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

namespace ExampleVehicles.WorldEditor;

/// <summary>The extension's entry: registers the mod's hooks (one of each kind the SDK offers).</summary>
public sealed class Extension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        var packed = new PackedIds();
        registry.AddDecoder(Keys.Packed, packed);
        registry.AddCopyRewriter(packed, Keys.Packed);
        registry.AddShapes(new Sails(), Keys.Sail);
        registry.AddShapes(new Cables(), Keys.Post);
        registry.AddGroundCuts(new Pits(), Keys.Pit);
        registry.AddScene(new Canals());
        registry.AddTool(new CanalTool());
        registry.AddAreaCopier("canals", new CanalCopies());
        registry.AddLiveListener(new LiveLink(registry));
        registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
    }
}
