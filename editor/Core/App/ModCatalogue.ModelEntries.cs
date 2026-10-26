using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Checking the entry of a model: its size, its parts (mesh, vertices, sub-mesh, material, bounds) and its matrix.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>
    /// A model: "s" three numbers, "p" a list of parts with a string "mesh", integers "v" and "sub", a string "mat" and
    /// "b" six numbers when there, and "m" 16 numbers.
    /// </summary>
    private static bool IsModel(JsonNode e)
    {
        return e is JsonObject o && o["s"] is JsonArray s && s.Count == 3 && s.All(x => Num(x))
            && o["p"] is JsonArray parts && parts.All(p => p is JsonObject part
                && part["mesh"] is { } mesh && Text(mesh) != null && Num(part["v"], true) && Num(part["sub"], true)
                && (part["mat"] is null || Text(part["mat"]!) != null)
                && (part["b"] is null || part["b"] is JsonArray b && b.Count == 6 && b.All(x => Num(x)))
                && part["m"] is JsonArray m && m.Count == 16 && m.All(x => Num(x)));
    }
}
