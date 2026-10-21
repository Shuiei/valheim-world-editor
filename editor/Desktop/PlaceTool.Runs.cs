using System.Numerics;

namespace TerrainEditor.Desktop;

// The runs of the line: the points picked, split where the line breaks.
public sealed partial class PlaceTool
{
    /// <summary>
    /// The line as runs of points: one run, or a rectangle's four sides each on their own when placing end to end (so
    /// its corners stay exact).
    /// </summary>
    public List<List<Vector2>> LineRuns()
    {
        if (LineShape == LineShapes.Points)
        {
            var c = LineCurve().Select(q => q.P).ToList();
            if (Loop && c.Count >= 3)
            {
                c.Add(c[0]);
            }
            return c.Count >= 2 ? new() { c } : new();
        }
        if (FigA is not { } a || FigB is not { } fb)
        {
            return [];
        }
        var b = SnapFigure(a, fb);
        if (LineShape == LineShapes.Rect)
        {
            if (MathF.Abs(b.X - a.X) < 0.5f || MathF.Abs(b.Y - a.Y) < 0.5f)
            {
                return [];
            }
            var c = new List<Vector2> { a, new(b.X, a.Y), b, new(a.X, b.Y) };
            return EndToEnd ? [.. Enumerable.Range(0, 4).Select(i => new List<Vector2> { c[i], c[(i + 1) % 4] })] : new() { c.Append(c[0]).ToList() };
        }
        float r = Vector2.Distance(a, b);
        if (r < 0.5f)
        {
            return [];
        }
        // End to end: one point per piece, so the pieces meet at the corners of a regular polygon.
        var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
        int n = e != null ? Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len)) : Math.Max(16, (int)MathF.Ceiling(MathF.Tau * r / 1.5f));
        float t0 = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        return [[.. Enumerable.Range(0, n + 1).Select(i => a + new Vector2(MathF.Cos(t0 + i / (float)n * MathF.Tau), MathF.Sin(t0 + i / (float)n * MathF.Tau)) * r)]];
    }
}
