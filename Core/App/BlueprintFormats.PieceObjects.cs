// BlueprintFormats: a part of the blueprint clipboard conversion (see BlueprintFormats.cs).

using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// The objects of a blueprint's clipboard: each piece as the clipboard keeps it.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// The clipboard's objects for the pieces: each at its place relative to the copy's centre (cx, cz), its height
    /// relative to the lowest piece (minY), with its scale (0 when it is the prefab's own) and its #EditorData fields.
    /// </summary>
    /// <param name="pieces">The pieces the clipboard keeps.</param>
    /// <param name="cx">The copy's centre, on x.</param>
    /// <param name="cz">The copy's centre, on z.</param>
    /// <param name="minY">The height the copy's pieces rest on.</param>
    internal static JsonArray PieceObjects(List<Piece> pieces, float cx, float cz, float minY)
    {
        JsonArray objects = [];
        foreach (Piece p in pieces)
        {
            objects.Add(new JsonObject
            {
                ["name"] = p.Name,
                ["dx"] = R(p.Position.X - cx),
                ["dz"] = R(p.Position.Z - cz),
                ["dy"] = R(p.Position.Y - minY),
                ["rx"] = R(p.Euler.X),
                ["ry"] = R(p.Euler.Y),
                ["rz"] = R(p.Euler.Z),
                ["scale"] = MathF.Abs(p.Scale - 1f) < 1e-3f ? 0 : R(p.Scale),
                ["sourceId"] = null,
            });
            if (p.Data is { Count: > 0 } fields)
            {
                ((JsonObject)objects[^1]!)["data"] = DataJson(fields);
            }
        }
        return objects;
    }
}
