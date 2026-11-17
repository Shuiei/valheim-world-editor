using System.Globalization;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using NVector3 = System.Numerics.Vector3;

namespace ExpandWorld.WorldEditor;

// The Expand World mods' data entries (expand_data*.yaml, config/data/*.yaml, and those written among Expand World
// Prefabs' rules) as the Custom object tool gives them to objects, with the objects the rules use each one on.
public static partial class ExpandWorldMod
{
    /// <summary>A value of a data entry the editor can set.</summary>
    /// <param name="List">The entry's list it is in ("ints", "strings"…).</param>
    /// <param name="Key">Its key, as written.</param>
    /// <param name="Text">Its value, as written.</param>
    /// <param name="Set">Sets it on an object.</param>
    public sealed record DataValue(string List, string Key, string Text, Action<ICopy> Set);

    /// <summary>A data entry, as far as the editor can give it to an object.</summary>
    /// <param name="Name">Its name.</param>
    /// <param name="Values">The values set.</param>
    /// <param name="Skipped">What is left out: values the mod works out when it makes the object, and fields that are no plain value.</param>
    /// <param name="Prefabs">The game objects the rules use it on (by its name, or by a filter its values pass), sorted.</param>
    public sealed record CustomData(string Name, IReadOnlyList<DataValue> Values, IReadOnlyList<string> Skipped, IReadOnlyList<string> Prefabs);

    /// <summary>What was found of the data entries.</summary>
    /// <param name="Entries">The entries, by name.</param>
    /// <param name="Problem">Why there are none, for the player ("": they were read).</param>
    public sealed record CustomObjects(IReadOnlyList<CustomData> Entries, string Problem);

    /// <summary>
    /// The data entries of the first BepInEx folder with Expand World Data or Expand World Prefabs installed, and the
    /// objects Expand World Prefabs' rules (expand_prefabs*.yaml) use them on. Lines about the files go to the log.
    /// </summary>
    /// <param name="folders">The BepInEx folders the editor knows.</param>
    public static CustomObjects ReadCustomObjects(IEnumerable<BepInExFolder> folders)
    {
        foreach (var folder in folders)
        {
            string plugins = Path.Combine(folder.Path, "plugins");
            if (!HasPlugin(plugins, "ExpandWorldData.dll") && !HasPlugin(plugins, "ExpandWorldPrefabs.dll"))
            {
                continue;
            }
            var yaml = YamlReader.Find(plugins);
            if (yaml == null)
            {
                return new CustomObjects([], $"No {YamlReader.DllName} in the plugins folder: the data files cannot be read.");
            }
            List<string> notes = [];
            string files = Path.Combine(folder.Path, "config", DataFolder);
            var found = Found.None with { Folder = folder.Path, DataFiles = Directory.Exists(files) ? files : null };
            var scripts = ReadKind(found.DataFiles, "prefabs", yaml, notes) ?? [];
            List<Entry> entries = [.. ReadDataEntries(found, yaml, notes), .. scripts.Where(e => e.Fields.ContainsKey("name") && !IsRule(e))];
            foreach (string note in notes)
            {
                Note($"Expand World: {note}");
            }
            var data = CustomDataOf(entries, [.. scripts.Where(IsRule)], Game.HasPrefab);
            return new CustomObjects(data, data.Count > 0 ? "" : "No data entry found in config/data or config/expand_world (expand_data*.yaml, expand_prefabs*.yaml).");
        }
        return new CustomObjects([], "Neither Expand World Data nor Expand World Prefabs is installed in the BepInEx folders the editor knows.");
    }

    private static bool IsRule(Entry e) => e.Fields.ContainsKey("prefab") || e.Fields.ContainsKey("type") || e.Fields.ContainsKey("types");

    private static readonly string[] ValueLists = ["ints", "floats", "strings", "longs", "bools", "hashes", "vecs", "quats", "bytes"];

