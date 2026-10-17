using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's list of the open area's objects (list_objects), and the description each object gets in the answers.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>list_objects: objects of the open area, filtered by kind, name and distance (limit clamped to 1 to 2000).</summary>
    [McpServerTool(Name = "list_objects", ReadOnly = true, Title = "List objects")]
    [Description("Objects of the open area: id, name, kind, position (x, y, z), turn (yaw in degrees), tilts (pitch, roll) and size (scale) when it has them, and whether a player built it. Filter by kind (Buildings, Trees, Rocks…), by a word of the name, and/or near a point; at most limit of them, nearest first when near a point.")]
    public Task<string> ListObjects(
        [Description("A kind, as describe_area counts them (optional).")] string? kind = null,
        [Description("A word the name must hold (optional).")] string? name = null,
        [Description("World x of a point to look near (optional, with z and radius).")] float? x = null,
        [Description("World z of that point.")] float? z = null,
        [Description("How far from it, in metres.")] float radius = 20,
        [Description("At most this many.")] int limit = 200) => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open.";
        }
        if (LoneCoordinate(x, z) is string lone)
        {
            return lone;
        }
        List<object> found;
        int reads;
        lock (scene.Things)
        {
            reads = scene.Reads;
            var q = scene.Things.Select((t, i) => (t, i)).Where(p => !p.t.Gone);
            if (kind != null)
            {
                q = q.Where(p => string.Equals(KindOf(p.t), kind, StringComparison.OrdinalIgnoreCase));
            }
            if (name != null)
            {
                q = q.Where(p => (_w.NameOfPrefab(p.t.Prefab) ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
            }
            if (x is float px && z is float pz)
            {
                q = q.Where(p => (p.t.Position.X - px) * (p.t.Position.X - px) + (p.t.Position.Z - pz) * (p.t.Position.Z - pz) <= radius * radius)
                    .OrderBy(p => (p.t.Position.X - px) * (p.t.Position.X - px) + (p.t.Position.Z - pz) * (p.t.Position.Z - pz));
            }
            found = [.. q.Take(Math.Clamp(limit, 1, 2000)).Select(p => Describe(p.t, p.i))];
        }
        // (A listing with nothing in it gives no id: ids given before stay refused.)
        if (found.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(found);
    });

    /// <summary>
    /// An object as the tools give it: id (its index in the scene), name, kind, position, yaw, built; its tilts and
    /// size only when it has them (most objects stand upright at the prefab's own size).
    /// </summary>
    private Dictionary<string, object?> Describe(WorldScene.Thing t, int i)
    {
        var d = new Dictionary<string, object?>
        {
            ["id"] = i,
            ["name"] = _w.NameOfPrefab(t.Prefab),
            ["kind"] = KindOf(t),
            ["x"] = MathF.Round(t.Position.X, 2),
            ["y"] = MathF.Round(t.Position.Y, 2),
            ["z"] = MathF.Round(t.Position.Z, 2),
            ["yaw"] = MathF.Round(t.Rotation.Y, 1),
            ["built"] = t.Piece,
        };
        if (MathF.Abs(t.Rotation.X) >= 0.05f || MathF.Abs(t.Rotation.Z) >= 0.05f)
        {
            d["pitch"] = MathF.Round(t.Rotation.X, 1);
            d["roll"] = MathF.Round(t.Rotation.Z, 1);
        }
        if (t.Scale > 0 && MathF.Abs(t.Scale - 1) >= 1e-3f)
        {
            d["scale"] = MathF.Round(t.Scale, 3);
        }
        return d;
    }
}
