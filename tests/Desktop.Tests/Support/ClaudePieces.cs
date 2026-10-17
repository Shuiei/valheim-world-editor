using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Pieces of a blueprint as the Claude tools read them, made up for the tests.
/// </summary>
internal static class ClaudePieces
{
    /// <summary>
    /// A piece of a blueprint: this prefab, this offset east from the middle and this height from the reference, not turned.
    /// </summary>
    /// <param name="prefab">The prefab's name.</param>
    /// <param name="dx">The offset east, in metres.</param>
    /// <param name="dy">The height from the reference, in metres.</param>
    internal static JsonNode Piece(string prefab, float dx, float dy) => new JsonObject
    {
        ["name"] = prefab,
        ["dx"] = dx,
        ["dy"] = dy,
        ["dz"] = 0,
        ["rx"] = 0,
        ["ry"] = 0,
        ["rz"] = 0,
        ["scale"] = 0,
    };
}
