using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// A copy read from the clipboard format (CopyFormat.Objects.cs): its objects and outline, checked as they are read.
/// </summary>
public static partial class CopyFormat
{
    /// <summary>Whether a node is a finite number: an outline point's coordinate.</summary>
    /// <param name="n">The node.</param>
    private static bool IsCoordinate(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number && float.IsFinite(F(v));

    /// <summary>Each object of the list must have a name.</summary>
    /// <param name="objects">The objects list.</param>
    /// <exception cref="FormatException">An object has no name.</exception>
    internal static void CheckNames(JsonArray objects)
    {
        if (objects.Any(x => x is not JsonObject ob || ob["name"] is not JsonValue v || !v.TryGetValue(out string? n) || string.IsNullOrWhiteSpace(n)))
        {
            throw new FormatException("an object in it has no name");
        }
    }

    /// <summary>The outline, when there is one, must hold only points with two finite coordinates.</summary>
    /// <param name="o">The copy's JSON.</param>
    /// <exception cref="FormatException">A point of the outline is not one.</exception>
    internal static void CheckOutline(JsonObject o)
    {
        if (o["poly"] is JsonArray outline && outline.Any(p => p is not JsonObject pt || !IsCoordinate(pt["gx"]) || !IsCoordinate(pt["gz"])))
        {
            throw new FormatException("a point of its outline is not a point");
        }
    }

    /// <summary>The copy's objects, as read.</summary>
    /// <param name="objects">The objects list, checked.</param>
    /// <param name="keepSources">Keep the objects' source ids.</param>
    internal static List<CopyData.Obj> ObjectsFrom(JsonArray objects, bool keepSources) =>
        [.. objects.Select(x => new CopyData.Obj(StableHash.OfName((string)x!["name"]!), (string)x["name"]!, F(x["dx"]), F(x["dz"]), F(x["dy"]),
            new Vector3(F(x["rx"]), F(x["ry"]), F(x["rz"])), F(x["scale"]), keepSources && x["sourceId"] != null ? I(x["sourceId"]) : null, (bool?)x["follow"] ?? false,
            BlueprintFormats.DataOf(x["data"])))];
}
