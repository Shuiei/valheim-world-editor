using System.Numerics;
using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop;

/// <summary>
/// A copy read from the clipboard format (CopyFormat.FromJson.cs): its size, ground and objects checked as they are read.
/// </summary>
public static partial class CopyFormat
{
    /// <summary>A copy from the clipboard format.</summary>
    /// <param name="o">The JSON.</param>
    /// <param name="name">Its name; null: the one in the JSON.</param>
    /// <param name="keepSources">Keep the objects' source ids: they only mean something in the world the copy came from.</param>
    /// <exception cref="FormatException">
    /// The copy's size is not positive or too large (its diagonal squared does not fit the whole numbers pasting
    /// measures it in), its ground arrays are missing or not as long as its size says (W × H heights and weights, four
    /// paint values a point), an object has no name or a point of its outline is not one (two finite coordinates):
    /// pasted, it would stop half-way through.
    /// </exception>
    public static CopyData FromJson(JsonObject o, string? name = null, bool keepSources = true)
    {
        int w = I(o["w"]), h = I(o["h"]);
        CheckSize(w, h);
        long points = (long)w * h;
        var rel = Numbers(o, "rel", points, w, h);
        var wt = Numbers(o, "wt", points, w, h);
        var pnt = Numbers(o, "pnt", points * 4, w, h);
        var objects = ObjectsOf(o);
        CheckNames(objects);
        CheckOutline(o);
        return new()
        {
            W = w,
            H = h,
            Rel = [.. rel.Select(v => I(v) == -32768 ? float.NaN : I(v) / 100f)],
            Wt = [.. wt.Select(v => I(v) / 255f)],
            Pnt = [.. pnt.Select(v => I(v) < 0 ? -1f : I(v) / 255f)],
            Objects = ObjectsFrom(objects, keepSources),
            Poly = (o["poly"] as JsonArray)?.Select(p => new Vector2(F(p!["gx"]), F(p["gz"]))).ToList() ?? [],
            Name = name ?? (string?)o["name"],
            Kept = KeptOf(o),
        };
    }

    /// <summary>What mods' extensions keep, by copier name ("mods": base64 text each); none when missing.</summary>
    /// <param name="o">The copy's JSON.</param>
    /// <exception cref="FormatException">"mods" is not an object of base64 texts.</exception>
    private static Dictionary<string, byte[]> KeptOf(JsonObject o)
    {
        var kept = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (o["mods"] is null)
        {
            return kept;
        }
        if (o["mods"] is not JsonObject mods)
        {
            throw new FormatException("its \"mods\" entry is not an object");
        }
        foreach (var (name, value) in mods)
        {
            kept[name] = value is JsonValue v && v.TryGetValue(out string? text) ? Base64(name, text)
                : throw new FormatException($"its \"mods\" entry \"{name}\" is not a text");
        }
        return kept;
    }

    /// <summary>A mod's kept bytes from their base64 text.</summary>
    /// <exception cref="FormatException">The text is not base64.</exception>
    private static byte[] Base64(string name, string text)
    {
        byte[] bytes = new byte[text.Length * 3 / 4 + 3];
        return Convert.TryFromBase64String(text, bytes, out int n) ? bytes[..n]
            : throw new FormatException($"its \"mods\" entry \"{name}\" is not base64");
    }

    /// <summary>The copy's size must be a grid that pasting can measure.</summary>
    /// <param name="w">The width in grid points.</param>
    /// <param name="h">The depth in grid points.</param>
    /// <exception cref="FormatException">The size is not positive or too large.</exception>
    internal static void CheckSize(int w, int h)
    {
        if (w <= 0 || h <= 0)
        {
            throw new FormatException($"its size ({w} × {h}) is not a grid");
        }
        // Pasting measures the copy's diagonal in whole numbers: a size whose square overflows them is no real copy.
        if ((long)w * w + (long)h * h > int.MaxValue)
        {
            throw new FormatException($"its size ({w} × {h}) is too large to paste");
        }
    }

    /// <summary>The copy's objects list.</summary>
    /// <param name="o">The copy's JSON.</param>
    /// <exception cref="FormatException">It is missing.</exception>
    internal static JsonArray ObjectsOf(JsonObject o) =>
        o["objects"] as JsonArray ?? throw new FormatException("it has no objects list");

    /// <summary>A copy's ground array, checked to hold as many values as its size says.</summary>
    /// <param name="o">The copy's JSON.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="count">How many values it must hold.</param>
    /// <param name="w">The copy's width, for the message.</param>
    /// <param name="h">The copy's depth, for the message.</param>
    /// <exception cref="FormatException">It is missing or of another length.</exception>
    private static JsonArray Numbers(JsonObject o, string key, long count, int w, int h) =>
        o[key] is not JsonArray a ? throw new FormatException($"it has no \"{key}\" list")
        : a.Count != count ? throw new FormatException($"its \"{key}\" list holds {a.Count} values, not the {count} a {w} × {h} copy needs")
        : a;
}
