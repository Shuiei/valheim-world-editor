using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The shapes running extensions draw for the objects of their prefabs.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension draws shapes at all.</summary>
    public static bool AnyShapes => _state.Hooks.Any(l => l.Shapes.Count > 0);

    /// <summary>Whether a running extension draws shapes for a prefab.</summary>
    public static bool HasShapes(int prefab) => _state.Hooks.Any(l => l.Shapes.ContainsKey(prefab));

    /// <summary>The shapes running extensions draw for an object of a prefab (none when none does).</summary>
    /// <param name="prefab">The prefab's hash.</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="world">The world around it (<see cref="NoWorld"/>: nothing to find).</param>
    public static List<Shape> ShapesOf(int prefab, IObjectView obj, IWorldView world)
    {
        var shapes = new List<Shape>();
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Shapes.GetValueOrDefault(prefab) ?? [])
            {
                shapes.AddRange(Guard(l, () => p.ShapesOf(obj, world)?.Where(Valid).ToList() ?? [], []));
            }
        }
        return shapes;
    }
    /// <summary>Whether a shape can be drawn: whole triangles of corners it has, finite corners.</summary>
    private static bool Valid(Shape s) =>
        s.Vertices != null && s.Triangles != null && s.Triangles.Length % 3 == 0
        && s.Triangles.All(i => i >= 0 && i < s.Vertices.Length)
        && s.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));
}
