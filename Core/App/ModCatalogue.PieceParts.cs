using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Checking the optional parts of a building piece entry: its box and its snap points.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>An optional box: absent, or six numbers.</summary>
    private static bool OptionalBox(JsonNode? box) => box is null || box is JsonArray b && b.Count == 6 && b.All(x => Num(x));

    /// <summary>Optional snap points: absent, or a list of points of three numbers each.</summary>
    private static bool OptionalSnaps(JsonNode? snap) =>
        snap is null || snap is JsonArray snaps && snaps.All(s => s is JsonArray p && p.Count == 3 && p.All(x => Num(x)));
}
