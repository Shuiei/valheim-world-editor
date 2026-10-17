using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// One thing's contribution to the extensions' shapes: the cuts it marks into the ground, and its triangles in view
// space, gathered by the kind of object, colour and sidedness they are drawn with.
public sealed partial class GlView
{
    /// <summary>
    /// Adds one thing's cuts (marked into the ground) and shapes (its triangles, in view space, by kind, colour and
    /// sidedness) from the mods' extensions.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="t">The thing.</param>
    /// <param name="view">The thing as an extension sees it.</param>
    /// <param name="around">The world round it, as the extensions see it.</param>
    /// <param name="meshes">The triangles gathered so far, by kind, colour and sidedness.</param>
    private void AddThingShapes(WorldScene s, WorldScene.Thing t, ObjectView view, ShapeWorld around,
        Dictionary<(ObjectKind Kind, Vector4 Color, bool BothSides), (List<float> V, List<uint> I)> meshes)
    {
        string name = view.Prefab;
        var q = view.Orientation;
        float scale = view.Scale;
        // A corner in view space: turned and placed as the object (or where it is, for a world-space shape), z
        // mirrored (Unity's winding then faces the view's way).
        Vector3 View(Vector3 v, bool worldSpace)
        {
            var p = worldSpace ? v : t.Position + Vector3.Transform(v * scale, q);
            return ToView(s, p);
        }
        // Grid coordinates of a point on the ground (x, z in the object's frame, or the world's).
        Vector2 Grid(Vector2 p, bool worldSpace)
        {
            var v = worldSpace ? new Vector3(p.X, 0, p.Y) : t.Position + Vector3.Transform(new Vector3(p.X, 0, p.Y) * scale, q);
            return new Vector2(v.X - s.Cx + (s.W - 1) / 2f, v.Z - s.Cz + (s.H - 1) / 2f);
        }
        foreach (var cut in ModExtensions.CutsOf(t.Prefab, view, around))
        {
            GroundCuts.Mark(s.Cut, s.W, s.H, [.. cut.Outline.Select(p => Grid(p, cut.WorldSpace))]);
            _cutMarked = s.Cut;
        }
        foreach (var shape in ModExtensions.ShapesOf(t.Prefab, view, around))
        {
            var key = (ObjectKinds.Of(name, t.Piece), shape.Color, shape.BothSides);
            if (!meshes.TryGetValue(key, out var mesh))
            {
                meshes[key] = mesh = ([], []);
            }
            for (int k = 0; k + 2 < shape.Triangles.Length; k += 3)
            {
                var a = View(shape.Vertices[shape.Triangles[k]], shape.WorldSpace);
                var b = View(shape.Vertices[shape.Triangles[k + 1]], shape.WorldSpace);
                var c = View(shape.Vertices[shape.Triangles[k + 2]], shape.WorldSpace);
                var n = Vector3.Cross(b - a, c - a);
                if (n.LengthSquared() < 1e-12f)
                {
                    continue;
                }
                n = Vector3.Normalize(n);
                foreach (var p in (Vector3[])[a, b, c])
                {
                    mesh.I.Add((uint)(mesh.V.Count / 8));
                    mesh.V.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, 0f, 0f]);
                }
            }
        }
    }
}
