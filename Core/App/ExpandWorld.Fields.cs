using System.Globalization;
using ValheimGen;

namespace TerrainEditor.App;

// An entry's fields as values, as the mod's typed fields take them.
public static partial class ExpandWorld
{
    /// <summary>
    /// An entry's fields read as the mod's typed fields would read them: text, numbers (invariant culture), true or
    /// false (YamlDotNet's words: true/false, yes/no, on/off, y/n), each with its default when the field is missing. A
    /// value that is not what the field takes is noted and the default used (the mod would refuse the whole file).
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="kind">The file kind, for the notes.</param>
    /// <param name="notes">Where a value not understood is noted.</param>
    private sealed class Fields(Entry entry, string kind, List<string> notes)
    {
        /// <summary>The file kind the entry is from ("world", "locations"...).</summary>
        public string Kind => kind;

        /// <summary>
        /// A field the mod reads as a list of texts (its StringList): a text is one item, a list its items (each a
        /// text; blank ones left out); none when the field is missing.
        /// </summary>
        public IReadOnlyList<string> Rows(string key)
        {
            if (!entry.Fields.TryGetValue(key, out object? v) || v == null)
            {
                return [];
            }
            return v switch
            {
                string s => string.IsNullOrWhiteSpace(s) ? [] : [s],
                List<object?> list => [.. list.OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s))],
                _ => [],
            };
        }

        /// <summary>Whether the entry has the field, with a value.</summary>
        public bool Has(string key) => entry.Fields.TryGetValue(key, out object? v) && v != null;

        /// <summary>A text field; the default when missing or not a scalar.</summary>
        public string Str(string key, string fallback = "") => entry.Fields.TryGetValue(key, out object? v) && v is string s ? s : fallback;

        /// <summary>A number field; the default when missing (or not a number: noted).</summary>
        public float F(string key, float fallback) => FOpt(key) ?? fallback;

        /// <summary>A number field that may be left out: null when missing (or not a number: noted).</summary>
        public float? FOpt(string key)
        {
            if (!Has(key))
            {
                return null;
            }
            string text = Str(key);
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                return f;
            }
            notes.Add($"expand_{kind}: {key} \"{text}\" is not a number (left out)");
            return null;
        }

        /// <summary>A true-or-false field; the default when missing (or neither: noted).</summary>
        public bool B(string key, bool fallback)
        {
            if (!Has(key))
            {
                return fallback;
            }
            string text = Str(key);
            switch (text.ToLowerInvariant())
            {
                case "true" or "yes" or "on" or "y":
                    return true;
                case "false" or "no" or "off" or "n":
                    return false;
                default:
                    notes.Add($"expand_{kind}: {key} \"{text}\" is not true or false (taken as {(fallback ? "true" : "false")})");
                    return fallback;
            }
        }

        /// <summary>A colour field ("r, g, b" or "r, g, b, a", 0..1; a channel missing or not a number is 0, alpha 1); null when missing.</summary>
        public Rgba? Colour(string key) => Has(key) ? ParseColour(Str(key)) : null;

        /// <summary>How many items a list field has (0 when missing or not a list).</summary>
        public int Count(string key) => entry.Fields.TryGetValue(key, out object? v) && v is List<object?> list ? list.Count : 0;
    }

    /// <summary>A colour as the mod parses one: "r, g, b" or "r, g, b, a" (0..1), a missing or unreadable channel 0, alpha 1.</summary>
    /// <param name="text">The colour's text.</param>
    internal static Rgba ParseColour(string text)
    {
        string[] parts = text.Split(',');
        return new Rgba(Channel(parts, 0, 0f), Channel(parts, 1, 0f), Channel(parts, 2, 0f), Channel(parts, 3, 1f));
    }

    /// <summary>One channel of a colour's parts: its number, the default when missing or not a number.</summary>
    private static float Channel(string[] parts, int i, float fallback) =>
        parts.Length > i && float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

    /// <summary>
    /// A paint as the mod parses one: a paint name (grass, patches, grass_dark, dirt, cultivated, paved, paved_moss,
    /// paved_dirt, paved_dark), else a colour.
    /// </summary>
    /// <param name="text">The paint's text.</param>
    internal static Rgba ParsePaint(string text) => Paints.TryGetValue(text, out var paint) && text.Split(',').Length < 3 ? paint : ParseColour(text);

    /// <summary>The paint names the mod knows, as the ground's mask colours.</summary>
    private static readonly Dictionary<string, Rgba> Paints = new()
    {
        ["grass"] = new(0f, 0f, 0f, 1f),
        ["patches"] = new(0f, 0.75f, 0f, 1f),
        ["grass_dark"] = new(0.6f, 0.5f, 0f, 1f),
        ["dirt"] = new(1f, 0f, 0f, 1f),
        ["cultivated"] = new(0f, 1f, 0f, 1f),
        ["paved"] = new(0f, 0f, 1f, 1f),
        ["paved_moss"] = new(0f, 0f, 0.5f, 1f),
        ["paved_dirt"] = new(1f, 0f, 0.5f, 1f),
        ["paved_dark"] = new(0f, 1f, 0.5f, 1f),
    };

    /// <summary>A comma-separated list's items, trimmed, empty ones left out (the mod's Parse.Split).</summary>
    /// <param name="text">The list.</param>
    internal static string[] SplitList(string text) => [.. text.Split(',').Select(s => s.Trim()).Where(s => s != "")];

    /// <summary>A territory's name as the mod keys it: trimmed and lower-cased.</summary>
    /// <param name="name">The name as written.</param>
    internal static string TerritoryKey(string name) => name.Trim().ToLowerInvariant();
}
