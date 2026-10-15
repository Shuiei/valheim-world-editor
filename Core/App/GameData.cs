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
public static class GameData
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
        new("dungeon-rooms", null, json =>
        {
            Editing.Dungeons.Use(json);
            Editing.LocationPrefabs.Use(json);
        }, TrimDungeons),
    ];

    /// <summary>
    /// The kinds' names, in the order they are read and used.
    /// </summary>
    internal static IEnumerable<string> KindNames => Kinds.Select(k => k.Name);

    /// <summary>
    /// A kind's cache file in the user's data folder: game-&lt;kind&gt;.json.
    /// </summary>
    public static string CacheFile(string kind) => Path.Combine(AppSettings.UserDataDir, $"game-{kind}.json");

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
    private static string? _from;
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
            ModCatalogue.Load();
            foreach (var kind in Kinds)
            {
                Use(kind, read[kind.Name]);
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
    /// A kind's data in use, with the mods' prefabs laid over it when it is one of theirs (ModCatalogue.Kinds). Should
    /// the mods' entries make it fail, the game's own data is used alone.
    /// </summary>
    private static void Use(Kind kind, string json)
    {
        if (!ModCatalogue.Kinds.ContainsKey(kind.Name))
        {
            kind.Use(json);
            return;
        }
        Own[kind.Name] = json;
        string merged = ModCatalogue.Merge(kind.Name, json);
        if (ReferenceEquals(merged, json))
        {
            kind.Use(json);
            return;
        }
        try
        {
            kind.Use(merged);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or InvalidCastException)
        {
            Console.WriteLine($"Game data {kind.Name}: the mods' prefabs could not be used ({e.Message}); the game's own only.");
            kind.Use(json);
        }
    }

    /// <summary>
    /// Takes a running game's prefabs (WorldEditorBridge's GET /catalogue): those that are not the game's own are
    /// kept (ModCatalogue) and, when they change what is known, laid over the data in use. Returns how many mod
    /// prefabs the game has. Before any data is in use, they are only kept.
    /// </summary>
    /// <exception cref="JsonException">The report is not a JSON object.</exception>
    public static int AddMods(string report)
    {
        lock (Lock)
        {
            if (!Own.TryGetValue("prefabs", out string? prefabs))
            {
                return 0;
            }
            using var doc = JsonDocument.Parse(prefabs);
            var vanilla = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var (mods, changed) = ModCatalogue.Take(report, vanilla);
            if (changed)
            {
                foreach (var kind in Kinds.Where(k => Own.ContainsKey(k.Name)))
                {
                    Use(kind, Own[kind.Name]);
                }
                _generation++;
            }
            return mods;
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

    /// <summary>
    /// Valheim's folder from its bundles folder (&lt;data folder&gt;/StreamingAssets/SoftRef/Bundles, the data folder
    /// being valheim_Data, or valheim.app/Contents/Resources/Data on a Mac).
    /// </summary>
    private static string ValheimOf(string bundles)
    {
        string? d = Path.TrimEndingDirectorySeparator(bundles);
        for (int i = 0; i < 3 && d != null; i++)
        {
            d = Path.GetDirectoryName(d);
        }
        return d == null ? bundles : GameLook.GameOfData(d);
    }

    /// <summary>
    /// The game's files a kind is read from, by name, size and time: what makes a cache out of date.
    /// </summary>
    internal static string Stamp(string valheim, string bundles) =>
        CacheVersion + " " + JsonSerializer.Serialize(Directory.GetFiles(bundles).Order(StringComparer.Ordinal)
            .Concat(new[] { GameLook.DataDir(valheim) }.OfType<string>().Select(d => Path.Combine(d, "Managed", "assembly_valheim.dll")).Where(File.Exists))
            .Select(f => new FileInfo(f)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

    /// <summary>
    /// Every kind as JSON, by name: from its cache when the game and the editor are unchanged, else read
    /// from the game (and kept, with cache); null when one cannot be read.
    /// </summary>
    /// <param name="bundles">The game's bundles.</param>
    /// <param name="cache">From and to the caches.</param>
    /// <param name="progress">Told how far the reading is (0..1) and what it reads: the kinds with a reader of their
    /// own share the first part (a fifth when the one pass over the bundles follows, which takes most of the time),
    /// the pass the rest, bundle by bundle.</param>
    internal static Dictionary<string, string>? Load(GameBundles bundles, bool cache, Action<double, string>? progress = null)
    {
        string valheim = ValheimOf(bundles.Folder);
        string what = "files";
        try
        {
            string stamp = Stamp(valheim, bundles.Folder);
            var json = new Dictionary<string, string>();
            var read = new List<string>();
            var own = new List<Kind>();
            var pass = new List<string>();
            foreach (var kind in Kinds)
            {
                if (cache && Cached(CacheFile(kind.Name), kind.Name + " " + stamp) is { } cached)
                {
                    Console.WriteLine($"Game data {kind.Name}: from the cache.");
                    json[kind.Name] = cached;
                }
                else if (kind.Read == null)
                {
                    pass.Add(kind.Name);
                }
                else
                {
                    own.Add(kind);
                }
            }
            // No kind of its own left to read (all from the caches): the pass starts at nothing read.
            double ownShare = own.Count == 0 ? 0 : pass.Count > 0 ? 0.2 : 1;
            for (int i = 0; i < own.Count; i++)
            {
                var kind = own[i];
                what = kind.Name;
                progress?.Invoke(ownShare * i / own.Count, $"Reading the game's {kind.Name}");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                json[kind.Name] = kind.Read!(valheim, bundles);
                Console.WriteLine($"Game data {kind.Name}: read from the game in {watch.ElapsedMilliseconds} ms.");
                read.Add(kind.Name);
            }
            if (pass.Count > 0)
            {
                what = string.Join(", ", pass);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                void Bundle(int done, int count) =>
                    progress?.Invoke(ownShare + ((1 - ownShare) * done / Math.Max(1, count)), $"Reading the game's bundles: {done} of {count}");
                foreach (var (name, j) in GameCatalogues.ReadAtStart(bundles, pass, Bundle))
                {
                    json[name] = j;
                }
                Console.WriteLine($"Game data {what}: read from the game's bundles in {watch.ElapsedMilliseconds} ms.");
                read.AddRange(pass);
            }
            if (cache)
            {
                foreach (string name in read)
                {
                    KeepCache(CacheFile(name), name + " " + stamp + "\n" + json[name]);
                }
            }
            return json;
        }
        catch (Exception e)
        {
            // Anything in the game's files can fail the reading: no world opens then.
            Console.WriteLine($"The game's {what} could not be read ({e.GetType().Name}: {e.Message}).");
            return null;
        }
    }

    /// <summary>
    /// Keeps a cache, written whole or not at all (a crash half way leaves the old file, or none). One that cannot be
    /// written (a full disk, a read-only folder, another editor writing it too) is only logged: what was read is used
    /// all the same, and read again next time.
    /// </summary>
    /// <returns>False when it could not be written.</returns>
    internal static bool KeepCache(string file, string text)
    {
        string part = file + ".part";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(part, text);
            File.Move(part, file, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"{Path.GetFileName(file)} could not be kept ({e.GetType().Name}: {e.Message}); it is read again next time.");
            try
            {
                File.Delete(part);
            }
            catch (Exception d) when (d is IOException or UnauthorizedAccessException)
            {
            }
            return false;
        }
    }

    /// <summary>
    /// A cache when it is for this game and editor; else null (read again).
    /// </summary>
    private static string? Cached(string file, string stamp)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            string[] cached = File.ReadAllText(file).Split('\n', 2);
            return cached.Length == 2 && cached[0] == stamp ? cached[1] : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A copy written one entry per line (an entry's value on its line, compact): small, and a game
    /// update shows in git entry by entry. A few groups of entries (piece-cost's items and pieces) are
    /// opened the same way.
    /// </summary>
    private static string Lines(JsonElement e, string indent = "")
    {
        string inner = indent + "  ";
        if (e.ValueKind == JsonValueKind.Object)
        {
            var props = e.EnumerateObject().ToList();
            bool groups = props.Count is > 0 and <= 4 && props.All(p => p.Value.ValueKind == JsonValueKind.Object);
            var lines = props.Select(p => inner + JsonSerializer.Serialize(p.Name) + ": " + (groups ? Lines(p.Value, inner) : p.Value.GetRawText()));
            return props.Count == 0 ? "{}" : "{\n" + string.Join(",\n", lines) + "\n" + indent + "}";
        }
        if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0)
        {
            return "[\n" + string.Join(",\n", e.EnumerateArray().Select(x => inner + x.GetRawText())) + "\n" + indent + "]";
        }
        return e.GetRawText();
    }

    /// <summary>
    /// The tests' copies (&lt;folder&gt;/&lt;kind&gt;.json) made again from the game, each keeping the entries it
    /// already has: --export-test-data &lt;folder&gt;. Returns the exit code: 0 done, 1 no game, a missing copy or the
    /// game's data not read, 2 no such folder.
    /// </summary>
    public static int Export(string? folder)
    {
        if (folder == null || !Directory.Exists(folder))
        {
            Console.Error.WriteLine("--export-test-data needs the folder of the tests' copies (tests/fixtures).");
            return 2;
        }
        var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
        if (bundles == null)
        {
            Console.Error.WriteLine("Valheim was not found.");
            return 1;
        }
        if (Kinds.Select(k => Path.Combine(folder, k.Name + ".json")).FirstOrDefault(p => !File.Exists(p)) is string missing)
        {
            Console.Error.WriteLine($"{missing} is missing.");
            return 1;
        }
        if (Load(bundles, cache: false) is not { } read)
        {
            Console.Error.WriteLine("The game's data could not be read (see the log).");
            return 1;
        }
        foreach (var kind in Kinds)
        {
            string path = Path.Combine(folder, kind.Name + ".json");
            var (text, kept, now) = Trim(kind.Name, read[kind.Name], File.ReadAllText(path));
            File.WriteAllText(path, text);
            Console.WriteLine($"{path}: {now.Count} of {kept.Count} entries" + (now.Count < kept.Count ? $" (not in the game any more: {string.Join(", ", kept.Except(now))})" : ""));
        }
        return 0;
    }

    /// <summary>
    /// A tests' copy made again: the game's JSON of a kind trimmed to what the copy (its JSON) holds, as the
    /// copy's text, with what the copy held and what it holds now (entries, or groups' entries).
    /// </summary>
    internal static (string Text, List<string> Kept, List<string> Now) Trim(string name, string game, string copy)
    {
        var kind = Kinds.First(k => k.Name == name);
        using var c = JsonDocument.Parse(copy);
        using var g = JsonDocument.Parse(game);
        var trimmed = JsonSerializer.SerializeToElement((kind.Trim ?? Entries)(g.RootElement, c.RootElement));
        return (Lines(trimmed) + "\n", Kept(c.RootElement, kind.Trim == null), Kept(trimmed, kind.Trim == null));
    }

    /// <summary>
    /// The game's entries the copy has.
    /// </summary>
    private static Dictionary<string, object> Entries(JsonElement game, JsonElement copy)
    {
        var kept = copy.EnumerateObject().Select(p => p.Name).ToHashSet();
        return game.EnumerateObject().Where(p => kept.Contains(p.Name)).ToDictionary(p => p.Name, p => (object)p.Value);
    }

    /// <summary>
    /// piece-cost: the pieces the copy has, and the items they take.
    /// </summary>
    private static Dictionary<string, object> TrimPieceCost(JsonElement game, JsonElement copy)
    {
        var pieces = Entries(game.GetProperty("pieces"), copy.GetProperty("pieces"));
        var items = pieces.Values.Cast<JsonElement>().SelectMany(p => p.GetProperty("r").EnumerateArray().Select(r => r[0].GetString())).ToHashSet();
        return new()
        {
            ["items"] = game.GetProperty("items").EnumerateObject().Where(p => items.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value),
            ["pieces"] = pieces,
        };
    }

    /// <summary>
    /// dungeon-rooms: every dungeon kind, and the rooms and the locations the copy has.
    /// </summary>
    private static Dictionary<string, object> TrimDungeons(JsonElement game, JsonElement copy) => new()
    {
        ["dungeons"] = game.GetProperty("dungeons"),
        ["rooms"] = Entries(game.GetProperty("rooms"), copy.GetProperty("rooms")),
        ["locations"] = copy.TryGetProperty("locations", out var locations) ? Entries(game.GetProperty("locations"), locations) : [],
    };

    /// <summary>
    /// vegetation (a list of rules): the game's rules the copy has, by name and prefab.
    /// </summary>
    private static List<JsonElement> TrimVegetation(JsonElement game, JsonElement copy)
    {
        var kept = copy.EnumerateArray().Select(RuleKey).ToHashSet();
        return [.. game.EnumerateArray().Where(r => kept.Contains(RuleKey(r)))];
    }

    /// <summary>
    /// A vegetation rule's key: "name/prefab".
    /// </summary>
    private static string RuleKey(JsonElement rule) => $"{rule.GetProperty("name").GetString()}/{rule.GetProperty("prefab").GetString()}";

    /// <summary>
    /// What a copy holds, to tell what went: its entries (a list's: its rules), else (trimmed its own
    /// way) those of each part.
    /// </summary>
    private static List<string> Kept(JsonElement copy, bool entries) =>
        copy.ValueKind == JsonValueKind.Array ? [.. copy.EnumerateArray().Select(RuleKey)]
        : [.. copy.EnumerateObject().SelectMany(p => entries ? new[] { p.Name } : p.Value.EnumerateObject().Select(e => $"{p.Name}/{e.Name}"))];
}
