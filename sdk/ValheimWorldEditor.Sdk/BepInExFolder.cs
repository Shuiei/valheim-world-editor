namespace ValheimWorldEditor.Sdk;

/// <summary>A BepInEx folder the editor knows (<see cref="IEditorHost.BepInExFolders"/>).</summary>
public sealed class BepInExFolder
{
    /// <summary>A BepInEx folder.</summary>
    /// <param name="path">Its full path (it may not exist).</param>
    /// <param name="where">Where it is, for the player ("Valheim folder", "r2modman profile Default"...).</param>
    public BepInExFolder(string path, string where)
    {
        Path = path;
        Where = where;
    }

    /// <summary>Its full path (it may not exist).</summary>
    public string Path { get; }

    /// <summary>Where it is, for the player.</summary>
    public string Where { get; }
}
