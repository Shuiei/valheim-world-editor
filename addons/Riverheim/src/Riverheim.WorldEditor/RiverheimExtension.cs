using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(Riverheim.WorldEditor.RiverheimExtension))]

namespace Riverheim.WorldEditor;

/// <summary>
/// The add-on's extension: Riverheim's world generator (and its deprecated beta's, whose worlds are refused with why),
/// with the add-on's lines going to the editor's log.
/// </summary>
public sealed class RiverheimExtension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        RiverheimLibrary.Log = registry.Log;
        registry.AddWorldGenerator(new RiverheimGenerator(registry.Host, RiverheimMod.PluginId, registry.Log));
        registry.AddWorldGenerator(new RiverheimGenerator(registry.Host, RiverheimMod.BetaPluginId, registry.Log));
    }
}
