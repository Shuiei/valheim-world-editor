using System.Text.Json;
using AssetsTools.NET;
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
public static class GameLocations
{
    /// <summary>
    /// The lists' wake order, by (locations, vegetations), as the game's log showed it for Valheim 0.221:
    /// used when no log of the game is found (ListOrder reads it from the player's own log first).
    /// </summary>
    private static readonly (int Locations, int Vegetation)[] BuiltInOrder = [(3, 0), (2, 0), (27, 25), (4, 0), (25, 33), (25, 35)];

    /// <summary>
    /// The lists' order from the game's own log (Player.log, or Player-prev.log, beside its saves: the
    /// newest that has it), else the built-in one; and where it came from (for the log).
    /// </summary>
    internal static ((int Locations, int Vegetation)[] Order, string From) ListOrder()
    {
        var logs = Places.WorldRoots().Select(Path.GetDirectoryName).Where(d => d != null).Distinct()
            .SelectMany(d => new[] { Path.Combine(d!, "Player.log"), Path.Combine(d!, "Player-prev.log") })
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);
        foreach (string log in logs)
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
    /// A log line of a list being added: its location and vegetation counts.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex Added = new(@"Added (\d+) locations, (\d+) vegetations.* from main\s*$");

    /// <summary>
    /// The game writes a line for each list as it adds them ("Added 27 locations, 25 vegetations, ...
    /// from main"), at each start: the last run's lines in their order (lists with no location left out:
    /// they add none), or null when the log has none.
    /// </summary>
    internal static (int Locations, int Vegetation)[]? OrderFromLog(IEnumerable<string> lines)
    {
        List<(int, int)>? last = null, run = null;
        foreach (string line in lines)
        {
            var m = Added.Match(line);
            if (!m.Success)
            {
                // A run's lines come one after another: anything else between them ends it.
                if (run != null)
                {
                    last = run;
                    run = null;
                }
                continue;
            }
            run ??= [];
            var list = (int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            // A list met again: the lines of another run, right after.
            if (run.Contains(list))
            {
                last = run;
                run = [];
            }
            if (list.Item1 > 0)
            {
                run.Add(list);
            }
        }
        last = run ?? last;
        return last is { Count: > 0 } ? last.ToArray() : null;
    }

    /// <summary>
    /// The rules' cache file in the user's data folder.
    /// </summary>
    public static string CacheFile => Path.Combine(AppSettings.UserDataDir, "game-locations.json");

    /// <summary>
    /// Serialises starting the reading.
    /// </summary>
    private static readonly Lock Lock = new();
    /// <summary>
    /// The bundles whose rules are in use (or being read), with the task reading them.
    /// </summary>
    private static (GameBundles Bundles, Task Task)? _using;

    /// <summary>
    /// The game's rules in use (SeedLocations.Use) when they can be read, else none: read once for a
    /// game folder, again when Valheim is found later or another folder is chosen.
    /// </summary>
    public static Task UseGameRules(GameBundles? bundles)
    {
        lock (Lock)
        {
            if (bundles == null)
            {
                return Task.CompletedTask;
            }
            if (_using is { } u && ReferenceEquals(u.Bundles, bundles))
            {
                return u.Task;
            }
            var task = Task.Run(() => SeedLocations.Use(Load(bundles, CacheFile) ?? new SeedLocations.RuleSet()));
            _using = (bundles, task);
            return task;
        }
    }

    /// <summary>
    /// What the cache was written by: rules read by another editor build are read again.
    /// </summary>
    private static readonly string CacheVersion = "rules-1 " + typeof(GameLocations).Assembly.ManifestModule.ModuleVersionId;

    /// <summary>
    /// The rules from the game (from the cache when the game and the editor are unchanged), or null when
    /// they cannot be read (whatever goes wrong: the page then shows no bosses or traders).
    /// </summary>
    /// <param name="bundles">The game's bundles.</param>
    /// <param name="cacheFile">Where the rules are kept; null: not kept nor read from a file.</param>
    public static SeedLocations.RuleSet? Load(GameBundles bundles, string? cacheFile)
    {
        try
        {
            var manifest = Manifest.Read(bundles.Folder);
            var scanned = MainSceneBundles(bundles, manifest);
            if (scanned.Count == 0)
            {
                return null;
            }
            var (order, from) = ListOrder();
            string stamp = CacheVersion + " " + string.Join(",", order.Select(o => $"{o.Locations}/{o.Vegetation}")) + " " + JsonSerializer.Serialize(scanned.Select(b => new FileInfo(bundles.PathOf(b)!)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
            if (cacheFile != null && Cached(cacheFile, stamp) is { } cached)
            {
                Console.WriteLine($"Location rules: {cached.locations.Count} kinds, from the cache.");
                return cached;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var rules = Read(bundles, scanned, manifest, order);
            if (rules != null)
            {
                Console.WriteLine($"Location rules: {rules.locations.Count} kinds and {rules.altBiomes.Count} alt biomes read from the game ({scanned.Count} bundles) in {watch.ElapsedMilliseconds} ms; the lists' order from {from}.");
            }
            if (rules != null && cacheFile != null)
            {
                // Written whole or not at all (a crash half way leaves the old file, or none).
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
                string part = cacheFile + ".part";
                File.WriteAllText(part, stamp + "\n" + rules.ToJson());
                File.Move(part, cacheFile, overwrite: true);
            }
            return rules;
        }
        catch (Exception e)
        {
            // Anything in the game's files can fail the reading: the page goes on without bosses or traders.
            Console.WriteLine($"The game's location rules could not be read ({e.GetType().Name}: {e.Message}): no bosses or traders on the New world page.");
            return null;
        }
    }

    /// <summary>
    /// The main scene's bundle and the bundles it needs, those the folder has (the SoftRef manifest lists
    /// them): where ZoneSystem and the LocationLists are (also the vegetation rules, GameCatalogues).
    /// </summary>
    internal static List<string> MainSceneBundles(GameBundles bundles) => MainSceneBundles(bundles, Manifest.Read(bundles.Folder));

    /// <summary>
    /// The main scene's bundles of the manifest that the folder has.
    /// </summary>
    private static List<string> MainSceneBundles(GameBundles bundles, Manifest manifest) => [.. manifest.SceneBundles("main").Where(bundles.HasBundle)];

    /// <summary>
    /// The cached rules when they are for this game and editor and can be read; else null (read again).
    /// </summary>
    private static SeedLocations.RuleSet? Cached(string file, string stamp)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            string[] cached = File.ReadAllText(file).Split('\n', 2);
            return cached.Length == 2 && cached[0] == stamp ? SeedLocations.RuleSet.FromJson(cached[1]) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds ZoneSystem's locations, the LocationLists named by the order and the alt biomes in these bundles and makes
    /// the rule set; null (and a log line) when one is missing.
    /// </summary>
    private static SeedLocations.RuleSet? Read(GameBundles bundles, List<string> scanned, Manifest manifest, (int Locations, int Vegetation)[] order)
    {
        AssetTypeValueField? zone = null, alts = null;
        var lists = new Dictionary<(int, int), AssetTypeValueField>();
        foreach (string bundle in scanned)
        {
            foreach (var f in bundles.MonoBehaviours(bundle))
            {
                var locations = f["m_locations"];
                if (!locations.IsDummy && !f["m_zoneSize"].IsDummy)
                {
                    // Some bundles hold another ZoneSystem too (fewer locations): the main one has most.
                    if (zone == null || Count(locations) > Count(zone))
                    {
                        zone = locations;
                    }
                }
                else if (!locations.IsDummy && !f["m_sortOrder"].IsDummy)
                {
                    var key = (Count(locations), Count(f["m_vegetation"]));
                    if (order.Contains(key))
                    {
                        lists.TryAdd(key, locations);
                    }
                }
                else if (!f["m_alts"].IsDummy && alts == null)
                {
                    alts = f["m_alts"];
                }
            }
        }
        if (zone == null || alts == null || order.Any(k => !lists.ContainsKey(k)))
        {
            Console.WriteLine($"The game's location rules were not all found (ZoneSystem: {zone != null}, alt biomes: {alts != null}, lists: {lists.Count}/{order.Length}); a game update changed them (start the game once to refresh its log)? No bosses or traders on the New world page.");
            return null;
        }
        var rules = new SeedLocations.RuleSet();
        rules.locations.AddRange(Items(zone).Select(l => Location(l, manifest, null)));
        foreach (var key in order)
        {
            rules.locations.AddRange(Items(lists[key]).Select(l => Location(l, manifest, null)));
        }
        foreach (var a in Items(alts))
        {
            string name = a["m_name"].AsString;
            rules.altBiomes.Add(new SeedLocations.AltBiomeRule { name = name, blockLocationNames = [.. Items(a["m_blockLocationNames"]).Select(s => s.AsString)] });
            rules.locations.AddRange(Items(a["m_addLocations"]).Select(l => Location(l, manifest, name)));
        }
        return rules;
    }

    /// <summary>
    /// A list field's items (its Array's, when it has one).
    /// </summary>
    private static List<AssetTypeValueField> Items(AssetTypeValueField list) => list["Array"].IsDummy ? list.Children : list["Array"].Children;

    /// <summary>
    /// How many items a list field has (0 when it is missing).
    /// </summary>
    private static int Count(AssetTypeValueField list) => list.IsDummy ? 0 : Items(list).Count;

    /// <summary>
    /// One ZoneLocation: each rule property from the field of the same name (m_ before it). <c>altBiome</c>: the alt
    /// biome adding it, null for the main lists. The prefab is named by the manifest from its asset ID, else by
    /// m_prefabName.
    /// </summary>
    private static SeedLocations.Rule Location(AssetTypeValueField l, Manifest manifest, string? altBiome)
    {
        var rule = new SeedLocations.Rule { altBiome = altBiome };
        foreach (var prop in typeof(SeedLocations.Rule).GetProperties())
        {
            var f = l["m_" + prop.Name];
            if (f.IsDummy || f.Value == null)
            {
                continue;
            }
            if (prop.PropertyType == typeof(int))
            {
                prop.SetValue(rule, f.Value.ValueType == AssetValueType.Bool ? (f.AsBool ? 1 : 0) : f.AsInt);
            }
            else if (prop.PropertyType == typeof(float))
            {
                prop.SetValue(rule, f.AsFloat);
            }
            else if (prop.PropertyType == typeof(string) && prop.Name != nameof(SeedLocations.Rule.altBiome))
            {
                prop.SetValue(rule, f.AsString);
            }
        }
        var id = l["m_prefab"]["m_assetID"];
        string hex = $"{id["v3"].AsUInt:x8}{id["v2"].AsUInt:x8}{id["v1"].AsUInt:x8}{id["v0"].AsUInt:x8}";
        rule.prefab = manifest.Names.GetValueOrDefault(hex) ?? l["m_prefabName"].AsString;
        return rule;
    }

    /// <summary>
    /// The tests' copy (tests/fixtures/locations.json: the kinds laid out first) made again from the game:
    /// --export-locations &lt;file&gt;. Returns the exit code: 0 done, 1 no game or the rules not read, 2 no file
    /// named.
    /// </summary>
    public static int Export(string? path)
    {
        if (path == null)
        {
            Console.Error.WriteLine("--export-locations needs the file to write.");
            return 2;
        }
        var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
        var rules = bundles == null ? null : Load(bundles, null);
        if (rules == null)
        {
            Console.Error.WriteLine(bundles == null ? "Valheim was not found." : "The location rules could not be read (see the log).");
            return 1;
        }
        var kept = SeedLocations.Prioritized(rules);
        File.WriteAllText(path, kept.ToJson());
        Console.WriteLine($"{kept.locations.Count} of {rules.locations.Count} locations written to {path}");
        return 0;
    }

    /// <summary>
    /// The SoftRef manifest beside the bundles folder: each asset's file name by its ID, and each
    /// bundle's dependencies.
    /// </summary>
    private sealed class Manifest
    {
        /// <summary>
        /// Each asset's file name (without extension) by its asset ID.
        /// </summary>
        public Dictionary<string, string> Names { get; } = [];
        /// <summary>
        /// Each bundle's dependencies.
        /// </summary>
        private readonly Dictionary<string, List<string>> _needs = [];
        /// <summary>
        /// Each scene's bundle, by scene name.
        /// </summary>
        private readonly Dictionary<string, string> _sceneBundle = [];

        /// <summary>
        /// Reads the manifest beside a bundles folder (its parent's "manifest" file).
        /// </summary>
        public static Manifest Read(string bundlesDir)
        {
            var m = new Manifest();
            string? id = null, bundle = null, depsOf = null;
            foreach (string raw in File.ReadLines(Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(bundlesDir))!, "manifest")))
            {
                string line = raw.Trim();
                if (raw.StartsWith("- bundle: ", StringComparison.Ordinal))
                {
                    depsOf = line[10..];
                    m._needs[depsOf] = [];
                }
                else if (depsOf != null && raw.StartsWith("  - ", StringComparison.Ordinal))
                {
                    m._needs[depsOf].Add(line[2..]);
                }
                else if (line.StartsWith("- asset ID: ", StringComparison.Ordinal))
                {
                    depsOf = null;
                    id = line[12..];
                    bundle = null;
                }
                else if (line.StartsWith("bundle: ", StringComparison.Ordinal))
                {
                    bundle = line[8..];
                }
                else if (line.StartsWith("path in bundle: ", StringComparison.Ordinal) && id != null)
                {
                    string file = line[16..];
                    m.Names[id] = Path.GetFileNameWithoutExtension(file);
                    if (file.EndsWith(".unity", StringComparison.Ordinal) && bundle != null)
                    {
                        m._sceneBundle[Path.GetFileNameWithoutExtension(file)] = bundle;
                    }
                }
            }
            return m;
        }

        /// <summary>
        /// A scene's bundle, then the bundles it needs.
        /// </summary>
        public List<string> SceneBundles(string scene) => _sceneBundle.TryGetValue(scene, out string? b)
            ? [b, .. _needs.GetValueOrDefault(b) ?? []]
            : [];
    }
}
