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
public static class ModCatalogue
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
        Dictionary<string, JsonNode> Models);

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
    public static Model? ModelOf(string prefab)
    {
        if (_data.Models.GetValueOrDefault(prefab) is not JsonObject o)
        {
            return null;
        }
        static float[] Floats(JsonNode? n) => [.. n!.AsArray().Select(x => (float)x!.GetValue<double>())];
        var parts = o["p"]!.AsArray().Select(p => new ModelPart(
            Plain(p!["mesh"]!.GetValue<string>()), p["v"]!.GetValue<int>(), p["sub"]!.GetValue<int>(),
            p["mat"] is { } m ? Plain(m.GetValue<string>()) : null, Floats(p["m"]), p["b"] is { } b ? Floats(b) : null)).ToList();
        return new Model(Floats(o["s"]), parts);
    }

    /// <summary>
    /// A name without what Unity adds to the copies made while the game runs (" (Instance)", " Instance").
    /// </summary>
    internal static string Plain(string name)
    {
        foreach (string end in (string[])[" (Instance)", " Instance"])
        {
            while (name.EndsWith(end, StringComparison.Ordinal))
            {
                name = name[..^end.Length];
            }
        }
        return name;
    }

    /// <summary>
    /// Takes a game's models (POST /models) of the mod prefabs known: each replaces the one there was, and the whole is
    /// kept. Models not in the expected form, and of prefabs that are not known mods' ones, are left out.
    /// </summary>
    /// <returns>How many models were taken.</returns>
    /// <exception cref="JsonException">The answer is not a JSON object.</exception>
    public static int TakeModels(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("the models are not a JSON object");
        var now = _data;
        var models = new Dictionary<string, JsonNode>(now.Models);
        int taken = 0;
        bool changed = false;
        foreach (var (name, e) in Entries(root))
        {
            if (now.Prefabs.ContainsKey(name) && IsModel(e))
            {
                taken++;
                changed |= Put(models, name, e.DeepClone());
            }
        }
        if (changed)
        {
            var next = now with { Models = models };
            _data = next;
            SafeFile.WriteAllText(FilePath, Write(next));
        }
        return taken;
    }

    /// <summary>What is known now, swapped in whole.</summary>
    private static volatile Data _data = Empty();

    /// <summary>How many mod prefabs are known.</summary>
    public static int Count => _data.Prefabs.Count;

    /// <summary>The names of the mod prefabs known.</summary>
    public static IEnumerable<string> Names => _data.Prefabs.Keys;

    /// <summary>The mod that adds a prefab, by prefab name; null for the game's own and when the game did not know it.</summary>
    public static string? ModOf(string prefab) => _data.Mods.GetValueOrDefault(prefab);

    /// <summary>Whether a prefab was reported by a game and is not the game's own.</summary>
    public static bool IsModded(string prefab) => _data.Prefabs.ContainsKey(prefab);

    /// <summary>
    /// The kept catalogue in use (nothing when there is none or it cannot be read).
    /// </summary>
    public static void Load()
    {
        try
        {
            _data = File.Exists(FilePath) ? Read(File.ReadAllText(FilePath)) : Empty();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Console.WriteLine($"mod catalogue: {FilePath} could not be read ({e.Message}); starting without it.");
            _data = Empty();
        }
    }

    /// <summary>
    /// Nothing known any more (the tests).
    /// </summary>
    internal static void Forget() => _data = Empty();

    /// <summary>
    /// Takes a game's report (GET /catalogue): its prefabs that are not the game's own (<paramref name="vanilla"/>)
    /// join those known, replacing their older entries, and the whole is kept. Entries not in the editor's format
    /// are left out.
    /// </summary>
    /// <param name="report">The bridge's JSON.</param>
    /// <param name="vanilla">The game's own prefab names (GameData's prefabs).</param>
    /// <returns>How many mod prefabs the report has, and whether what is known changed.</returns>
    /// <exception cref="JsonException">The report is not JSON, or not an object.</exception>
    public static (int Mods, bool Changed) Take(string report, IReadOnlySet<string> vanilla)
    {
        var got = Read(report);
        var now = _data;
        var next = new Data(new(now.Prefabs), new(now.Pieces), new(now.Costs), new(now.Items), new(now.Mods), new(now.Models));
        int mods = 0;
        bool changed = false;
        foreach (var (name, entry) in got.Prefabs)
        {
            if (vanilla.Contains(name))
            {
                continue;
            }
            mods++;
            changed |= Put(next.Prefabs, name, entry);
            changed |= got.Pieces.TryGetValue(name, out var piece) ? Put(next.Pieces, name, piece) : next.Pieces.Remove(name);
            changed |= got.Costs.TryGetValue(name, out var cost) ? Put(next.Costs, name, cost) : next.Costs.Remove(name);
            if (got.Mods.TryGetValue(name, out string? mod) && next.Mods.GetValueOrDefault(name) != mod)
            {
                next.Mods[name] = mod;
                changed = true;
            }
        }
        foreach (var (item, name) in got.Items)
        {
            if (next.Costs.Values.Any(c => Uses(c, item)) && next.Items.GetValueOrDefault(item) != name)
            {
                next.Items[item] = name;
                changed = true;
            }
        }
        if (changed)
        {
            // Kept first: should writing fail, nothing is in use that a retry would take for already known.
            SafeFile.WriteAllText(FilePath, Write(next));
            _data = next;
        }
        return (mods, changed);
    }

    /// <summary>
    /// A kind's game data (JSON) with the mods' entries laid over it: those the game's data does not have. A kind
    /// the mods add nothing to is returned as it is.
    /// </summary>
    /// <param name="kind">The GameData kind (prefabs, pieces, piece-cost).</param>
    /// <param name="json">The game's own data of that kind.</param>
    public static string Merge(string kind, string json)
    {
        var d = _data;
        if (!Kinds.ContainsKey(kind) || d.Prefabs.Count == 0)
        {
            return json;
        }
        var root = JsonNode.Parse(json)!.AsObject();
        if (kind == "piece-cost")
        {
            Over(root["pieces"]!.AsObject(), d.Costs);
            Over(root["items"]!.AsObject(), d.Items.ToDictionary(kv => kv.Key, kv => (JsonNode)JsonValue.Create(kv.Value)));
        }
        else
        {
            Over(root, kind == "prefabs" ? d.Prefabs : d.Pieces);
        }
        return root.ToJsonString();
    }

    /// <summary>Adds the entries an object does not have yet (copies: a node has one parent).</summary>
    private static void Over(JsonObject o, Dictionary<string, JsonNode> entries)
    {
        foreach (var (name, entry) in entries)
        {
            if (!o.ContainsKey(name))
            {
                o[name] = entry.DeepClone();
            }
        }
    }

    /// <summary>Sets an entry; whether it differs from the one there was.</summary>
    private static bool Put(Dictionary<string, JsonNode> to, string name, JsonNode entry)
    {
        if (to.TryGetValue(name, out var old) && JsonNode.DeepEquals(old, entry))
        {
            return false;
        }
        to[name] = entry;
        return true;
    }

    /// <summary>Whether a cost entry takes an item.</summary>
    private static bool Uses(JsonNode cost, string item) => cost["r"]!.AsArray().Any(r => r![0]!.GetValue<string>() == item);

    /// <summary>
    /// A report or the kept file: its entries in the editor's formats (the others left out, as a broken entry
    /// would stop the game data from being used at all).
    /// </summary>
    /// <exception cref="JsonException">Not JSON, or not an object.</exception>
    private static Data Read(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("the mod catalogue is not a JSON object");
        var d = Empty();
        foreach (var (name, e) in Entries(root["prefabs"]))
        {
            if (IsPrefab(e))
            {
                d.Prefabs[name] = e.DeepClone();
            }
        }
        foreach (var (name, e) in Entries(root["pieces"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsPiece(e))
            {
                d.Pieces[name] = e.DeepClone();
            }
        }
        foreach (var (name, e) in Entries(root["cost"]?["pieces"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsCost(e))
            {
                d.Costs[name] = e.DeepClone();
            }
        }
        foreach (var (item, e) in Entries(root["cost"]?["items"]))
        {
            if (Text(e) is string n)
            {
                d.Items[item] = n;
            }
        }
        foreach (var (name, e) in Entries(root["mods"]))
        {
            if (Text(e) is string mod && d.Prefabs.ContainsKey(name))
            {
                d.Mods[name] = mod;
            }
        }
        foreach (var (name, e) in Entries(root["models"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsModel(e))
            {
                d.Models[name] = e.DeepClone();
            }
        }
        return d;
    }

    /// <summary>The kept file's JSON.</summary>
    private static string Write(Data d)
    {
        static JsonObject Obj(IEnumerable<KeyValuePair<string, JsonNode>> e) => new(e.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.DeepClone())));
        static IEnumerable<KeyValuePair<string, JsonNode>> Texts(Dictionary<string, string> t) => t.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)JsonValue.Create(kv.Value)));
        var root = new JsonObject
        {
            ["version"] = 1,
            ["prefabs"] = Obj(d.Prefabs),
            ["pieces"] = Obj(d.Pieces),
            ["cost"] = new JsonObject { ["pieces"] = Obj(d.Costs), ["items"] = Obj(Texts(d.Items)) },
            ["mods"] = Obj(Texts(d.Mods)),
            ["models"] = Obj(d.Models),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>An object's entries that are objects or strings; none when it is not an object.</summary>
    private static IEnumerable<(string Name, JsonNode Entry)> Entries(JsonNode? o) =>
        o is JsonObject obj ? obj.Where(kv => kv.Value != null).Select(kv => (kv.Key, kv.Value!)) : [];

    /// <summary>A string value, or null.</summary>
    private static string? Text(JsonNode e) => e is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    /// <summary>Whether a node is a number (an integer when asked).</summary>
    private static bool Num(JsonNode? e, bool integer = false) =>
        e is JsonValue v && v.GetValueKind() == JsonValueKind.Number && (!integer || v.TryGetValue(out int _));

    /// <summary>
    /// A prefab entry: an object with integer "p", "d" and "t", and any of the other keys the editor reads of the
    /// right kind (numbers; "grows", "isi", "isu", "cs", "sa" lists of strings; "is", "lv" lists of integers; "as"
    /// lists of lists of integers).
    /// </summary>
    private static bool IsPrefab(JsonNode e)
    {
        if (e is not JsonObject o || !Num(o["p"], true) || !Num(o["d"], true) || !Num(o["t"], true))
        {
            return false;
        }
        foreach (var (key, v) in o)
        {
            bool ok = key switch
            {
                "grows" or "isi" or "isu" or "cs" or "sa" => v is JsonArray a && a.All(x => x is not null && Text(x) != null),
                "is" or "lv" => v is JsonArray a && a.All(x => Num(x, true)),
                "as" => v is JsonArray a && a.All(x => x is JsonArray b && b.All(y => Num(y, true))),
                "gr" or "ward" or "build" or "dur" or "durl" or "rt" or "sr" => Num(v),
                _ => Num(v, true),
            };
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// A model: "s" three numbers, "p" a list of parts with a string "mesh", integers "v" and "sub", a string "mat" and
    /// "b" six numbers when there, and "m" 16 numbers.
    /// </summary>
    private static bool IsModel(JsonNode e) =>
        e is JsonObject o && o["s"] is JsonArray s && s.Count == 3 && s.All(x => Num(x))
        && o["p"] is JsonArray parts && parts.All(p => p is JsonObject part
            && part["mesh"] is { } mesh && Text(mesh) != null && Num(part["v"], true) && Num(part["sub"], true)
            && (part["mat"] is null || Text(part["mat"]!) != null)
            && (part["b"] is null || part["b"] is JsonArray b && b.Count == 6 && b.All(x => Num(x)))
            && part["m"] is JsonArray m && m.Count == 16 && m.All(x => Num(x)));

    /// <summary>
    /// A piece entry: an integer "cat"; "box" six numbers; "snap" a list of points (three numbers); "tool" a string.
    /// </summary>
    private static bool IsPiece(JsonNode e) =>
        e is JsonObject o && Num(o["cat"], true)
        && (o["box"] is null || o["box"] is JsonArray box && box.Count == 6 && box.All(x => Num(x)))
        && (o["snap"] is null || o["snap"] is JsonArray snaps && snaps.All(s => s is JsonArray p && p.Count == 3 && p.All(x => Num(x))))
        && (o["tool"] is null || Text(o["tool"]!) != null)
        && (o["comfort"] is null || Num(o["comfort"], true));

    /// <summary>
    /// A cost entry: a string "n", "r" a list of [item, amount, ...], "st" a string when there.
    /// </summary>
    private static bool IsCost(JsonNode e) =>
        e is JsonObject o && o["n"] is { } n && Text(n) != null
        && o["r"] is JsonArray r && r.All(x => x is JsonArray a && a.Count >= 2 && a[0] is { } item && Text(item) != null && Num(a[1], true))
        && (o["st"] is null || Text(o["st"]!) != null);
}
