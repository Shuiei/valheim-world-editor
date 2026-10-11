using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// The game's own look (models, textures, the terrain shader, the map's textures) is read from the
/// user's own Valheim install, never shipped nor copied: this finds the install and keeps the one
/// reader of its asset bundles (GameBundles). The first time, and after a game update, the bundles are
/// indexed (a few seconds, in the background; the index is kept in a file).
/// </summary>
public static class GameLook
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

    /// <summary>How far reading the game's files is (0..1), while running; null before the first step is told.</summary>
    private static double? _progress;

    /// <summary>What is being read, while running.</summary>
    private static string? _step;

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
    /// "ready", "missing" (Valheim not found), "running" (indexing its bundles), "failed".
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
    public sealed record Snapshot(string State, string? Message, string? Valheim, string? LastLine, double? Progress);

    /// <summary>
    /// The state as it is now.
    /// </summary>
    public static Snapshot Now()
    {
        lock (Lock)
        {
            bool running = State == "running";
            return new Snapshot(State, Message, ValheimPath, running ? _step : null, State == "ready" ? 1 : running ? _progress : null);
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
                Set("ready", null);
                // The rest of the game data too, ready before a world is opened (from the caches: at once).
                Task.Run(() => GameData.Ensure(game));
                return;
            }
            Set("running", "Reading the game's files (a few seconds, only after a game update).");
        }
        Task.Run(() =>
        {
            string state, message;
            // The bar: the bundles' index first (a tenth: its table of contents only), then the game data.
            void Report(double progress, string step)
            {
                lock (Lock)
                {
                    if (ReferenceEquals(_bundles, game))
                    {
                        _progress = progress;
                        _step = step;
                    }
                }
            }
            try
            {
                game.EnsureIndexed((done, count) => Report(0.1 * done / Math.Max(1, count), $"Indexing the game's bundles: {done} of {count}"));
                int n = game.Index.Count;
                (state, message) = n == 0 ? ("failed", "No models were found in the game's files.")
                    : !GameData.Ensure(game, (p, step) => Report(0.1 + (0.9 * p), step)) ? ("failed", "Some of the game's data could not be read (see the log).")
                    : ("ready", "The game's look is ready.");
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or NotImplementedException)
            {
                Console.WriteLine($"game look: {e}");
                (state, message) = ("failed", "The game's files could not be read: " + e.Message);
            }
            lock (Lock)
            {
                if (ReferenceEquals(_bundles, game))
                {
                    Set(state, message);
                }
            }
        });
    }

    /// <summary>
    /// Use this Valheim folder: kept in the settings, then read. The problem, or null.
    /// </summary>
    public static string? Start(string valheim, AppSettings settings)
    {
        valheim = valheim.Trim().Trim('"');
        if (GameFolder(valheim) is not string game)
        {
            return "That folder is not Valheim's game folder: it has no valheim_Data, and Valheim is not inside it. In Steam, right-click Valheim, then Manage › Browse local files: choose the folder that opens.";
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
        _progress = null;
        _step = null;
    }

    /// <summary>
    /// The copy older editors made (up to 1.16): about 150 MB nothing reads any more. Called by the app
    /// at start (not by the tests' editors: their data folder is the user's).
    /// </summary>
    public static void DeleteOldCopy()
    {
        foreach (string d in new[] { Dir, OldCache })
        {
            try
            {
                if (Directory.Exists(d))
                {
                    Directory.Delete(d, true);
                    Console.WriteLine($"game look: deleted the old copy in {d}");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"game look: could not delete the old copy in {d}: {e.Message}");
            }
        }
    }

    // ---- Finding things.

    /// <summary>
    /// The bundles folder of a Valheim game folder (valheim_Data/StreamingAssets/SoftRef/Bundles, either spelling), or
    /// null.
    /// </summary>
    public static string? BundlesDir(string valheim)
    {
        foreach (string sub in new[] { "valheim_Data", "Valheim_Data" })
        {
            string d = Path.Combine(valheim, sub, "StreamingAssets", "SoftRef", "Bundles");
            if (Directory.Exists(d))
            {
                return d;
            }
        }
        return null;
    }

    /// <summary>
    /// The game folder from a folder chosen near it: the game folder itself, its valheim_Data, or a
    /// folder above it in a Steam library (Steam, steamapps, common). Null when none of these.
    /// </summary>
    public static string? GameFolder(string chosen)
    {
        try
        {
            string full = Path.GetFullPath(chosen.Trim().Trim('"'));
            // Without a trailing separator (kept on a drive's or the file system's root).
            full = Path.TrimEndingDirectorySeparator(full);
            string? parent = Path.GetDirectoryName(full);
            var tries = new List<string> { full };
            if (parent != null && Path.GetFileName(full).Equals("valheim_Data", StringComparison.OrdinalIgnoreCase))
            {
                tries.Add(parent);
            }
            tries.Add(Path.Combine(full, "Valheim"));
            tries.Add(Path.Combine(full, "common", "Valheim"));
            tries.Add(Path.Combine(full, "steamapps", "common", "Valheim"));
            return tries.FirstOrDefault(t => BundlesDir(t) != null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The configured folder if it is valid, else Valheim in any Steam library on this computer.
    /// </summary>
    public static string? FindValheim(string? configured)
    {
        if (configured != null && BundlesDir(configured) != null)
        {
            return configured;
        }
        foreach (string lib in SteamLibraries())
        {
            string v = Path.Combine(lib, "steamapps", "common", "Valheim");
            if (BundlesDir(v) != null)
            {
                return v;
            }
        }
        return null;
    }

    /// <summary>
    /// Steam's system-wide folders on Linux (looked at only with <c>SearchOutsideHome</c>).
    /// </summary>
    private static readonly string[] SystemSteamRoots = ["/opt/Steam", "/usr/share/steam"];

    /// <summary>
    /// Steam's folders on this computer (registry and default places on Windows, the home's and Flatpak's on Linux),
    /// each followed by the other libraries its libraryfolders.vdf lists; each folder once.
    /// </summary>
    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string sp)
                {
                    roots.Add(sp.Replace('/', '\\'));
                }
            }
            catch
            {
            }
            if (SearchOutsideHome)
            {
                roots.Add(@"C:\Program Files (x86)\Steam");
                roots.Add(@"C:\Program Files\Steam");
            }
        }
        else
        {
            roots.AddRange(
            [
                Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".steam", "root"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            ]);
            if (SearchOutsideHome)
            {
                roots.AddRange(SystemSteamRoots);
            }
        }
        var seen = new HashSet<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root) || !seen.Add(Path.GetFullPath(root)))
            {
                continue;
            }
            yield return root;
            // Other libraries are listed in libraryfolders.vdf: "path"   "D:\\SteamLibrary"
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                string lib = m.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib)))
                {
                    yield return lib;
                }
            }
        }
    }
}
