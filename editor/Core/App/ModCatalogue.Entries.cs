using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Checking the entries of a report or the kept file: the values they hold and whether an entry is a prefab.
/// </summary>
public static partial class ModCatalogue
{
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
}
