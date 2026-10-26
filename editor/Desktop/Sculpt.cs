
namespace TerrainEditor.Desktop;

/// <summary>Applies the sculpt and paint tools to the ground.</summary>
public static partial class Sculpt
{
    /// <summary>
    /// One frame of a stroke with the brush's middle at grid point (cx, cz), <c>dt</c> seconds after the last (counted
    /// as 0.1 at most). Returns the grid rectangle changed (to redraw), or null when no point changed.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="b">The brush.</param>
    /// <param name="s">The stroke.</param>
    /// <param name="cx">The brush middle's grid x.</param>
    /// <param name="cz">The brush middle's grid z.</param>
    /// <param name="dt">Seconds since the last frame.</param>
    /// <param name="mask">How much each point may change (1 everywhere when null).</param>
    public static (int X0, int Z0, int X1, int Z1)? Apply(Ground g, Brush b, Stroke s, float cx, float cz, float dt, Func<int, float>? mask = null)
    {
        float reach = b.Reach;
        int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
        int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
        if (x0 > x1 || z0 > z1)
        {
            return null;
        }
        float rate = b.Strength * MathF.Min(dt, 0.1f);
        // Erosion works on the neighbourhood of every point (Erosion).
        if (s.Tool == BrushTool.Erode)
        {
            return ErodeFrame(g, b, s, cx, cz, reach, rate, mask);
        }
        // Smoothing reads the heights from before this frame: the brush rectangle and a point around it, row by row.
        var frame = new Frame(rate, s.Tool == BrushTool.Smooth ? TakeSnapshot(g, x0, x1, z0, z1) : null, x1 - x0 + 3, x0, z0, Brush.PaintOf(s.Tool));
        float ox = g.OriginX, oz = g.OriginZ;
        bool any = false;
        foreach (var (gx, gz) in Unlocked(g, x0, z0, x1, z1))
        {
            float wb = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz);
            if (wb <= 0)
            {
                continue;
            }
            int p = gz * g.W + gx;
            float w = wb * (mask?.Invoke(p) ?? 1);
            if (w <= 0)
            {
                continue;
            }
            s.Touched.Add(p);
            any = true;
            ApplyPoint(g, b, s, frame, gx, gz, p, w);
        }
        // Nothing changed (locked ground, the mask or the brush's shape left it all out): nothing to redraw.
        return any ? (x0 - 1, z0 - 1, x1 + 1, z1 + 1) : null;
    }

    /// <summary>One frame of an Erode stroke: the erosion runs over the neighbourhood of the brush and reports the rectangle it changed.</summary>
    internal static (int X0, int Z0, int X1, int Z1) ErodeFrame(Ground g, Brush b, Stroke s, float cx, float cz, float reach, float rate, Func<int, float>? mask)
    {
        float r = b.Radius, er = reach + 2, talus = Erosion.Talus(b.RestAngle);
        float ox = g.OriginX, oz = g.OriginZ;
        bool clamped = false;
        var (ex0, ez0, ex1, ez1) = Erosion.Run(g, (int)MathF.Floor(cx - er), (int)MathF.Floor(cz - er), (int)MathF.Ceiling(cx + er), (int)MathF.Ceiling(cz + er),
            (gx, gz, p) => b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1),
            (h, wt, w, d) =>
            {
                if (b.ErodeWater)
                {
                    Erosion.Hydraulic(h, wt, w, d, (int)MathF.Ceiling(r * r * rate * 6), 1, b.Random);
                }
                else
                {
                    Erosion.Thermal(h, wt, w, d, talus, MathF.Min(1, rate * 12), 2);
                }
            }, s.Touched, ref clamped);
        s.Clamped |= clamped;
        return (ex0 - 1, ez0 - 1, ex1 + 1, ez1 + 1);
    }
}
