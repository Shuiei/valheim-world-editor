using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// The area <see cref="Inside"/> picks within an outline, in square units of its points (m² for grid points). An
    /// outline that crosses itself counts each part the even-odd way, as the selection does (a figure eight both
    /// loops, a crossed star not its middle), where the shoelace formula let crossed parts cancel out.
    /// </summary>
    public static float Area(List<Vector2> poly)
    {
        int n = poly.Count;
        // Between the heights where the outline turns or crosses itself, the inside's width changes linearly: the
        // width halfway up a slab gives its area exactly.
        List<double> ys = [.. poly.Select(p => (double)p.Y)];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1)
                {
                    continue;
                }
                if (Crossing(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]) is double y)
                {
                    ys.Add(y);
                }
            }
        }
        ys.Sort();
        double area = 0;
        List<double> xs = [];
        for (int k = 0; k + 1 < ys.Count; k++)
        {
            double y0 = ys[k], y1 = ys[k + 1];
            if (y1 <= y0)
            {
                continue;
            }
            double mid = (y0 + y1) / 2;
            xs.Clear();
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.Y > mid) != (b.Y > mid))
                {
                    xs.Add(a.X + (mid - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }
            xs.Sort();
            for (int i = 0; i + 1 < xs.Count; i += 2)
            {
                area += (xs[i + 1] - xs[i]) * (y1 - y0);
            }
        }
        return (float)area;
    }

    /// <summary>The height at which two edges cross, if they do (not where they only touch end to end).</summary>
    private static double? Crossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        double den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-12)
        {
            return null;
        }
        double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den, u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
        return t is > 0 and < 1 && u is > 0 and < 1 ? a.Y + t * ry : null;
    }
}
