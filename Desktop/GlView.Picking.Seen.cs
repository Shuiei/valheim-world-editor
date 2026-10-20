using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// What is seen inside a rectangle of the view: the objects the camera sees there, not those hidden behind others.
public sealed partial class GlView
{
    /// <summary>
    /// The shown objects seen inside a rectangle of the view: those of which a point inside it (the middle of the box,
    /// or a corner of it pulled in by a third) is what a click there would find (<see cref="ObjectAt"/>: the nearest
    /// drawn shape, the ground not in front). What is behind others, or behind the ground, is left out.
    /// </summary>
    /// <param name="r">The rectangle, in the view's points.</param>
    /// <param name="size">The view's size.</param>
    internal List<int> SeenIn(Rect r, Size size)
    {
        var seen = new List<int>();
        if (Scene is not { } s || size.Width <= 0)
        {
            return seen;
        }
        // Only what is drawn over the rectangle can stand in front of what is seen inside it.
        var over = Enumerable.Range(0, _bounds.Length).Where(i => Hittable(i) && OverRect(_bounds[i], r, size)).ToList();
        foreach (int i in over)
        {
            if (SamplesOf(_bounds[i]).Any(p => SeenAt(s, i, p, r, size, over)))
            {
                seen.Add(i);
            }
        }
        return seen;
    }

    /// <summary>
    /// Whether an object is what is seen at one of its points: the point is inside the rectangle and the ray through
    /// it meets that object first, the ground not in front of it (half a metre of slack).
    /// </summary>
    private bool SeenAt(WorldScene s, int i, Vector3 p, Rect r, Size size, List<int> over)
    {
        if (ScreenOf(p, size) is not { } at || !r.Contains(at))
        {
            return false;
        }
        var (o, d) = RayAt(at, size);
        d = Vector3.Normalize(d);
        var (hit, best) = NearestMeshHit(s, o, d, BoxesHitBy(o, d, over));
        return hit == i && !(Picking.HitGround(s, o, d, best + 1) is float g && g + 0.5f < best);
    }

    /// <summary>
    /// The points of a box tried for being seen: its middle, and its eight corners pulled a third of the way in.
    /// </summary>
    /// <param name="b">The box (view space).</param>
    internal static IEnumerable<Vector3> SamplesOf((Vector3 Min, Vector3 Max) b)
    {
        var c = (b.Min + b.Max) / 2;
        yield return c;
        for (int k = 0; k < 8; k++)
        {
            yield return c + (Corner(b, k) - c) * (2f / 3);
        }
    }

    /// <summary>
    /// Whether a box is drawn over a rectangle of the view: the rectangle round its corners on the view meets it
    /// (always, when a corner is behind the camera).
    /// </summary>
    private bool OverRect((Vector3 Min, Vector3 Max) b, Rect r, Size size)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        for (int k = 0; k < 8; k++)
        {
            if (ScreenOf(Corner(b, k), size) is not { } p)
            {
                return true;
            }
            (x0, y0, x1, y1) = (Math.Min(x0, p.X), Math.Min(y0, p.Y), Math.Max(x1, p.X), Math.Max(y1, p.Y));
        }
        return x1 >= r.Left && x0 <= r.Right && y1 >= r.Top && y0 <= r.Bottom;
    }

    /// <summary>Where a view-space point is on the view, or null behind the camera.</summary>
    internal Point? ScreenOf(Vector3 p, Size size)
    {
        var q = Vector4.Transform(new Vector4(p, 1), _lastViewProj);
        return q.W <= 0 ? null : new Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }
}
