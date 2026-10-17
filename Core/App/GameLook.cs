namespace TerrainEditor.App;

/// <summary>
/// The game's own look (models, textures, the terrain shader, the map's textures) is read from the
/// user's own Valheim install, never shipped nor copied: this finds the install and keeps the one
/// reader of its asset bundles (GameBundles). The first time, and after a game update, the bundles are
/// indexed (a few seconds, in the background; the index is kept in a file).
/// </summary>
public static partial class GameLook
{
    /// <summary>
    /// Valheim's Steam app id (its Proton prefix under compatdata is named by it).
    /// </summary>
    public const int ValheimAppId = 892970;

    /// <summary>
    /// Guards the bundles reader and the state.
    /// </summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// The reader of the game's bundles in use.
    /// </summary>
    private static GameBundles? _bundles;

    /// <summary>
    /// How far reading the game's files is, as last told by the reading (null before its first step). Set without the
    /// lock: the reading tells it from inside the bundles reader's own lock, which Check takes under this one.
    /// </summary>
    private static volatile Told? _told;

    /// <summary>
    /// A step of the reading: the reader it is about, how far (0..1) and what is being read.
    /// </summary>
    private sealed record Told(GameBundles Game, double Progress, string Step);

    /// <summary>
    /// Where older editors copied the game's look (game-look): deleted (<c>DeleteOldCopy</c>), nothing reads it
    /// any more.
    /// </summary>
    public static string Dir => Path.Combine(AppSettings.UserDataDir, "game-look");

    /// <summary>
    /// Where older editors kept the copy's cache: deleted too (<c>DeleteOldCopy</c>).
    /// </summary>
    private static string OldCache => Path.Combine(AppSettings.UserDataDir, "export-cache");

    /// <summary>
    /// The bundles' index (prefab -&gt; bundle), kept between runs.
    /// </summary>
    public static string IndexFile => Path.Combine(AppSettings.UserDataDir, "game-index.json");

    /// <summary>
    /// False: Steam is only looked for under the home folder (not /opt/Steam, /usr/share/steam or
    /// Program Files): the editor driven by the tests and the documentation's pictures, whose stand-in
    /// home must not lead to the computer's own game.
    /// </summary>
    public static bool SearchOutsideHome { get; set; } = true;

    /// <summary>
    /// "ready", "missing" (Valheim not found), "running" (indexing its bundles, or at start
    /// checking the game data kept), "failed".
    /// </summary>
    public static string State { get; private set; } = "missing";

    /// <summary>
    /// The state's message for the start page, null when none.
    /// </summary>
    public static string? Message { get; private set; }

    /// <summary>
    /// The Valheim folder found by the last <c>Check</c>, null when none.
    /// </summary>
    public static string? ValheimPath { get; private set; }

    /// <summary>
    /// The reader of the game's bundles (null when Valheim is not found).
    /// </summary>
    public static GameBundles? Bundles
    {
        get
        {
            lock (Lock)
            {
                return _bundles;
            }
        }
    }

    /// <summary>
    /// What the start page shows.
    /// </summary>
    /// <param name="State">The state (see <c>State</c>).</param>
    /// <param name="Message">Its message.</param>
    /// <param name="Valheim">The Valheim folder.</param>
    /// <param name="LastLine">What is being read, while running; else null.</param>
    /// <param name="Progress">The progress (0..1, null when not known).</param>
    /// <param name="Reading">While running: whether the game's files are being read (false while only the kept index
    /// and caches are checked, a moment at each start).</param>
    public sealed record Snapshot(string State, string? Message, string? Valheim, string? LastLine, double? Progress, bool Reading = true);

    /// <summary>
    /// The state as it is now.
    /// </summary>
    public static Snapshot Now()
    {
        lock (Lock)
        {
            bool running = State == "running";
            var told = running && _told is { } t && ReferenceEquals(t.Game, _bundles) ? t : null;
            return new Snapshot(State, Message, ValheimPath, told?.Step, State == "ready" ? 1 : told?.Progress, !running || told != null);
        }
    }

    /// <summary>
    /// At start and when the game folder changes: finds Valheim and indexes its bundles (in the
    /// background when the kept index is out of date).
    /// </summary>
    public static void Check(AppSettings settings)
    {
        string? valheim = FindValheim(settings.ValheimPath);
        GameBundles? game;
        lock (Lock)
        {
            ValheimPath = valheim;
            if (valheim == null)
            {
                _bundles = null;
                Set("missing", "Valheim was not found on this computer. Choose its folder to get the game's look.");
                return;
            }
            if (_bundles == null || !string.Equals(_bundles.Folder, BundlesDir(valheim), StringComparison.Ordinal))
            {
                _bundles = GameBundles.ForGame(valheim, IndexFile);
            }
            game = _bundles;
            if (game == null)
            {
                Set("failed", "The game's files could not be read.");
                return;
            }
            if (game.Indexed)
            {
                // Running without reading: the rest of the game data checked too (from the caches: at once), so
                // "ready" is said only once it is in use.
                Set("running", null);
                Task.Run(() => CheckGameData(game));
                return;
            }
            Set("running", "Reading the game's files (a few seconds, only after a game update).");
        }
        Task.Run(() => IndexBundles(game));
    }

    /// <summary>
    /// The message when the game data (GameData.Ensure) cannot be had.
    /// </summary>
    private const string DataUnread = "Some of the game's data could not be read (see the log).";

    /// <summary>
    /// Use this Valheim folder: kept in the settings, then read. The problem, or null.
    /// </summary>
    public static string? Start(string valheim, AppSettings settings)
    {
        valheim = valheim.Trim().Trim('"');
        if (GameFolder(valheim) is not string game)
        {
            return "That folder is not Valheim's game folder: it has no valheim_Data (on a Mac: no valheim.app), and Valheim is not inside it. In Steam, right-click Valheim, then Manage › Browse local files: choose the folder that opens.";
        }
        valheim = game;
        settings.ValheimPath = valheim;
        settings.Save();
        Check(settings);
        return null;
    }

    /// <summary>
    /// Sets the state and its message, logging a change.
    /// </summary>
    private static void Set(string state, string? message)
    {
        if (state != State || message != Message)
        {
            Console.WriteLine($"game look: {state}{(message == null ? "" : ": " + message)}");
        }
        State = state;
        Message = message;
        _told = null;
    }

    /// <summary>
    /// Sets the state a background reading ended with, unless the game folder has changed since it began.
    /// </summary>
    /// <param name="game">The reader the reading was about.</param>
    /// <param name="state">The state it ended in.</param>
    /// <param name="message">Its message, or null.</param>
    internal static void FinishReading(GameBundles game, string state, string? message)
    {
        lock (Lock)
        {
            if (ReferenceEquals(_bundles, game))
            {
                Set(state, message);
            }
        }
    }
}
