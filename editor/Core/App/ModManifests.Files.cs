using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Finding the manifests in the plugins folders and reading them in order, each file into the notes in use.
/// </summary>
public static partial class ModManifests
{
    /// <summary>
    /// Reads every manifest in the plugins folders and of the add-ons in use (AddOns: theirs first) and uses what they
    /// say (nothing when there is none). Never throws.
    /// </summary>
    /// <returns>How many manifests were read.</returns>
    public static int Refresh(AppSettings settings)
    {
        var files = new List<(FileInfo File, string Plugins)>();
        foreach (var (dir, _) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            try
            {
                if (Directory.Exists(plugins))
                {
                    files.AddRange(Directory.EnumerateFiles(plugins, FileName, SearchOption.AllDirectories).Select(f => (new FileInfo(f), plugins)));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be listed: its manifests are not read.
                Log.Caught(e, "listing a plugins folder for worldeditor.json files (its manifests are not read)");
            }
        }
        var notes = BaseNotes();
        var addOns = new List<AddOns.AddOn>();
        int read = ReadAddOns(settings, notes, addOns);
        // Oldest first: a newer file's notes replace an older one's.
        foreach (var (file, plugins) in files.DistinctBy(f => f.File.FullName).OrderBy(f => f.File.LastWriteTimeUtc))
        {
            try
            {
                // The mod: its folder right under plugins. A manifest lying in plugins itself belongs to no mod: it
                // offers no extension (any mod's DLL would be "inside" its folder, under the name "plugins").
                string folder = file.DirectoryName!;
                string relative = Path.GetRelativePath(plugins, folder);
                (string, string)? from = relative == "." ? null : (folder, relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]);
                if (file.Length <= MaxFile && Read(File.ReadAllText(file.FullName), notes, from))
                {
                    read++;
                }
                else
                {
                    Console.WriteLine($"mod manifest {file.FullName}: not a worldeditor.json this editor reads, skipped.");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log.Caught(e, "reading a mod's worldeditor.json (skipped)");
            }
        }
        notes.LinkSet = new LinkSet([.. notes.Links.Values]);
        _notes = notes;
        _addOns = addOns;
        Interlocked.Increment(ref _generation);
        return read;
    }

    /// <summary>
    /// Adds the extension a manifest offers (its "extension" DLL inside the mod's folder, ending in .dll) to the notes.
    /// </summary>
    private static void ReadExtension(JsonObject root, (string Folder, string Mod)? from, Notes notes, bool addOn)
    {
        if (from is var (folder, mod) && ExtensionPath(root, folder) is { } path)
        {
            notes.Extensions[path] = new Offered(path, mod, Names(root["gameComponents"], IsScriptName, 32), Names(root["settingsFiles"], IsSettingsFile, 8), addOn)
            {
                WorldGenerators = GeneratorNames(root["worldGenerators"]),
            };
        }
    }

    /// <summary>
    /// The full path of the extension a manifest names ("extension"): a DLL inside its folder only; null for none or
    /// one elsewhere.
    /// </summary>
    private static string? ExtensionPath(JsonObject root, string folder)
    {
        if (!Text(root["extension"], out string? dll) || dll == null)
        {
            return null;
        }
        string path = Path.GetFullPath(Path.Combine(folder, dll));
        return path.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    /// <summary>
    /// The names a manifest's list holds: its strings that <paramref name="ok"/> takes, each once, <paramref name="most"/>
    /// at most; none when it is not a list.
    /// </summary>
    private static List<string> Names(JsonNode? node, Func<string, bool> ok, int most) =>
        node is not JsonArray list
            ? []
            : [.. list.OfType<JsonValue>().Select(v => v.TryGetValue(out string? s) ? s : null).OfType<string>()
                .Where(ok).Distinct(StringComparer.Ordinal).Take(most)];

    /// <summary>
    /// The world generators a manifest says its extension registers ("worldGenerators"), by the id their worlds' files
    /// name, with the mod's name for the player: entries whose id (100 characters at most) and name (64 at most) are
    /// not blank, 8 at most; null when there are none.
    /// </summary>
    internal static Dictionary<string, string>? GeneratorNames(JsonNode? node)
    {
        if (node is not JsonObject map)
        {
            return null;
        }
        var names = map.Where(e => e.Key.Length is > 0 and <= 100 && !string.IsNullOrWhiteSpace(e.Key))
            .Select(e => (e.Key, Name: e.Value is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : ""))
            .Where(e => e.Name.Length is > 0 and <= 64).Take(8).ToDictionary(e => e.Key, e => e.Name, StringComparer.Ordinal);
        return names.Count > 0 ? names : null;
    }

    /// <summary>Whether a "gameComponents" entry is a class name: letters, digits and underscores, not starting with a digit.</summary>
    private static bool IsScriptName(string s) =>
        s.Length is > 0 and <= 100 && !char.IsAsciiDigit(s[0]) && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>Whether a "settingsFiles" entry is a settings file's name in BepInEx/config: ending in .cfg, no folder.</summary>
    private static bool IsSettingsFile(string s) => AddOns.IsFileName(s, ".cfg");
}
