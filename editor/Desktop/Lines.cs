using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

/// <summary>Lines drawn through points on the ground (the Path and Place tools): smooth curves and what is under the mouse.</summary>
internal static class Lines
{
    /// <summary>
    /// A Catmull-Rom curve through the points, about 1 m between its points; Seg is the index of the point each stretch
    /// starts from. At least two points.
    /// </summary>
    /// <param name="p">The points.</param>
    internal static List<(Vector2 P, int Seg)> CatmullRom(IReadOnlyList<Vector2> p)
    {
        var outp = new List<(Vector2, int)>();
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Count - 1, i + 2)];
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                outp.Add((0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3), i));
            }
        }
        outp.Add((p[^1], p.Count - 2));
        return outp;
    }

    /// <summary>The point within 10 px of the mouse, or -1.</summary>
    /// <param name="pts">The points.</param>
    /// <param name="at">The mouse.</param>
    /// <param name="screen">Where a point is on the view (null: behind the camera).</param>
    internal static int PointNear(IReadOnlyList<Vector2> pts, Point at, Func<Vector2, Point?> screen)
    {
        int best = -1;
        double bd = 10;
        for (int i = 0; i < pts.Count; i++)
        {
            if (screen(pts[i]) is { } s && Point.Distance(s, at) < bd)
            {
                bd = Point.Distance(s, at);
                best = i;
            }
        }
        return best;
    }

    /// <summary>The stretch of a dense line within 8 px of the mouse: the index of the point it starts from, or -1.</summary>
    /// <param name="dense">The line, with each point's stretch.</param>
    /// <param name="at">The mouse.</param>
    /// <param name="screen">Where a point is on the view (null: behind the camera).</param>
    internal static int StretchNear(IReadOnlyList<(Vector2 P, int Seg)> dense, Point at, Func<Vector2, Point?> screen)
    {
        int best = -1;
        double bd = 8;
        for (int i = 1; i < dense.Count; i++)
        {
            if (screen(dense[i - 1].P) is not { } a || screen(dense[i].P) is not { } b)
            {
                continue;
            }
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = Math.Max(dx * dx + dy * dy, 1e-9);
            double t = Math.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / l2, 0, 1);
            double d = Point.Distance(at, new Point(a.X + dx * t, a.Y + dy * t));
            if (d < bd)
            {
                bd = d;
                best = dense[i - 1].Seg;
            }
        }
        return best;
    }
}
