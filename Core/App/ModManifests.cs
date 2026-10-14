using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// What mod authors can tell the editor about their mod, in a worldeditor.json shipped with it (anywhere in its
/// folder under BepInEx/plugins; docs/modding.md, docs/worldeditor.schema.json): labels, help and read-only flags for
/// the data their objects keep (the inspector), prefabs the editor should not offer (tools, effects, helpers), and
/// groups for their pieces (the Build panel's Mods tab). Everything else the editor learns by itself (ModCatalogue,
/// ModKeys). Read from the plugins folders the editor knows (LocalGame.BepInExFolders) as a world or the Workshop
/// opens; of two manifests saying something of the same name, the newest file's wins. A file that is not a manifest
/// is skipped.
/// </summary>
public static class ModManifests
{
    /// <summary>The manifest's file name.</summary>
    public const string FileName = "worldeditor.json";

    /// <summary>The format version this editor reads ("version"); a manifest of a later one is skipped.</summary>
    public const int Version = 1;

    /// <summary>A manifest larger than this is not read.</summary>
    private const long MaxFile = 1 << 20;

    /// <summary>
    /// What a manifest says of a data key.
    /// </summary>
    /// <param name="Label">How the inspector names it (else its key's name).</param>
    /// <param name="ReadOnly">The inspector shows it without letting it be changed.</param>
    /// <param name="Help">What it is, shown when pointing at it.</param>
    public sealed record DataNote(string? Label, bool ReadOnly, string? Help);

    /// <summary>
    /// What a manifest says of a prefab.
    /// </summary>
    /// <param name="Hidden">Not offered in Place or the Build panel (objects already in a world still show).</param>
    /// <param name="Group">The heading it is listed under in the Mods tab (else its crafting station).</param>
    public sealed record PrefabNote(bool Hidden, string? Group);

    /// <summary>
    /// What the manifests say.
    /// </summary>
    /// <param name="Data">By data key name.</param>
    /// <param name="Prefabs">By prefab name.</param>
    private sealed record Notes(Dictionary<string, DataNote> Data, Dictionary<string, PrefabNote> Prefabs);

    /// <summary>What is in use, swapped in whole.</summary>
    private static volatile Notes _notes = new([], []);

    /// <summary>Counts each time what is in use is replaced, for lists worked out from it to be worked out again.</summary>
    public static int Generation => _generation;
    /// <summary>Backs <see cref="Generation"/>.</summary>
    private static volatile int _generation;

    /// <summary>What a manifest says of a data key; null when none says anything.</summary>
    public static DataNote? DataOf(string? key) => key == null ? null : _notes.Data.GetValueOrDefault(key);

    /// <summary>What a manifest says of a prefab; null when none says anything.</summary>
    public static PrefabNote? PrefabOf(string? prefab) => prefab == null ? null : _notes.Prefabs.GetValueOrDefault(prefab);

    /// <summary>Whether a manifest asks for a prefab not to be offered.</summary>
    public static bool IsHidden(string? prefab) => PrefabOf(prefab)?.Hidden == true;

    /// <summary>
    /// Reads every manifest in the plugins folders and uses what they say (nothing when there is none). Never throws.
    /// </summary>
    /// <returns>How many manifests were read.</returns>
    public static int Refresh(AppSettings settings)
    {
        var files = new List<FileInfo>();
        foreach (var (dir, _) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            try
            {
                if (Directory.Exists(plugins))
                {
                    files.AddRange(Directory.EnumerateFiles(plugins, FileName, SearchOption.AllDirectories).Select(f => new FileInfo(f)));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be listed: its manifests are not read.
            }
        }
        var notes = new Notes([], []);
        int read = 0;
        // Oldest first: a newer file's notes replace an older one's.
        foreach (var file in files.DistinctBy(f => f.FullName).OrderBy(f => f.LastWriteTimeUtc))
        {
            try
            {
                if (file.Length <= MaxFile && Read(File.ReadAllText(file.FullName), notes))
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
                Console.WriteLine($"mod manifest {file.FullName}: not read ({e.Message}).");
            }
        }
        _notes = notes;
        _generation++;
        return read;
    }

    /// <summary>Nothing in use any more (the tests).</summary>
    internal static void Forget()
    {
        _notes = new([], []);
        _generation++;
    }

    /// <summary>
    /// Adds what a manifest says to <paramref name="notes"/>; false (nothing added) when it is not a manifest of a
    /// version this editor reads. Entries not in the expected form are left out.
    /// </summary>
    /// <exception cref="JsonException">Not JSON.</exception>
    private static bool Read(string json, Notes notes)
    {
        if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is not JsonObject root
            || root["version"] is not JsonValue v || !v.TryGetValue(out int version) || version < 1 || version > Version)
        {
            return false;
        }
        if (root["data"] is JsonObject data)
        {
            foreach (var (key, e) in data)
            {
                if (e is JsonObject o && Text(o["label"], out string? label) && Flag(o["readOnly"], out bool readOnly) && Text(o["help"], out string? help))
                {
                    notes.Data[key] = new DataNote(label, readOnly, help);
                }
            }
        }
        if (root["prefabs"] is JsonObject prefabs)
        {
            foreach (var (name, e) in prefabs)
            {
                if (e is JsonObject o && Flag(o["hidden"], out bool hidden) && Text(o["group"], out string? group))
                {
                    notes.Prefabs[name] = new PrefabNote(hidden, group);
                }
            }
        }
        return true;
    }

    /// <summary>An optional string: absent (null) or a non-empty string of at most 200 characters.</summary>
    private static bool Text(JsonNode? n, out string? text)
    {
        text = null;
        if (n is null)
        {
            return true;
        }
        if (n is JsonValue v && v.TryGetValue(out string? s) && s.Trim().Length is > 0 and <= 200)
        {
            text = s.Trim();
            return true;
        }
        return false;
    }

    /// <summary>An optional true or false: absent (false) or a boolean.</summary>
    private static bool Flag(JsonNode? n, out bool flag)
    {
        flag = false;
        return n is null || n is JsonValue v && v.TryGetValue(out flag);
    }
}
