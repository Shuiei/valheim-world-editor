using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(ExpandWorld.WorldEditor.ExpandWorldExtension))]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ExpandWorld.WorldEditor.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WorldCheck")]

namespace ExpandWorld.WorldEditor;

/// <summary>
/// The add-on's extension: Expand World's world rules, with the readers' lines going to the editor's log and the game's
/// own rules taken from the editor, the Persistent event tool, the Custom object tool and the Prefab rules tool.
/// </summary>
public sealed class ExpandWorldExtension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        ExpandWorldMod.Note = registry.Log;
        ExpandWorldMod.Game = registry.Host.World;
        registry.AddWorldRules(new ExpandWorldRules(registry.Host));
        registry.AddTool(new PersistentEventTool(registry.Host));
        registry.AddTool(new CustomObjectTool(registry.Host));
        registry.AddTool(new PrefabRuleTool(registry.Host));
        registry.AddCreationRules(new PrefabCreationRules(registry.Host));
    }
}
