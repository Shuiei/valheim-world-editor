namespace TerrainEditor.App;

/// <summary>
/// What the editor reads of the game's files at its start (GameData), in C# on the same reader as the
/// models: the kinds found in the bundles, all in one pass over them (ReadAtStart), and those with their
/// own readers. Each is written as CatalogueJson writes it.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// The kinds read in the one pass over the bundles, each found from the scripts it needs up to their
    /// prefabs, not by reading every object of every bundle (seconds, not minutes). Each bundle is opened
    /// once and given to each kind's reader, in the order of their file names: the order entries come in,
    /// and which one wins when a name is found twice.
    /// </summary>
    public static readonly string[] AtStart = ["terrain-modifiers", "prefabs", "pieces", "piece-support", "piece-place", "piece-cost", "dungeon-rooms", "wake-draws"];

    /// <summary>
    /// The kinds named (of AtStart), as their JSON. Throws ArgumentException for a kind not in <c>AtStart</c>.
    /// </summary>
    /// <param name="bundles">The game's bundles (its folder is what is read).</param>
    /// <param name="kinds">The kinds to read, of <c>AtStart</c>.</param>
    /// <param name="progress">Told the bundles read, of all of them, before each.</param>
    /// <returns>Each kind's JSON, by kind.</returns>
    public static Dictionary<string, string> ReadAtStart(GameBundles bundles, IReadOnlyList<string> kinds, Action<int, int>? progress = null)
    {
        var scans = kinds.Select(k => (Kind: k, Scan: k switch
        {
            "terrain-modifiers" => (IStartScan)new ModifierScan(),
            "prefabs" => new PrefabScan(),
            "pieces" => new PieceScan(),
            "piece-support" => new SupportScan(),
            "piece-place" => new PlaceScan(),
            "piece-cost" => new CostScan(Localization.English(bundles.Folder)),
            "dungeon-rooms" => new DungeonScan(),
            "wake-draws" => new WakeScan(),
            _ => throw new ArgumentException($"no kind {k} is read at the start"),
        })).ToList();
        using var game = new Game(bundles.Folder);
        game.IndexCabs();
        int done = 0;
        foreach (string name in game.Bundles)
        {
            progress?.Invoke(done++, game.Bundles.Count);
            using var b = game.Open(name);
            if (b != null)
            {
                foreach (var (_, scan) in scans)
                {
                    scan.Read(b, game);
                }
            }
        }
        return scans.ToDictionary(s => s.Kind, s => s.Scan.Json());
    }

    /// <summary>
    /// One kind's reading of every bundle at the editor's start (the game's other bundles are found
    /// through game), and its JSON.
    /// </summary>
    private interface IStartScan
    {
        /// <summary>
        /// Reads what this kind needs of one bundle; <c>game</c> gives the others (scripts, references).
        /// </summary>
        void Read(Bundle b, Game game);

        /// <summary>
        /// The kind's JSON, once every bundle is read.
        /// </summary>
        string Json();
    }

    // ---- The bundles.

    /// <summary>
    /// An object of another internal file of the bundles: the file's name (lower case) and its path id.
    /// </summary>
    /// <param name="Cab">The internal file's name, lower case.</param>
    /// <param name="PathId">The object's path id in that file.</param>
    internal readonly record struct Ref(string Cab, long PathId);
}
