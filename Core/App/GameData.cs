using System.Text.Json;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// What the editor needs of the game besides its look, read from the player's own Valheim: the editor
/// keeps no copy, which would go out of date with each game update (and players have different
/// versions). Each kind is read at the editor's start, in seconds (those found in the bundles all in one
/// pass over them), and kept in the data folder (game-&lt;kind&gt;.json) until the game's files change.
/// Without them the editor cannot work right (the ground near locations, the names of object data...), so
/// no world or Workshop opens until Valheim is found (Ensure). The tests, and the smoke tests through the
/// driver, use trimmed copies in tests/fixtures (UseCopies), made again after a game update with
/// --export-test-data.
/// </summary>
public static partial class GameData
{
    /// <summary>
    /// One kind of game data.
    /// </summary>
    /// <param name="Name">The kind's name: the files' &lt;name&gt;.json.</param>
    /// <param name="Read">How it is read from the game (Valheim's folder, its bundles -&gt; JSON); null: in the one
    /// pass over the bundles, GameCatalogues.ReadAtStart.</param>
    /// <param name="Use">Who takes it: given the JSON.</param>
    /// <param name="Trim">How the tests' copy is trimmed (the game's and the copy's JSON -&gt; what the copy keeps);
    /// null: the entries the copy has.</param>
    private sealed record Kind(string Name, Func<string, GameBundles, string>? Read, Action<string> Use, Func<JsonElement, JsonElement, object>? Trim = null);

    /// <summary>
    /// Every kind, in the order they are read and used.
    /// </summary>
    private static readonly Kind[] Kinds =
    [
        new("terrain-modifiers", null, TerrainModifiers.Use),
        new("prefabs", null, PrefabCatalog.Use),
        new("pieces", null, PieceCatalog.Use),
        new("piece-support", null, Editing.Stability.Use),
        new("piece-place", null, Editing.Hammer.Use),
        new("zdo-keys", (valheim, _) => GameCatalogues.ZdoKeysJson(valheim), ZdoKeys.Use),
        new("cave-rocks", (_, bundles) => GameCatalogues.CaveRocksJson(bundles), CaveRocks.Use),
        new("piece-cost", null, PieceCost.Use, TrimPieceCost),
        new("vegetation", (_, bundles) => GameCatalogues.VegetationJson(bundles), Regrow.Use, TrimVegetation),
        new("room-themes", (valheim, _) => GameCatalogues.RoomThemesJson(valheim), Editing.Dungeons.UseThemes),
        new("dungeon-rooms", null, json =>
        {
            Editing.Dungeons.Use(json);
            Editing.LocationPrefabs.Use(json);
        }, TrimDungeons),
        new("wake-draws", null, WakeDraws.Use),
    ];

    /// <summary>
    /// The kinds' names, in the order they are read and used.
    /// </summary>
    internal static IEnumerable<string> KindNames => Kinds.Select(k => k.Name);

    /// <summary>
    /// What opening a world says when Valheim's files cannot be read.
    /// </summary>
    public const string Missing = "Valheim was not found, and the editor needs its files (the game's look and what it knows of the game's objects). Choose its folder on the start page.";

    /// <summary>
    /// What a cache was written by: data read by another editor build is read again.
    /// </summary>
    private static readonly string CacheVersion = typeof(GameData).Assembly.ManifestModule.ModuleVersionId.ToString();

    /// <summary>
    /// Serialises loading and using the data.
    /// </summary>
    private static readonly Lock Lock = new();
    /// <summary>
    /// Where the data in use came from: a bundles folder, Copies, or none yet.
    /// </summary>
    private static volatile string? _from;
    /// <summary>
    /// The game's own JSON of the kinds the mods' prefabs are laid over (ModCatalogue.Kinds), as last used: what
    /// AddMods lays them over again.
    /// </summary>
    private static readonly Dictionary<string, string> Own = [];

    /// <summary>
    /// Counts each time the data in use changes (a game's data, the tests' copies, mods' prefabs learned), for lists
    /// worked out from it to be worked out again.
    /// </summary>
    public static int Generation => _generation;
    /// <summary>
    /// Whether some game data is in use (Ensure done, or the tests' copies): before, at the editor's start, the
    /// catalogues are empty (no item, no piece is known).
    /// </summary>
    public static bool InUse => _from != null;
    /// <summary>Backs <see cref="Generation"/>.</summary>
    private static volatile int _generation;
    /// <summary>
    /// <c>_from</c> when the tests' copies are in use.
    /// </summary>
    private const string Copies = "(copies)";

    /// <summary>
    /// Every kind in use for this game: from the caches, else read from the game; already in use, nothing
    /// to do. False when they cannot all be had: no Valheim, or its files could not be read. The tests'
    /// copies (UseCopies) count as had, whatever game folder the tests stand in.
    /// </summary>
    /// <param name="bundles">The game's bundles; null: no game.</param>
    /// <param name="progress">Told how far the reading is (0..1) and what it reads, while it reads from the game.</param>
    public static bool Ensure(GameBundles? bundles, Action<double, string>? progress = null)
    {
        lock (Lock)
        {
            if (_from == Copies)
            {
                return true;
            }
            if (bundles == null)
            {
                return false;
            }
            if (_from == bundles.Folder)
            {
                return true;
            }
            if (Load(bundles, cache: true, progress) is not { } read)
            {
                return false;
            }
            try
            {
                ModCatalogue.Load(PrefabNames(read["prefabs"]));
                foreach (var kind in Kinds)
                {
                    Use(kind, read[kind.Name]);
                }
            }
            catch (Exception e)
            {
                // A kind the editor cannot take (the game changed what it holds): those before it are in use already,
                // so none counts as had, and the next Ensure takes them all again.
                Log.Caught(e, "using the game's data (none counts as had; taken again next time)");
                _from = null;
                _generation++;
                return false;
            }
            _from = bundles.Folder;
            _generation++;
            return true;
        }
    }

    /// <summary>
    /// The tests' copies (&lt;folder&gt;/&lt;kind&gt;.json) in use, for where there is no game: the tests, and the
    /// smoke tests through the driver.
    /// </summary>
    public static void UseCopies(string folder)
    {
        lock (Lock)
        {
            foreach (var kind in Kinds)
            {
                Use(kind, File.ReadAllText(Path.Combine(folder, kind.Name + ".json")));
            }
            _from = Copies;
            _generation++;
        }
    }

    /// <summary>
    /// No data in use any more (the tests: Ensure is then tried as the editor does at its start).
    /// </summary>
    internal static void Forget()
    {
        lock (Lock)
        {
            _from = null;
        }
    }
}
