using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// The game's location rules (SeedLocations.RuleSet) read from its files (the editor has no copy of its
/// own: Valheim is needed anyway, and a copy would go out of date): the main scene's ZoneSystem
/// and LocationLists, and the alt biomes (in the main scene's bundle and the bundles it needs, which the
/// SoftRef manifest lists), kept in the data folder until the game changes.
///
/// The order is the game's (ZoneSystem.SetupLocations): ZoneSystem's own m_locations, then the
/// LocationLists by m_sortOrder (they share one: the game keeps the order they wake in, which its log
/// shows as "Added N locations, M vegetations ... from main": read from the player's log, so it
/// follows game updates), then the alt biomes' m_addLocations.
/// A prefab's name is the file name the manifest lists for its asset ID (what SoftReference.Name gives;
/// m_prefabName is not always saved, and sometimes out of date).
/// </summary>
public static partial class GameLocations
{
    /// <summary>
    /// The lists' wake order, by (locations, vegetations), as the game's log showed it for Valheim 0.221:
    /// used when no log of the game is found (ListOrder reads it from the player's own log first).
    /// </summary>
    private static readonly (int Locations, int Vegetation)[] BuiltInOrder = [(3, 0), (2, 0), (27, 25), (4, 0), (25, 33), (25, 35)];

    /// <summary>
    /// The lists' order from the game's own log (Player.log, or Player-prev.log, beside its saves, or in
    /// ~/Library/Logs/IronGate/Valheim on a Mac: the newest that has it), else the built-in one; and where it came
    /// from (for the log).
    /// </summary>
    internal static ((int Locations, int Vegetation)[] Order, string From) ListOrder()
    {
        foreach (string log in Logs())
        {
            try
            {
                // Shared: the game may be running and writing it.
                using var reader = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                if (OrderFromLog(ReadLines(reader)) is { } order)
                {
                    return (order, $"{log} ({File.GetLastWriteTime(log):yyyy-MM-dd})");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Next one.
                Log.CaughtBrief(e, "reading a game log for the location lists' order (the next one is tried)");
            }
        }
        return (BuiltInOrder, "the editor's table (Valheim 0.221; no log of the game found)");

        static IEnumerable<string> ReadLines(StreamReader r)
        {
            while (r.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// The game's logs that exist (Player.log and Player-prev.log, beside its saves, or in ~/Library/Logs/IronGate/Valheim
    /// on a Mac), newest first.
    /// </summary>
    private static List<string> Logs()
    {
        var folders = Places.WorldRoots().Select(Path.GetDirectoryName).OfType<string>().ToList();
        if (OperatingSystem.IsMacOS())
        {
            // Unity writes the log apart from the saves on a Mac.
            folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "IronGate", "Valheim"));
        }
        return [.. folders.Distinct()
            .SelectMany(d => new[] { Path.Combine(d, "Player.log"), Path.Combine(d, "Player-prev.log") })
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)];
    }

    /// <summary>The game's logs, their sizes and when each was last written: changes when the game ran since.</summary>
    private static string LogsStamp() => string.Join("|", Logs().Select(l => $"{l}:{new FileInfo(l).Length}:{File.GetLastWriteTimeUtc(l).Ticks}"));

    /// <summary>
    /// The rules' cache file in the user's data folder.
    /// </summary>
    public static string CacheFile => Path.Combine(AppSettings.UserDataDir, "game-locations.json");

    /// <summary>
    /// Serialises starting the reading.
    /// </summary>
    private static readonly Lock Lock = new();
    /// <summary>
    /// The bundles whose rules are in use (or being read), with the task reading them (true when the game's rules were
    /// read) and the game's logs as they were then (<see cref="LogsStamp"/>).
    /// </summary>
    private static (GameBundles Bundles, Task<bool> Task, string Logs)? _using;

    /// <summary>
    /// The game's rules in use (SeedLocations.Use) when they can be read, else none: read once for a
    /// game folder, again when Valheim is found later or another folder is chosen. Rules that could not be read are
    /// read again once the game has run since (its log tells the lists' order, and a game update needs it new).
    /// </summary>
    public static Task UseGameRules(GameBundles? bundles)
    {
        lock (Lock)
        {
            if (bundles == null)
            {
                return Task.CompletedTask;
            }
            string logs = LogsStamp();
            if (_using is { } u && ReferenceEquals(u.Bundles, bundles) && !(u.Task.IsCompletedSuccessfully && !u.Task.Result && u.Logs != logs))
            {
                return u.Task;
            }
            var task = Task.Run(() =>
            {
                var rules = Load(bundles, CacheFile);
                SeedLocations.Use(rules ?? new SeedLocations.RuleSet());
                return rules != null;
            });
            _using = (bundles, task, logs);
            return task;
        }
    }

    /// <summary>
    /// What the cache was written by: rules read by another editor build are read again.
    /// </summary>
    private static readonly string CacheVersion = "rules-2 " + typeof(GameLocations).Assembly.ManifestModule.ModuleVersionId;
}