    private static readonly Dictionary<string, string> ListOfType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int"] = "ints",
        ["float"] = "floats",
        ["string"] = "strings",
        ["long"] = "longs",
        ["bool"] = "bools",
        ["hash"] = "hashes",
        ["vec"] = "vecs",
        ["vec3"] = "vecs",
        ["quat"] = "quats",
        ["bytes"] = "bytes",
    };

    /// <summary>The data entries, each with the values the editor can set and the objects the rules use it on.</summary>
    /// <param name="entries">The data entries (the last of a name wins, as a later file's does).</param>
    /// <param name="rules">Expand World Prefabs' rules.</param>
    /// <param name="known">Whether a name is an object of the game.</param>
    internal static List<CustomData> CustomDataOf(IReadOnlyList<Entry> entries, IReadOnlyList<Entry> rules, Func<string, bool> known)
    {
        Dictionary<string, (List<DataValue> Values, List<string> Skipped)> byName = new(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Fields.GetValueOrDefault("name") is not string name || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            List<DataValue> values = [];
            List<string> skipped = [];
            foreach (var (field, value) in entry.Fields)
            {
                if (field == "name" || value == null)
                {
                    continue;
                }
                if (!ValueLists.Contains(field))
                {
                    skipped.Add(field);
                    continue;
                }
                foreach (string row in value is string one ? [one] : value is List<object?> list ? list.OfType<string>() : [])
                {
                    int comma = row.IndexOf(',', StringComparison.Ordinal);
                    string key = (comma < 0 ? row : row[..comma]).Trim(), text = comma < 0 ? "" : row[(comma + 1)..].Trim();
                    if (Setter(field, key, text) is { } set)
                    {
                        values.Add(new DataValue(field, key, text, set));
                    }
                    else
                    {
                        skipped.Add(key);
                    }
                }
            }
            byName[name] = (values, skipped);
        }
        var prefabs = byName.Keys.ToDictionary(n => n, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            Pair(rule.Fields, Names(rule.Fields.GetValueOrDefault("prefab"), known), byName, prefabs, known);
        }
        return [.. byName.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Select(e => new CustomData(e.Key, e.Value.Values, e.Value.Skipped, [.. prefabs[e.Key]]))];
    }

    /// <summary>
    /// The objects a rule uses data entries on: the rule's own objects with the entry it sets ("data") or filters by
    /// ("filter", "filters"), the objects it spawns or swaps in with an entry, and the nearby objects it asks for.
    /// </summary>
    private static void Pair(IReadOnlyDictionary<string, object?> rule, List<string> own, Dictionary<string, (List<DataValue> Values, List<string> Skipped)> byName,
        Dictionary<string, SortedSet<string>> prefabs, Func<string, bool> known)
    {
        void Add(IEnumerable<string> names, IEnumerable<string> objects)
        {
            foreach (string name in names.Where(prefabs.ContainsKey))
            {
                prefabs[name].UnionWith(objects);
            }
        }
        IEnumerable<string> Used(IReadOnlyDictionary<string, object?> fields) =>
        [
            .. fields.GetValueOrDefault("data") is string data ? [data.Trim()] : Array.Empty<string>(),
            .. Rows(fields.GetValueOrDefault("filter")).Concat(Rows(fields.GetValueOrDefault("filters"))).SelectMany(f => Passing(f, byName)),
        ];
        Add(Used(rule), own);
        foreach (string key in new[] { "spawn", "swap", "spawns", "swaps", "objects" })
        {
            foreach (object? item in rule.GetValueOrDefault(key) is List<object?> list ? list : [rule.GetValueOrDefault(key)])
            {
                if (item is Dictionary<string, object?> map)
                {
                    Add(Used(map), Names(map.GetValueOrDefault("prefab"), known));
                }
                else if (item is string line)
                {
                    // "id, position, rotation…, data": the entry is the last part when it names one.
                    string[] parts = [.. line.Split(',').Select(p => p.Trim())];
                    Add(parts.Length > 1 ? [parts[^1]] : [], Names(parts[0], known));
                }
            }
        }
    }

    private static IEnumerable<string> Rows(object? value) => value is string s ? [s] : value is List<object?> list ? list.OfType<string>() : [];

    /// <summary>The entries a filter "type, key, value" passes: those setting that key to that value.</summary>
    private static IEnumerable<string> Passing(string filter, Dictionary<string, (List<DataValue> Values, List<string> Skipped)> byName)
    {
        string[] tkv = [.. filter.Split(',', 3).Select(p => p.Trim())];
        if (tkv.Length != 3 || !ListOfType.TryGetValue(tkv[0], out string? list))
        {
            return [];
        }
        return byName.Where(e => e.Value.Values.Any(v => v.List == list && v.Key == tkv[1] && v.Text == tkv[2])).Select(e => e.Key);
    }

    /// <summary>The game objects a "prefab" field names (wildcards, value groups and the player are no object to place).</summary>
    private static List<string> Names(object? prefab, Func<string, bool> known) =>
        [.. Rows(prefab).SelectMany(SplitList).Where(n => n != "Player" && known(n))];

    /// <summary>
    /// What sets a value on an object; null for one the editor cannot write as it is: a key that is a number or an
    /// animation's, a value the mod chooses or works out as it makes the object (a parameter, a list, a range).
    /// </summary>
    private static Action<ICopy>? Setter(string list, string key, string text)
    {
        if (key.Length == 0 || key.StartsWith('$') || int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || text.Contains('<', StringComparison.Ordinal))
        {
            return null;
        }
        Action<ICopy>? set = list switch
        {
            "strings" => c => c.SetString(key, text.Length > 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text),
            "ints" when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => c => c.SetInt(key, i),
            "bools" when bool.TryParse(text, out bool b) => c => c.SetInt(key, b ? 1 : 0),
            "hashes" when text.Length > 0 && !text.Contains(',', StringComparison.Ordinal) => c => c.SetInt(key, GameHash.Of(text)),
            "floats" when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) => c => c.SetFloat(key, f),
            "longs" when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) => c => c.SetLong(key, l),
            // The mods write vectors x, z, y and turns y, x, z.
            "vecs" when Three(text, key == "scale") is { } v => c => c.SetVector3(key, new NVector3(v[0], v[2], v[1])),
            "quats" when Three(text, false) is { } e => c => c.SetQuaternion(key, Mathf.Euler(new NVector3(e[1], e[0], e[2]))),
            "bytes" when text.Length > 0 && Convert.TryFromBase64String(text, new byte[text.Length], out _) => c => c.SetBytes(key, Convert.FromBase64String(text)),
            _ => null,
        };
        if (set == null)
        {
            return null;
        }
        // A component's field ("Piece.m_name"): the game reads it only from an object marked as having fields, and
        // fields of that component.
        int dot = key.IndexOf('.', StringComparison.Ordinal);
        return dot <= 0 ? set : c =>
        {
            c.SetInt("HasFields", 1);
            c.SetInt("HasFields" + key[..dot], 1);
            set(c);
        };
    }

    /// <summary>Up to three numbers "a, b, c" (those missing 0; one alone for all three when asked); null when they are not numbers.</summary>
    private static float[]? Three(string text, bool oneForAll)
    {
        string[] parts = [.. text.Split(',').Select(p => p.Trim()).Where(p => p != "")];
        if (parts.Length is 0 or > 3)
        {
            return null;
        }
        float[] v = new float[3];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
            {
                return null;
            }
        }
        return oneForAll && parts.Length == 1 ? [v[0], v[0], v[0]] : v;
    }
}
