using System;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Marks an assembly as a Valheim World Editor extension and names its entry type, which the editor makes (it needs a
/// public constructor without parameters) and asks to register its hooks.
/// </summary>
/// <example><code>[assembly: WorldEditorExtension(typeof(MyMod.EditorExtension))]</code></example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class WorldEditorExtensionAttribute : Attribute
{
    /// <summary>Names the entry type.</summary>
    /// <param name="entry">A public class implementing <see cref="IWorldEditorExtension"/>.</param>
    public WorldEditorExtensionAttribute(Type entry)
    {
        Entry = entry;
    }

    /// <summary>The entry type: a public class implementing <see cref="IWorldEditorExtension"/>.</summary>
    public Type Entry { get; }

    /// <summary>The API version the extension was built against (<see cref="SdkInfo.ApiVersion"/>, the default).</summary>
    public int ApiVersion { get; set; } = SdkInfo.ApiVersion;
}
