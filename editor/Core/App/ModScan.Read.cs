using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading the installed mods' pieces: each mod bundle on its own (which one a piece comes from names its mod), the
/// mods' English texts beside their DLLs, and the add-ons' manifests; kept until the mods change.
/// </summary>
public static partial class ModScan
{
    /// <summary>The kept reading of the mods' bundles in the data folder.</summary>
    public static string FilePath => Path.Combine(AppSettings.DataDir, "mod-scan.json");

    /// <summary>The kept file's format; another is read again.</summary>
    private const int Version = 1;

    /// <summary>A mod's English texts file larger than this is not read.</summary>
    private const long MaxTexts = 4L << 20;

    /// <summary>
    /// The installed mods' pieces: from the kept reading when the mods have not changed since (<paramref name="again"/>
    /// false), else read from their bundles and kept; with the add-ons' manifests' pieces after. The game's own prefabs
    /// are left out.
    /// </summary>
    /// <param name="settings">The editor's settings (the plugins folders it knows).</param>
    /// <param name="gameBundles">The game's bundles folder, for its English texts (the costs' items).</param>
    /// <param name="vanilla">The game's own prefab names.</param>
    /// <param name="again">Read the bundles even when the kept reading is of the same mods.</param>
    /// <param name="progress">Told the bundles read, of all of them, while it reads them.</param>
    public static Data Scan(AppSettings settings, string gameBundles, IReadOnlySet<string> vanilla, bool again = false, Action<int, int>? progress = null)
    {
        var sources = ModBundles.Find(settings);
        string stamp = JsonSerializer.Serialize(sources);
        var data = again ? null : Kept(stamp);
        if (data == null)
        {
            data = Data.Empty;
            if (sources.Count > 0 && ModBundles.Reader(settings) is { } reader)
            {
                var english = new Dictionary<string, string>(GameCatalogues.Localization.English(gameBundles));
                var byName = sources.ToDictionary(s => s.Name, StringComparer.Ordinal);
                foreach (string dir in sources.Select(s => ModFolder(s.Path)).Distinct(StringComparer.Ordinal))
                {
                    foreach (var (token, text) in Texts(dir))
                    {
                        english.TryAdd(token, text);
                    }
                }
                foreach (var (bundle, kinds) in GameCatalogues.ReadEach(reader.Folder, Kinds, english, progress))
                {
                    if (byName.TryGetValue(bundle, out var source))
                    {
                        Take(data, kinds, ModName(source.Path));
                    }
                }
            }
            Keep(stamp, data);
        }
        data = Without(data, vanilla);
        AddManifestPieces(data, vanilla);
        return data;
    }

