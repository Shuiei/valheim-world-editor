using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Area tool's selection, like the web editor's (editor/area.js): a box (drag) or a polygon (click
/// points; double-click or Enter closes it) of grid points, with a soft edge inwards; and the ground
/// actions done inside it. Points are grid points of the block (x east, z north, one per metre).
/// </summary>
public sealed partial class AreaTool
{
    /// <summary>A box dragged on the ground (true) or a polygon clicked point by point (false).</summary>
    public bool Box { get; set; } = true;

    /// <summary>The soft edge in metres: how far inwards from the outline the actions fade in (0: a hard edge).</summary>
    public float Soft { get; set; } = 3;

    /// <summary>
    /// The points clicked: for a box its two opposite corners, for a polygon its corners in order. Lock it to
    /// read or change it from another thread.
    /// </summary>
    public List<Vector2> Points { get; } = [];

    /// <summary>Whether the polygon is closed: the next click then starts a new one.</summary>
    public bool Closed { get; private set; }

    /// <summary>Whether a box is being dragged (the mouse button held since <see cref="Down"/>).</summary>
    public bool Dragging { get; private set; }

    /// <summary>The selection changed.</summary>
    public event Action? Changed;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>The kinds of objects the object actions work on.</summary>
    public HashSet<ObjectKind> Kinds { get; } = [ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables];

    /// <summary>The selection's outline, or null while there is none.</summary>
    public List<Vector2>? Polygon()
    {
        lock (Points)
        {
            if (Box)
            {
                if (Points.Count < 2)
                {
                    return null;
                }
                Vector2 a = Points[0], b = Points[1];
                return [a, new(b.X, a.Y), b, new(a.X, b.Y)];
            }
            return Points.Count >= 3 ? [.. Points] : null;
        }
    }

    /// <summary>Drops the selection.</summary>
    public void Clear()
    {
        lock (Points)
        {
            Points.Clear();
        }
        Closed = false;
        Dragging = false;
        Notify();
    }

    // ---- Geometry.
    /// <summary>Whether a point is inside an outline.</summary>
    public static bool Inside(List<Vector2> poly, float x, float z) => SelectTool.Inside(poly, x, z);

    /// <summary>The distance from a point to an outline's nearest edge, in the outline's units.</summary>
    public static float EdgeDistance(List<Vector2> poly, float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[j], d = poly[i] - a;
            float l2 = MathF.Max(d.LengthSquared(), 1e-9f);
            float t = Math.Clamp(((x - a.X) * d.X + (z - a.Y) * d.Y) / l2, 0, 1);
            best = MathF.Min(best, Vector2.Distance(new Vector2(x, z), a + d * t));
        }
        return best;
    }
}
