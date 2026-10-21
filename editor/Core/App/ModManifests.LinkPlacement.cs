using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading the placement of a link rule of a manifest: its id, the keys it is placed after, its offset and rotations.
/// </summary>
public static partial class ModManifests
{
    /// <summary>The placement fields of a link rule: its id key, the keys it is placed after, its offset and its rotations.</summary>
    private static (string Id, string[] Parents, string Offset, string? Euler, string? Quat)? LinkPlacement(JsonObject o)
    {
        if (!Text(o["id"], out string? id) || id == null || !Names(o["parents"], out string[] parents) || parents.Length == 0
            || !Text(o["offset"], out string? offset) || offset == null || !Text(o["rotation"], out string? euler)
            || !Text(o["rotationQuaternion"], out string? quat))
        {
            return null;
        }
        return (id, parents, offset, euler, quat);
    }
}
