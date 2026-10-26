using System.Globalization;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using NVector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

namespace ExpandWorld.WorldEditor;

// Expand World Data's data entries (the "data" a location or an object is given), as far as they can be written as
// they are: values the mod computes when it spawns the object (a choice, a range, a function, a parameter) cannot.
public static partial class ExpandWorldMod
{
    /// <summary>
    /// The data entries a world's files name (BepInEx/config/data/*.yaml and expand_world/expand_data*.yaml), each as
    /// the values it sets, or why it cannot be written ahead of the game.
    /// </summary>
    internal sealed class DataTable
    {
        /// <summary>No entries.</summary>
        public static readonly DataTable Empty = new([]);

        /// <summary>Each named entry: its values, or why they cannot be written.</summary>
        private readonly Dictionary<string, DataValues> _byName = new(StringComparer.Ordinal);

        /// <summary>The entries of the data files, in the order read (a later one of a name replaces an earlier one).</summary>
        public DataTable(IReadOnlyList<Entry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Fields.GetValueOrDefault("name") is string name)
                {
                    _byName[name] = Of(entry.Fields);
                }
            }
        }

        /// <summary>
        /// What a location's or an object's data reference gives (DataHelper.Get): an entry's name, the shorthand
        /// "type, key, value", or an entry written in place; a base64 package or an unknown name cannot be written.
        /// </summary>
        public DataValues Resolve(object? reference) => reference switch
        {
            null => DataValues.Nothing,
            IReadOnlyDictionary<string, object?> inline => Of(inline),
            string name when string.IsNullOrWhiteSpace(name) => DataValues.Nothing,
            string name when _byName.TryGetValue(name, out var found) => found,
            string name when Shorthand(name) is { } values => values,
            string name => DataValues.Cannot($"data \"{name}\" is no entry the editor reads (a base64 package, or a name of no file)"),
            _ => DataValues.Cannot("data that is neither a name nor an entry"),
        };

        /// <summary>The shorthand "type, key, value" (the mod's supported types); null when the text is not one.</summary>
        private static DataValues? Shorthand(string text)
        {
            string[] tkv = [.. text.Split(',', 3).Select(s => s.Trim())];
            if (tkv.Length != 3 || !Types.TryGetValue(tkv[0].ToLowerInvariant(), out string? list))
            {
                return null;
            }
            var values = new DataValues([]);
            Add(values, list, $"{tkv[1]}, {tkv[2]}");
            return values;
        }

        /// <summary>The data lists of an entry, by name.</summary>
        private static readonly HashSet<string> Lists = ["ints", "floats", "strings", "longs", "bools", "hashes", "vecs", "quats", "bytes"];

        /// <summary>The shorthand's types (the mod's supported types), with the list each fills.</summary>
        private static readonly Dictionary<string, string> Types = new(StringComparer.Ordinal)
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

        /// <summary>The fields an entry's yaml has that are not data lists, which the editor passes over.</summary>
        private static readonly HashSet<string> Passed = ["name", "valueGroup", "value", "values"];

        /// <summary>An entry's values (DataEntry.Load of its yaml), as far as they can be written.</summary>
        private static DataValues Of(IReadOnlyDictionary<string, object?> fields)
        {
            var values = new DataValues([]);
            foreach (var (key, value) in fields)
            {
                if (value == null || Passed.Contains(key))
                {
                    continue;
                }
                if (!Lists.Contains(key))
                {
                    return DataValues.Cannot($"data with \"{key}\" (set when the game spawns the object)");
                }
                foreach (string row in Rows(value))
                {
                    Add(values, key, row);
                }
            }
            return values;
        }

        /// <summary>A data list's rows (one text, or a list of them).</summary>
        private static IEnumerable<string> Rows(object value) => value switch
        {
            string s => [s],
            List<object?> list => list.OfType<string>(),
            _ => [],
        };

        /// <summary>One "key, value" row of a list added to the values (or, when it cannot be written, why).</summary>
        private static void Add(DataValues values, string list, string row)
        {
            if (values.Why != null)
            {
                return;
            }
            // As the mod's Parse.Kvp splits it: the value trimmed, the key as written (the game hashes it as it is).
            int comma = row.IndexOf(',', StringComparison.Ordinal);
            string key = comma < 0 ? row : row[..comma], text = comma < 0 ? "" : row[(comma + 1)..].Trim();
            if (KeyHash(key) is not { } hash)
            {
                values.Why = $"data key \"{key}\" (an animation or a component's field)";
                return;
            }
            if (Field(list, hash, text) is { } field)
            {
                values.Fields.Add(field);
            }
            else
            {
                values.Why = $"data value \"{text}\" of {list} (chosen or computed when the game spawns the object)";
            }
        }

        /// <summary>A key's hash as the mod makes it (ZdoHelper.Hash): a number as it is, else the name's stable hash; null for an animation or a component's field.</summary>
        private static int? KeyHash(string key)
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return number;
            }
            return key.StartsWith('$') || key.Contains('.', StringComparison.Ordinal) ? null : GameHash.Of(key);
        }

        /// <summary>
        /// A value as a field, when it is a single literal of its list's type (DataValue's constants); null when the mod
        /// works it out when it spawns the object (several choices, a range, a function or a parameter).
        /// </summary>
        private static ContentField? Field(string list, int key, string text)
        {
            bool quoted = list == "strings" && text.Length > 2 && text[0] == '"' && text[^1] == '"';
            if (quoted)
            {
                return new("strings", key, text[1..^1]);
            }
            if (text.Contains('<', StringComparison.Ordinal) || (list != "vecs" && list != "quats" && text.Contains(',', StringComparison.Ordinal)))
            {
                return null;
            }
            return Number(list, key, text) ?? Other(list, key, text);
        }

        /// <summary>A float as text that reads back the same.</summary>
        private static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A number of the number lists (ints, floats, longs, bools, hashes as ints); null otherwise.</summary>
        private static ContentField? Number(string list, int key, string text) => list switch
        {
            "ints" when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => new("ints", key, i.ToString(CultureInfo.InvariantCulture)),
            "floats" when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) => new("floats", key, F(f)),
            "longs" when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) => new("longs", key, l.ToString(CultureInfo.InvariantCulture)),
            "bools" when bool.TryParse(text, out bool b) => new("ints", key, b ? "1" : "0"),
            "hashes" when text.Length > 0 => new("ints", key, GameHash.Of(text).ToString(CultureInfo.InvariantCulture)),
            _ => null,
        };

        /// <summary>A value of the other lists (strings, vectors x, z, y, turns y, x, z, base64 bytes); null otherwise.</summary>
        private static ContentField? Other(string list, int key, string text) => list switch
        {
            "strings" => new("strings", key, text),
            "vecs" when Numbers(text) is { } v => new("vec3", key, $"{F(v[0])} {F(v[2])} {F(v[1])}"),
            "quats" when Numbers(text) is { } e => Quat(key, Mathf.Euler(new NVector3(e[1], e[0], e[2]))),
            "bytes" when Base64(text) => new("bytes", key, text),
            _ => null,
        };

        /// <summary>A rotation as its four components.</summary>
        private static ContentField Quat(int key, Quaternion q) =>
            new("quats", key, string.Join(' ', new[] { q.X, q.Y, q.Z, q.W }.Select(c => c.ToString("R", CultureInfo.InvariantCulture))));

        /// <summary>
        /// One to three numbers separated by commas, the missing ones 0 (the mod's vector and angle parsing); null for
        /// anything else (more numbers, a distance and an angle, a word).
        /// </summary>
        private static float[]? Numbers(string text)
        {
            string[] parts = [.. text.Split(',').Select(p => p.Trim()).Where(p => p != "")];
            float[] v = new float[3];
            if (parts.Length is 0 or > 3)
            {
                return null;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
                {
                    return null;
                }
            }
            return v;
        }

        /// <summary>Whether a text is base64 (an empty one is no value).</summary>
        private static bool Base64(string text) => text.Length > 0 && Convert.TryFromBase64String(text, new byte[text.Length], out _);
    }

    /// <summary>A data entry's values, or why they cannot be written ahead of the game.</summary>
    /// <param name="fields">The values.</param>
    internal sealed class DataValues(List<ContentField> fields)
    {
        /// <summary>No data.</summary>
        public static DataValues Nothing => new([]);

        /// <summary>The values.</summary>
        public List<ContentField> Fields { get; } = fields;

        /// <summary>Why they cannot be written; null when they can.</summary>
        public string? Why { get; set; }

        /// <summary>Data that cannot be written, for this reason.</summary>
        public static DataValues Cannot(string why) => new([]) { Why = why };

        /// <summary>The values, none as null.</summary>
        public IReadOnlyList<ContentField>? OrNull => Fields.Count > 0 ? Fields : null;
    }
}
