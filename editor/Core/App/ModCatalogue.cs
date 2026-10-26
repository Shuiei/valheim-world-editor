using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// The prefabs mods add to the game, as WorldEditorBridge reports them (GET /catalogue) when a live world opens:
/// what the editor's own reading of the game (GameData) cannot know, since mods make most of their prefabs while
/// the game runs. Kept in the data folder (mod-catalogue.json), every mod prefab ever seen, the newest report of
/// each winning, so worlds of modded servers read offline show them too. GameData lays them over the game's own
/// prefabs, pieces and costs (the game's win: a mod's report never changes a vanilla prefab).
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>The kinds of GameData the mods' entries are laid over, with the part of the report each takes.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["prefabs"] = "prefabs",
        ["pieces"] = "pieces",
        ["piece-cost"] = "cost",
    };

    /// <summary>The kept catalogue in the data folder.</summary>
    public static string FilePath => Path.Combine(AppSettings.DataDir, "mod-catalogue.json");

    /// <summary>
    /// What is known of the mods' prefabs.
    /// </summary>
    /// <param name="Prefabs">Prefab entries by name (PrefabCatalog's format).</param>
    /// <param name="Pieces">Piece entries by name (PieceCatalog's format).</param>
    /// <param name="Costs">Piece costs by name (PieceCost's "pieces").</param>
    /// <param name="Items">Item names by prefab name (PieceCost's "items"), for the costs' items.</param>
    /// <param name="Mods">The mod that adds each prefab, when the game knew it (with Jötunn).</param>
    /// <param name="Models">What the game draws of each prefab (GET /models), when it was asked.</param>
    private sealed record Data(Dictionary<string, JsonNode> Prefabs, Dictionary<string, JsonNode> Pieces, Dictionary<string, JsonNode> Costs, Dictionary<string, string> Items, Dictionary<string, string> Mods,
        Dictionary<string, JsonNode> Models)
    {
        /// <summary>
        /// <see cref="Models"/> read into records when put in use (<see cref="Use"/>, under the lock): the JSON nodes
        /// are read on first use, which two threads must not do at once, and ModelOf runs on the model loaders' threads
        /// and the UI's.
        /// </summary>
        public IReadOnlyDictionary<string, Model> Parsed { get; init; } = new Dictionary<string, Model>();
    }

    /// <summary>Nothing known: before Load, and when the kept file is missing or cannot be read.</summary>
    private static Data Empty() => new([], [], [], [], [], []);

    /// <summary>
    /// One part of a mod prefab's model, as the game draws it: found by name in the mods' and the game's bundles.
    /// </summary>
    /// <param name="Mesh">The mesh's name (without the " Instance" of a copy made while the game runs).</param>
    /// <param name="Vertices">The mesh's vertex count, to tell apart meshes of the same name.</param>
    /// <param name="Sub">The submesh drawn.</param>
    /// <param name="Material">The material's name (without " (Instance)"), when it has one.</param>
    /// <param name="Matrix">Its place in the prefab's frame: Unity's matrix, row by row (as GameBundles.Part's).</param>
    /// <param name="Bounds">The mesh's bounds, centre and extent (x, y, z each), when the game told them.</param>
    public sealed record ModelPart(string Mesh, int Vertices, int Sub, string? Material, float[] Matrix, float[]? Bounds = null);

    /// <summary>
    /// What the game draws of a mod prefab.
    /// </summary>
    /// <param name="RootScale">The prefab root's scale.</param>
    /// <param name="Parts">Its parts.</param>
    public sealed record Model(float[] RootScale, IReadOnlyList<ModelPart> Parts);

    /// <summary>
    /// What the game draws of a mod prefab, as it reported it; null when it was not asked or the prefab is not a mod's.
    /// </summary>
    public static Model? ModelOf(string prefab) => _data.Parsed.GetValueOrDefault(prefab);

    /// <summary>
    /// Taken by each change to what is known (Load, Take, TakeModels, Forget): each starts from what the one before
    /// left, so none loses another's update.
    /// </summary>
    private static readonly Lock Writing = new();

    /// <summary>What is known now, swapped in whole (<see cref="Use"/>).</summary>
    private static volatile Data _data = Empty();

    /// <summary>Counts each time what is known is replaced, for lists worked out from it to be worked out again.</summary>
    public static int Generation => _generation;
    /// <summary>Backs <see cref="Generation"/>.</summary>
    private static volatile int _generation;

    /// <summary>Puts what is known in use, counting it in <see cref="Generation"/>.</summary>
    private static void Use(Data next)
    {
        _data = next with { Parsed = next.Models.ToDictionary(kv => kv.Key, kv => ToModel(kv.Value.AsObject()), StringComparer.Ordinal) };
        Interlocked.Increment(ref _generation);
    }

    /// <summary>How many mod prefabs are known.</summary>
    public static int Count => _data.Prefabs.Count;

    /// <summary>The names of the mod prefabs known.</summary>
    public static IEnumerable<string> Names => _data.Prefabs.Keys;

    /// <summary>The mod that adds a prefab, by prefab name; null for the game's own and when the game did not know it.</summary>
    public static string? ModOf(string prefab) => _data.Mods.GetValueOrDefault(prefab);

    /// <summary>Whether a prefab was reported by a game and is not the game's own.</summary>
    public static bool IsModded(string prefab) => _data.Prefabs.ContainsKey(prefab);

    /// <summary>
    /// The kept catalogue in use (nothing when there is none or it cannot be read), without the prefabs that are the
    /// game's own now (a game update made them vanilla).
    /// </summary>
    /// <param name="vanilla">The game's own prefab names (GameData's prefabs).</param>
    public static void Load(IReadOnlySet<string> vanilla)
    {
        lock (Writing)
        {
            try
            {
                var kept = File.Exists(FilePath) ? Read(File.ReadAllText(FilePath)) : Empty();
                DropVanilla(kept, vanilla);
                Use(kept);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                Log.Caught(e, "reading the kept mod catalogue (starting without it)");
                Use(Empty());
            }
        }
    }

    /// <summary>
    /// Nothing known any more (the tests).
    /// </summary>
    internal static void Forget()
    {
        lock (Writing)
        {
            Use(Empty());
        }
    }
}
