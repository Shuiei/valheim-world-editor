using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// What mod authors can tell the editor about their mod, in a worldeditor.json shipped with it (anywhere in its
/// folder under BepInEx/plugins; docs/modding.md, docs/worldeditor.schema.json): labels, help and read-only flags for
/// the data their objects keep (the inspector), prefabs the editor should not offer (tools, effects, helpers), groups
/// for their pieces (the Build panel's Mods tab), how their objects link into one (ObjectLinks: a ship and its
/// pieces), and the editor extension they offer (ModExtensions). Everything else the editor learns by itself (ModCatalogue, ModKeys). Read from the plugins folders the
/// editor knows (LocalGame.BepInExFolders) as a world or the Workshop opens; of two manifests saying something of the
/// same name, the newest file's wins. A file that is not a manifest is skipped. The editor ships none for any mod:
/// supporting a mod is its author's to do.
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

    /// <summary>An editor extension a mod offers (its manifest's "extension").</summary>
    /// <param name="Dll">The extension's DLL, its full path (inside the mod's folder).</param>
    /// <param name="Mod">The mod, as its folder under plugins is named.</param>
    public sealed record Offered(string Dll, string Mod);

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
    /// <param name="Links">How objects link into one, by the name of the id key (in the order first read).</param>
    /// <param name="Extensions">The extensions mods offer (ModExtensions), by their DLL's full path.</param>
    private sealed record Notes(Dictionary<string, DataNote> Data, Dictionary<string, PrefabNote> Prefabs, OrderedDictionary<string, LinkRule> Links,
        Dictionary<string, Offered> Extensions)
    {
        /// <summary>The link rules, with the keys they read.</summary>
        public LinkSet LinkSet { get; set; } = LinkSet.Empty;
    }

    /// <summary>
    /// Manifests read before the mods' own, as a mod's would be: none in the editor. The tests give their made-up
    /// example mod's here (before anything is read), so every test sees it whatever another test refreshes.
    /// </summary>
    internal static IReadOnlyList<string> Base
    {
        get;
        set
        {
            field = value;
            _baseNotes = null;
            _notes = BaseNotes();
            _generation++;
        }
    } = [];

    /// <summary>What <see cref="Base"/> says, read once: each refresh starts from a copy, so its rules stay the same instances.</summary>
    private static Notes? _baseNotes;

    /// <summary>What is in use, swapped in whole.</summary>
    private static volatile Notes _notes = new([], [], []);

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

    /// <summary>How the manifests say objects link into one (ObjectLinks).</summary>
    public static LinkSet Links => _notes.LinkSet;

    /// <summary>The editor extensions mods offer (ModExtensions decides which run), in no particular order.</summary>
    public static IReadOnlyCollection<Offered> Extensions => _notes.Extensions.Values;

    /// <summary>A copy of what <see cref="Base"/> says (a manifest there that cannot be read is a bug: it throws).</summary>
    private static Notes BaseNotes()
    {
        if (_baseNotes is not { } once)
        {
            once = new Notes([], [], [], []);
            foreach (string json in Base)
            {
                if (!Read(json, once, null))
                {
                    throw new InvalidDataException("a base manifest is not a worldeditor.json this editor reads.");
                }
            }
            _baseNotes = once;
        }
        var notes = new Notes(new(once.Data), new(once.Prefabs), new(once.Links), new(once.Extensions));
        notes.LinkSet = new LinkSet([.. notes.Links.Values]);
        return notes;
    }

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
            }
        }
        var notes = BaseNotes();
        int read = 0;
        // Oldest first: a newer file's notes replace an older one's.
        foreach (var (file, plugins) in files.DistinctBy(f => f.File.FullName).OrderBy(f => f.File.LastWriteTimeUtc))
        {
            try
            {
                // The mod: its folder right under plugins (the manifest's own when it lies there).
                string folder = file.DirectoryName!;
                string relative = Path.GetRelativePath(plugins, folder);
                string mod = relative == "." ? Path.GetFileName(folder) : relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (file.Length <= MaxFile && Read(File.ReadAllText(file.FullName), notes, (folder, mod)))
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
        notes.LinkSet = new LinkSet([.. notes.Links.Values]);
        _notes = notes;
        _generation++;
        return read;
    }

    /// <summary>Only <see cref="Base"/> in use (the tests).</summary>
    internal static void Forget()
    {
        _notes = BaseNotes();
        _generation++;
    }

    /// <summary>
    /// Adds what a manifest says to <paramref name="notes"/>; false (nothing added) when it is not a manifest of a
    /// version this editor reads. Entries not in the expected form are left out.
    /// </summary>
    /// <param name="json">The manifest's text.</param>
    /// <param name="notes">What is added to.</param>
    /// <param name="from">The manifest's folder and its mod (null for one given in code, which offers no extension).</param>
    /// <exception cref="JsonException">Not JSON.</exception>
    private static bool Read(string json, Notes notes, (string Folder, string Mod)? from)
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
        if (root["links"] is JsonArray links)
        {
            foreach (var e in links)
            {
                if (e is JsonObject o && Link(o) is var (id, rule))
                {
                    // In place of one of the same id, where it stood: the first rule an object matches wins.
                    notes.Links[id] = rule;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// A link rule (ObjectLinks) and the name of its id key; null when it is not in the expected form: "id", "parents"
    /// (at least one) and "offset" given; "rotation", "rotationQuaternion", "references", "dropOnCopy", "name" and
    /// "storedAt" ("root", the default, or "own") optional.
    /// </summary>
    private static (string Id, LinkRule Rule)? Link(JsonObject o)
    {
        if (!Text(o["id"], out string? id) || id == null || !Names(o["parents"], out string[] parents) || parents.Length == 0
            || !Text(o["offset"], out string? offset) || offset == null || !Text(o["rotation"], out string? euler)
            || !Text(o["rotationQuaternion"], out string? quat) || !Names(o["references"], out string[] references)
            || !Names(o["dropOnCopy"], out string[] drops) || !Text(o["name"], out string? name) || !Text(o["storedAt"], out string? stored)
            || stored is not (null or "root" or "own"))
        {
            return null;
        }
        static int Key(string? n) => n == null ? 0 : StableHash.Of(n);
        return (id, new LinkRule(name ?? LinkRule.DefaultName, Key(id), [.. parents.Select(Key)], Key(offset), Key(euler), Key(quat),
            [.. references.Select(Key)], [.. drops.Select(Key)], stored != "own"));
    }

    /// <summary>An optional list of names: absent (empty) or an array of at most 32 texts.</summary>
    private static bool Names(JsonNode? n, out string[] names)
    {
        names = [];
        if (n is null)
        {
            return true;
        }
        if (n is not JsonArray a || a.Count > 32)
        {
            return false;
        }
        var list = new List<string>();
        foreach (var e in a)
        {
            if (!Text(e, out string? s) || s == null)
            {
                return false;
            }
            list.Add(s);
        }
        names = [.. list];
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
