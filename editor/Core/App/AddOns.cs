using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Editor add-ons (docs/modding.md#add-ons): what makes the editor work with a mod that brings no worldeditor.json of
/// its own, made by anyone and installed by the player in the addons folder next to the editor's program, one
/// subfolder each with its worldeditor.json (naming the mod's DLL it serves, "serves") and its extension DLL. Installing
/// one is the player's choice: it is used as installed, while the mod it serves is in a plugins folder (one serving none
/// always), unless its switch in Settings turns it off (<see cref="AppSettings.AddOnsOff"/>). Read with the mods' manifests (ModManifests.Refresh), before them: a mod's
/// own file says the last word.
/// </summary>
public static class AddOns
{
    /// <summary>The folder's name.</summary>
    public const string FolderName = "addons";

    /// <summary>An installed add-on.</summary>
    /// <param name="Name">Its folder's name.</param>
    /// <param name="Folder">Its folder's full path.</param>
    /// <param name="Serves">The mod's DLL it serves ("Tunnelheim.dll"), or null for none.</param>
    /// <param name="Served">Whether that DLL is in a plugins folder (always for none): the add-on is then in use.</param>
    /// <param name="Dll">Its extension DLL's full path, or null when it brings none (only its manifest's notes).</param>
    /// <param name="On">Whether its switch in Settings is on (<see cref="AppSettings.AddOnsOff"/>): off, it is not used.</param>
    public sealed record AddOn(string Name, string Folder, string? Serves, bool Served, string? Dll, bool On = true)
    {
        /// <summary>
        /// The world generators its extension registers, by the id their worlds' files name, with the mod's name
        /// ("worldGenerators"): known even while it is off or waiting, to say why their worlds cannot open. Null for none.
        /// </summary>
        public IReadOnlyDictionary<string, string>? WorldGenerators { get; init; }
    }

    /// <summary>
    /// The add-ons turned off once Settings' switches are taken (<see cref="AppSettings.AddOnsOff"/>): those whose switch
    /// is off, and those turned off before that are not listed now (one put back later stays off).
    /// </summary>
    /// <param name="before">The add-ons turned off until now.</param>
    /// <param name="switches">Each listed add-on's name and whether its switch is on.</param>
    public static List<string> TurnedOff(IReadOnlyList<string> before, IEnumerable<(string Name, bool On)> switches)
    {
        var listed = switches.ToList();
        return [.. before.Where(n => !listed.Exists(s => s.Name == n)), .. listed.Where(s => !s.On).Select(s => s.Name)];
    }

    /// <summary>Stands in for the program's folder (the tests).</summary>
    internal static string? FolderForTests { get; set; }

    /// <summary>The folders add-ons are installed in (<see cref="FoldersFor"/> the program's folder).</summary>
    public static IReadOnlyList<string> Folders =>
        FolderForTests is { } test ? [Path.Combine(test, FolderName)] : FoldersFor(AppContext.BaseDirectory);

    /// <summary>
    /// The add-ons' folders for a program in <paramref name="program"/>: addons next to it; on macOS also next to the
    /// app (the program's own folder, …/ValheimWorldEditor.app/Contents/MacOS, is inside its signed bundle).
    /// </summary>
    internal static List<string> FoldersFor(string program)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(program));
        var folders = new List<string> { Path.Combine(dir.FullName, FolderName) };
        if (dir is { Name: "MacOS", Parent: { Name: "Contents", Parent: { Parent: { } holder } app } } && app.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            folders.Add(Path.Combine(holder.FullName, FolderName));
        }
        return folders;
    }

    /// <summary>
    /// Every add-on's manifest: worldeditor.json right inside a subfolder of an add-ons folder, with that subfolder.
    /// A folder that cannot be listed is said in the log and has none.
    /// </summary>
    internal static List<(FileInfo File, string Folder)> Manifests()
    {
        var found = new List<(FileInfo, string)>();
        foreach (string root in Folders.Distinct(StringComparer.Ordinal))
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }
                foreach (string folder in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
                {
                    string file = Path.Combine(folder, ModManifests.FileName);
                    if (File.Exists(file))
                    {
                        found.Add((new FileInfo(file), folder));
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Caught(e, "listing an add-ons folder (its add-ons are not read)");
            }
        }
        return found;
    }

    /// <summary>
    /// Whether a manifest's "serves" names a DLL plainly (a file name ending in .dll, no folder), or is left out.
    /// </summary>
    internal static bool IsDllName(string? serves) => serves == null || IsFileName(serves, ".dll");

    /// <summary>Whether a name is a plain file name (no folder, 100 characters at most) ending in an extension.</summary>
    /// <param name="name">The name.</param>
    /// <param name="extension">The extension, with its dot (any case matches).</param>
    internal static bool IsFileName(string name, string extension) =>
        name.Length > extension.Length && name.Length <= 100 && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            && name.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\']) < 0;

    /// <summary>Whether a mod's DLL is in one of the plugins folders the settings name (or found by themselves).</summary>
    internal static bool IsInstalled(AppSettings settings, string dll) =>
        LocalGame.BepInExFolders(settings).Any(f => PluginFiles.Has(Path.Combine(f.BepInEx, "plugins"), dll, Console.WriteLine));
}
