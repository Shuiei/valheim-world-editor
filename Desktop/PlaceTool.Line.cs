using System.Numerics;

namespace TerrainEditor.Desktop;

// The line mode: the curve along the points, and the length walked along a run.
public sealed partial class PlaceTool
{
    /// <summary>
    /// The line through the points: a smooth curve or straight stretches (Seg: the point each starts from).
    /// </summary>
    public List<(Vector2 P, int Seg)> LineCurve()
    {
        List<Vector2> p;
        lock (Points)
        {
            p = [.. Points];
        }
        if (p.Count < 3 || !Curve)
        {
            return [.. p.Select((q, i) => (q, Math.Min(i, Math.Max(0, p.Count - 2))))];
        }
        return Lines.CatmullRom(p);
    }

    /// <summary>
    /// End to end: the figure's size snaps so that whole pieces of the first chosen kind close it; returns the corner
    /// or edge point to use instead of <c>b</c>.
    /// </summary>
    /// <param name="a">The circle's centre or the rectangle's first corner.</param>
    /// <param name="b">The point dragged to.</param>
    public Vector2 SnapFigure(Vector2 a, Vector2 b)
    {
        var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
        if (e == null)
        {
            return b;
        }
        if (LineShape == LineShapes.Rect)
        {
            float Fit(float d) => MathF.Sign(d == 0 ? 1 : d) * MathF.Max(1, MathF.Round(MathF.Abs(d) / e.Len)) * e.Len;
            return new Vector2(a.X + Fit(b.X - a.X), a.Y + Fit(b.Y - a.Y));
        }
        float r = Vector2.Distance(a, b);
        int n = Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len));
        float radius = e.Len / (2 * MathF.Sin(MathF.PI / n)), t = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        return new Vector2(a.X + MathF.Cos(t) * radius, a.Y + MathF.Sin(t) * radius);
    }

    /// <summary>
    /// The point along the run at a straight distance dist from s, searching from segment seg at fraction t0 (forwards
    /// only), or null past its end.
    /// </summary>
    private static (Vector2 Q, int Seg, float T)? AlongRun(List<Vector2> pts, Vector2 s, int seg, float t0, float dist)
    {
        float tStart = t0;
        for (; seg < pts.Count - 1; seg++, tStart = 0)
        {
            Vector2 a = pts[seg], d = pts[seg + 1] - a, f = a - s;
            float dd = d.LengthSquared(), b = 2 * Vector2.Dot(f, d), c = f.LengthSquared() - dist * dist, disc = b * b - 4 * dd * c;
            if (dd < 1e-9f || disc < 0)
            {
                continue;
            }
            float t = (-b + MathF.Sqrt(disc)) / (2 * dd);
            // A millimetre of slack at the end: float rounding over a whole ring of pieces otherwise
            // leaves the last one just past the end, and the ring one piece short.
            float slack = 1e-3f / MathF.Sqrt(dd);
            if (t >= tStart - 1e-6f && t <= 1 + slack)
            {
                t = MathF.Min(t, 1);
                return (a + d * t, seg, t);
            }
        }
        return null;
    }

    /// <summary>How much of the line is left at the end once no whole piece fits (end to end).</summary>
    public float SnapGap { get; private set; }
}
