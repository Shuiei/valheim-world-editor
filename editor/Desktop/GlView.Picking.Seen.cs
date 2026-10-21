using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// What is seen inside a rectangle of the view: what a click would find at each point of it, not what is behind.
public sealed partial class GlView
{
    /// <summary>The side of the view's squares (pixels) the objects drawn over a rectangle are sorted into.</summary>
    private const int SeenCell = 32;

    /// <summary>
    /// The shown objects seen inside a rectangle of the view: what a click would find (<see cref="ObjectAt"/>: the
    /// nearest drawn shape, the ground not in front) at the points of a grid over it, every 3 pixels, wider for a big
    /// rectangle (about 40,000 points at most). What is behind others, or behind the ground, is left out.
    /// </summary>
    /// <param name="r">The rectangle, in the view's points.</param>
    /// <param name="size">The view's size.</param>
    internal List<int> SeenIn(Rect r, Size size)
    {
        var seen = new HashSet<int>();
        if (Scene is not { } s || size.Width <= 0 || r.Width < 1 || r.Height < 1)
        {
            return [];
        }
        var over = OverCells(r, size);
        double step = Math.Max(3, Math.Sqrt(r.Width * r.Height / 40_000));
        for (double y = r.Top + step / 2; y < r.Bottom; y += step)
        {
            for (double x = r.Left + step / 2; x < r.Right; x += step)
            {
                if (FirstSeen(s, new Point(x, y), size, over) is int i)
                {
                    seen.Add(i);
                }
            }
        }
        return [.. seen.Order()];
    }

    /// <summary>
    /// What a click at a point would find among the objects drawn over its square: the nearest drawn shape, unless
    /// the ground is in front of it (half a metre of slack); null when nothing is.
    /// </summary>
    private int? FirstSeen(WorldScene s, Point at, Size size, Dictionary<(int, int), List<int>> over)
    {
        if (!over.TryGetValue(((int)(at.X / SeenCell), (int)(at.Y / SeenCell)), out var here))
        {
            return null;
        }
        var (o, d) = RayAt(at, size);
        d = Vector3.Normalize(d);
        var (hit, best) = NearestMeshHit(s, o, d, BoxesHitBy(o, d, here));
        return hit != null && Picking.HitGround(s, o, d, best + 1) is float g && g + 0.5f < best ? null : hit;
    }

    /// <summary>
    /// The objects that can be met by a ray (<see cref="Hittable"/>) drawn over a rectangle, by the view's square
    /// (<see cref="SeenCell"/> pixels) they are drawn over: only those can be what a point of that square sees.
    /// </summary>
    private Dictionary<(int, int), List<int>> OverCells(Rect r, Size size)
    {
        var cells = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < _bounds.Length; i++)
        {
            if (!Hittable(i))
            {
                continue;
            }
            var box = ScreenRectOf(_bounds[i], size, r);
            if (!box.Intersects(r))
            {
                continue;
            }
            var on = box.Intersect(r);
            for (int cy = (int)(on.Top / SeenCell); cy <= (int)(on.Bottom / SeenCell); cy++)
            {
                for (int cx = (int)(on.Left / SeenCell); cx <= (int)(on.Right / SeenCell); cx++)
                {
                    (cells.TryGetValue((cx, cy), out var l) ? l : cells[(cx, cy)] = []).Add(i);
                }
            }
        }
        return cells;
    }

    /// <summary>
    /// The rectangle round a box's corners on the view; the whole of <paramref name="r"/> when a corner is behind the
    /// camera (the box may then be drawn anywhere over it).
    /// </summary>
    private Rect ScreenRectOf((Vector3 Min, Vector3 Max) b, Size size, Rect r)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        for (int k = 0; k < 8; k++)
        {
            if (ScreenOf(Corner(b, k), size) is not { } p)
            {
                return r;
            }
            (x0, y0, x1, y1) = (Math.Min(x0, p.X), Math.Min(y0, p.Y), Math.Max(x1, p.X), Math.Max(y1, p.Y));
        }
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// The shown objects inside a rectangle of the view, seen or hidden behind others (Select through, like Blender's
    /// X-ray): all those seen there (<see cref="SeenIn"/>, however thin a part of them crosses it), and those of which
    /// a point (the middle of the box, or a corner of it pulled a third of the way in) is inside it.
    /// </summary>
    /// <param name="r">The rectangle, in the view's points.</param>
    /// <param name="size">The view's size.</param>
    internal List<int> InsideRect(Rect r, Size size)
    {
        var inside = SeenIn(r, size).ToHashSet();
        if (Scene == null || size.Width <= 0)
        {
            return [.. inside];
        }
        for (int i = 0; i < _bounds.Length; i++)
        {
            if (Hittable(i) && SamplesOf(_bounds[i]).Any(p => ScreenOf(p, size) is { } at && r.Contains(at)))
            {
                inside.Add(i);
            }
        }
        return [.. inside.Order()];
    }

    /// <summary>
    /// The points of a box tried for being inside a rectangle: its middle, and its eight corners pulled a third of the
    /// way in.
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

    /// <summary>Where a view-space point is on the view, or null behind the camera.</summary>
    internal Point? ScreenOf(Vector3 p, Size size)
    {
        var q = Vector4.Transform(new Vector4(p, 1), _lastViewProj);
        return q.W <= 0 ? null : new Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }
}
