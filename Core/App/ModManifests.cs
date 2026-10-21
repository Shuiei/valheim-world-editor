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
public static partial class ModManifests
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
    /// <param name="GameComponents">The game's scripts whose fields it asks for ("gameComponents"; ModComponents reads them).</param>
    public sealed record Offered(string Dll, string Mod, IReadOnlyList<string> GameComponents);

    /// <summary>
    /// What a manifest says of a prefab.
    /// </summary>
    /// <param name="Hidden">Not offered in Place or the Build panel (objects already in a world still show).</param>
    /// <param name="Group">The heading it is listed under in the Mods tab (else its crafting station).</param>
    /// <param name="Data">
    /// Objects of it only hold the mod's data (no model): never drawn, picked or listed in an area, and kept by a zone reset
    /// that keeps the buildings.
    /// </param>
    public sealed record PrefabNote(bool Hidden, string? Group, bool Data = false);

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

        /// <summary>The hashes of the prefabs whose objects only hold data, worked out once the notes are complete.</summary>
        public HashSet<int> DataPrefabs => field ??= [.. Prefabs.Where(p => p.Value.Data).Select(p => StableHash.Of(p.Key))];
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
            Interlocked.Increment(ref _generation);
        }
    } = [];

    /// <summary>What <see cref="Base"/> says, read once: each refresh starts from a copy, so its rules stay the same instances.</summary>
    private static Notes? _baseNotes;

    /// <summary>What is in use, swapped in whole.</summary>
    private static volatile Notes _notes = new([], [], [], []);

    /// <summary>Counts each time what is in use is replaced, for lists worked out from it to be worked out again.</summary>
    public static int Generation => _generation;
    /// <summary>Backs <see cref="Generation"/>.</summary>
    private static volatile int _generation;

    /// <summary>What a manifest says of a data key; null when none says anything.</summary>
    public static DataNote? DataOf(string? key) => key == null ? null : _notes.Data.GetValueOrDefault(key);

    /// <summary>What a manifest says of a prefab; null when none says anything.</summary>
    public static PrefabNote? PrefabOf(string? prefab) => prefab == null ? null : _notes.Prefabs.GetValueOrDefault(prefab);

    /// <summary>Whether a manifest says the objects of a prefab only hold its mod's data (<see cref="PrefabNote.Data"/>).</summary>
    /// <param name="prefab">The prefab's hash.</param>
    public static bool IsData(int prefab) => _notes.DataPrefabs.Contains(prefab);

    /// <summary>The hashes of every prefab whose objects only hold a mod's data.</summary>
    public static IReadOnlyCollection<int> DataPrefabs => _notes.DataPrefabs;

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

    /// <summary>Only <see cref="Base"/> in use (the tests).</summary>
    internal static void Forget()
    {
        _notes = BaseNotes();
        Interlocked.Increment(ref _generation);
    }
}