    /// <summary>
    /// The kept reading when it is of these mods (the same stamp) and of this format; null otherwise, or when it cannot
    /// be read (said in the log).
    /// </summary>
    private static Data? Kept(string stamp)
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(FilePath))?.AsObject();
            if (root?["version"]?.GetValue<int>() != Version || root["stamp"]?.GetValue<string>() != stamp)
            {
                return null;
            }
            var data = Data.Empty;
            foreach (string kind in Kinds)
            {
                foreach (var (name, e) in root["kinds"]?[kind]?.AsObject() ?? [])
                {
                    if (e != null)
                    {
                        data.Entries[kind][name] = e.DeepClone();
                    }
                }
            }
            foreach (var (item, e) in root["items"]?.AsObject() ?? [])
            {
                if (e?.GetValue<string>() is string n)
                {
                    data.Items[item] = n;
                }
            }
            foreach (var (name, e) in root["mods"]?.AsObject() ?? [])
            {
                if (e?.GetValue<string>() is string mod)
                {
                    data.Mods[name] = mod;
                }
            }
            return data;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            Log.Caught(e, "reading the kept reading of the mods' pieces (read from the mods again)");
            return null;
        }
    }

    /// <summary>Keeps a reading of the mods' bundles, with the stamp of the mods it is of (a failure is said in the log).</summary>
    private static void Keep(string stamp, Data data)
    {
        var kinds = new JsonObject();
        foreach (var (kind, entries) in data.Entries)
        {
            kinds[kind] = new JsonObject(entries.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.DeepClone())));
        }
        var root = new JsonObject
        {
            ["version"] = Version,
            ["stamp"] = stamp,
            ["kinds"] = kinds,
            ["items"] = new JsonObject(data.Items.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
            ["mods"] = new JsonObject(data.Mods.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            SafeFile.WriteAllText(FilePath, root.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "keeping the reading of the mods' pieces (read again next time)");
        }
    }

    /// <summary>
    /// Takes one bundle's pieces: those with a prefab entry (a networked prefab: one the game can save) and a piece
    /// entry, each kind's entry, the costs' item names, and the mod. A piece no build tool's menu in the bundle lists
    /// (a mod adds it to the hammer in code) is the hammer's; a cost named by a token with no English text is left
    /// unnamed (the prefab name shows).
    /// </summary>
    /// <param name="data">Where the pieces go.</param>
    /// <param name="kinds">The bundle's reading, each kind's JSON.</param>
    /// <param name="mod">The mod the bundle is of.</param>
    internal static void Take(Data data, IReadOnlyDictionary<string, string> kinds, string mod)
    {
        JsonObject Of(string kind) => kinds.TryGetValue(kind, out string? json) ? JsonNode.Parse(json)?.AsObject() ?? [] : [];
        var prefabs = Of("prefabs");
        var pieces = Of("pieces");
        var place = Of("piece-place");
        var support = Of("piece-support");
        var cost = Of("piece-cost");
        var costPieces = cost["pieces"]?.AsObject() ?? [];
        var items = cost["items"]?.AsObject() ?? [];
        foreach (var (name, piece) in pieces)
        {
            bool loose = piece?["loose"] != null;
            var prefab = prefabs[name];
            if (piece is not JsonObject p || prefab == null && !loose)
            {
                continue;
            }
            var entry = p.DeepClone().AsObject();
            entry.Remove("loose");
            entry["tool"] ??= "hammer";
            // A piece the mod gives its Piece and ZNetView in code: saved as any piece, placed by its snap points and box.
            data.Entries["prefabs"][name] = prefab?.DeepClone() ?? new JsonObject { ["p"] = 1, ["d"] = 0, ["t"] = 2 };
            data.Entries["pieces"][name] = entry;
            if (place[name] is { } pl)
            {
                data.Entries["piece-place"][name] = pl.DeepClone();
            }
            else if (loose && PlaceOf(entry) is { } made)
            {
                data.Entries["piece-place"][name] = made;
            }
            if (support[name] is { } su)
            {
                data.Entries["piece-support"][name] = su.DeepClone();
            }
            if (costPieces[name] is JsonObject c)
            {
                var named = c.DeepClone().AsObject();
                if (named["n"]?.GetValue<string>() is string n && n.StartsWith('$'))
                {
                    named.Remove("n");
                }
                data.Entries["piece-cost"][name] = named;
                foreach (var r in named["r"]?.AsArray() ?? [])
                {
                    if (r?[0]?.GetValue<string>() is string item && items[item]?.GetValue<string>() is string itemName)
                    {
                        data.Items[item] = itemName;
                    }
                }
            }
            data.Mods[name] = mod;
        }
    }

    /// <summary>
    /// How a piece read without a Piece is placed (piece-place's format): its snap points, and one box collider, its box,
    /// else the box round its snap points (10 cm thick at least); null without snap points.
    /// </summary>
    /// <param name="piece">The piece's entry ("snap", "box": min and max x, then z, then y).</param>
    internal static JsonObject? PlaceOf(JsonObject piece)
    {
        if (piece["snap"] is not JsonArray { Count: > 0 } snap)
        {
            return null;
        }
        double[] b;
        if (piece["box"] is JsonArray { Count: 6 } box)
        {
            b = [.. box.Select(v => v!.GetValue<double>())];
        }
        else
        {
            var pts = snap.Select(s => s!.AsArray().Select(v => v!.GetValue<double>()).ToArray()).ToList();
            b = [pts.Min(q => q[0]), pts.Max(q => q[0]), pts.Min(q => q[2]), pts.Max(q => q[2]), pts.Min(q => q[1]), pts.Max(q => q[1])];
        }
        static double Half(double lo, double hi) => Math.Max(0.05, (hi - lo) / 2);
        var collider = new JsonArray("b", (b[0] + b[1]) / 2, (b[4] + b[5]) / 2, (b[2] + b[3]) / 2, Half(b[0], b[1]), Half(b[4], b[5]), Half(b[2], b[3]), 0, 0, 0, 1);
        return new JsonObject { ["f"] = new JsonObject(), ["s"] = snap.DeepClone(), ["c"] = new JsonArray(collider) };
    }

    /// <summary>A reading without the prefabs that are the game's own (a game update made a mod's name vanilla).</summary>
    private static Data Without(Data data, IReadOnlySet<string> vanilla)
    {
        foreach (string name in data.Mods.Keys.Where(vanilla.Contains).ToList())
        {
            data.Mods.Remove(name);
            foreach (var entries in data.Entries.Values)
            {
                entries.Remove(name);
            }
        }
        return data;
    }

    /// <summary>
    /// The mod a bundle's file is of, for the player: the name in the Thunderstore manifest of its folder under plugins
    /// (underscores as spaces), else that folder's name without its author ("Author-Mod"), else the file's own name.
    /// </summary>
    /// <param name="file">The DLL or bundle file.</param>
    internal static string ModName(string file)
    {
        string folder = ModFolder(file);
        string manifest = Path.Combine(folder, "manifest.json");
        try
        {
            if (File.Exists(manifest) && JsonNode.Parse(File.ReadAllText(manifest))?["name"]?.GetValue<string>() is { Length: > 0 } name)
            {
                return name.Replace('_', ' ');
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Log.CaughtBrief(e, "reading a mod's manifest.json for its name (its folder's name is used)");
        }
        string own = Path.GetFileName(folder);
        if (Path.GetFileName(Path.GetDirectoryName(file)) is "plugins")
        {
            return Path.GetFileNameWithoutExtension(file);
        }
        int dash = own.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 && dash < own.Length - 1 ? own[(dash + 1)..] : own;
    }

    /// <summary>
    /// A mod's own folder: the one right under "plugins" its file is in (a mod manager's Author-Mod folder); the file's
    /// own folder when no folder of the path is named plugins.
    /// </summary>
    /// <param name="file">The DLL or bundle file.</param>
    internal static string ModFolder(string file)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        for (string? d = dir; d != null; d = Path.GetDirectoryName(d))
        {
            if (Path.GetFileName(Path.GetDirectoryName(d)) is string parent && parent.Equals("plugins", StringComparison.OrdinalIgnoreCase))
            {
                return d;
            }
        }
        return dir;
    }

    /// <summary>
    /// A mod's English texts, by token without "$": every JSON file in its folder whose name says English
    /// ("Mod.English.json", "Translations/English/*.json"), its string values (others skipped).
    /// </summary>
    /// <param name="folder">The mod's folder.</param>
    internal static IEnumerable<(string Token, string Text)> Texts(string folder)
    {
        List<string> files;
        try
        {
            files = [.. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Where(f => f.Contains("English", StringComparison.OrdinalIgnoreCase) && new FileInfo(f).Length <= MaxTexts)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(e, "listing a mod's English texts (its pieces keep their prefab names)");
            yield break;
        }
        foreach (string f in files)
        {
            JsonObject? texts = null;
            try
            {
                texts = JsonNode.Parse(File.ReadAllText(f)) as JsonObject;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log.CaughtBrief(e, $"reading a mod's English texts {Path.GetFileName(f)} (skipped)");
            }
            foreach (var (token, v) in texts ?? [])
            {
                if (v is JsonValue value && value.TryGetValue(out string? text))
                {
                    yield return (token.TrimStart('$'), text);
                }
            }
        }
    }
}
