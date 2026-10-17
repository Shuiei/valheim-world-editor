using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Finding the manifests in the plugins folders and reading them in order, each file into the notes in use.
/// </summary>
public static partial class ModManifests
{
    /// <summary>
    /// Reads every manifest in the plugins folders and uses what they say (nothing when there is none). Never throws.
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
        int read = 0;
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
        Interlocked.Increment(ref _generation);
        return read;
    }

    /// <summary>
    /// Adds the extension a manifest offers (its "extension" DLL inside the mod's folder, ending in .dll) to the notes.
    /// </summary>
    private static void ReadExtension(JsonObject root, (string Folder, string Mod)? from, Notes notes)
    {
        if (from is var (folder, mod) && Text(root["extension"], out string? dll) && dll != null)
        {
            // A DLL inside the mod's own folder only.
            string path = Path.GetFullPath(Path.Combine(folder, dll));
            if (path.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                notes.Extensions[path] = new Offered(path, mod);
            }
        }
    }
}
