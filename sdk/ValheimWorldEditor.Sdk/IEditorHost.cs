namespace ValheimWorldEditor.Sdk;

/// <summary>What an extension can ask of the editor at any time (see <see cref="IExtensionRegistry.Host"/>).</summary>
public interface IEditorHost
{
    /// <summary>The game the editor is connected to; null when it is not (a saved world, the Workshop, or nothing open).</summary>
    ILiveGame? Live { get; }

    /// <summary>What the editor read of the game's own files for the extensions (the manifests' "gameComponents").</summary>
    IGameData Game { get; }

    /// <summary>
    /// A mod's setting as the world's game has it, from a BepInEx settings file the manifest's "settingsFiles" names:
    /// the connected game's own (through its bridge) while live, else the first of the player's BepInEx folders that has
    /// the file. Null when there is no such file, section or setting: the mod's default applies. Ready once a world or
    /// the Workshop is open, so ask from your hooks, not from Register.
    /// </summary>
    /// <param name="file">The file's name in BepInEx/config ("author.mod.cfg").</param>
    /// <param name="section">Its section, as the heading names it without the brackets.</param>
    /// <param name="key">The setting's name.</param>
    /// <returns>The setting's text as the file has it (BepInEx writes numbers with a dot, true and false for switches).</returns>
    string? Setting(string file, string section, string key);

    /// <summary>
    /// A mod's plugin DLL in the player's BepInEx folders the editor knows (the one named in Settings for Expand World
    /// first, then the others; a mod manager's file of a mod turned off does not count); null when none has it.
    /// </summary>
    /// <param name="dllName">The DLL's file name ("SomeMod.dll").</param>
    PluginFile? FindPlugin(string dllName);

    /// <summary>The player's Valheim's Managed folder (the game's and Unity's assemblies); null when Valheim is not found.</summary>
    string? GameManagedFolder { get; }

    /// <summary>
    /// Asks for the extensions' shapes and ground cuts to be made again (after an answer from the game changed what
    /// they are). Cheap to call; the work happens at the next frame.
    /// </summary>
    void Redraw();
}
