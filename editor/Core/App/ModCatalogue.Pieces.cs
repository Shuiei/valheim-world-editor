using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Checking the entries of building pieces and their costs.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>
    /// A piece entry: an integer "cat"; "box" six numbers; "snap" a list of points (three numbers); "tool" a string.
    /// </summary>
    private static bool IsPiece(JsonNode e)
    {
        return e is JsonObject o && Num(o["cat"], true) && OptionalBox(o["box"]) && OptionalSnaps(o["snap"])
            && (o["tool"] is null || Text(o["tool"]!) != null) && (o["comfort"] is null || Num(o["comfort"], true));
    }

    /// <summary>
    /// A cost entry: a string "n", "r" a list of [item, amount, ...], "st" a string when there.
    /// </summary>
    private static bool IsCost(JsonNode e)
    {
        return e is JsonObject o && o["n"] is { } n && Text(n) != null
            && o["r"] is JsonArray r && r.All(x => x is JsonArray a && a.Count >= 2 && a[0] is { } item && Text(item) != null && Num(a[1], true))
            && (o["st"] is null || Text(o["st"]!) != null);
    }
}
