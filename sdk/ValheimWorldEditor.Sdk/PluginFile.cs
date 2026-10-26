namespace ValheimWorldEditor.Sdk;

/// <summary>A mod's plugin DLL in one of the player's BepInEx folders (<see cref="IEditorHost.FindPlugin"/>).</summary>
public sealed class PluginFile
{
    /// <summary>A plugin file found.</summary>
    /// <param name="dll">The DLL's full path.</param>
    /// <param name="bepInExFolder">The BepInEx folder it is in (its core folder holds BepInEx's and Harmony's assemblies).</param>
    public PluginFile(string dll, string bepInExFolder)
    {
        Dll = dll;
        BepInExFolder = bepInExFolder;
    }

    /// <summary>The DLL's full path.</summary>
    public string Dll { get; }

    /// <summary>The BepInEx folder it is in.</summary>
    public string BepInExFolder { get; }
}
