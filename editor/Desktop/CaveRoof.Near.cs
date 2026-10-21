using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The cave's near field (CaveRoof.Build): the grid points by the line, and the line's ceiling and depth there.
/// </summary>
public static partial class CaveRoof
{
    /// <summary>
    /// Each grid point within reach of the cave's line: its distance from the line, the ceiling above it, and whether
    /// the stretch there is roofed (deep enough for the headroom).
    /// </summary>
    /// <param name="path">The Path tool: its headroom.</param>
    /// <param name="c">The cave's plan.</param>
    /// <param name="w">The grid's width.</param>
    /// <param name="h">The grid's height.</param>
    /// <param name="reach">How far from the line a point is still kept.</param>
    internal static Dictionary<int, (float Dist, float Ceiling, bool Roofed)> NearField(PathTool path, PathTool.CavePlan c, int w, int h, float reach)
    {
        var near = new Dictionary<int, (float Dist, float Ceiling, bool Roofed)>();
        int gx0 = Math.Max(0, (int)MathF.Floor(c.Curve.Min(q => q.P.X) - reach)), gx1 = Math.Min(w - 1, (int)MathF.Ceiling(c.Curve.Max(q => q.P.X) + reach));
        int gz0 = Math.Max(0, (int)MathF.Floor(c.Curve.Min(q => q.P.Y) - reach)), gz1 = Math.Min(h - 1, (int)MathF.Ceiling(c.Curve.Max(q => q.P.Y) + reach));
        for (int z = gz0; z <= gz1; z++)
        {
            for (int x = gx0; x <= gx1; x++)
            {
                var (best, ceiling, depth) = Nearest(path, c, x, z);
                if (best <= reach)
                {
                    near[z * w + x] = (best, ceiling, depth >= path.Headroom + 1.5f);
                }
            }
        }
        return near;
    }

    /// <summary>
    /// The distance from a grid point to the cave's line, and the ceiling and the depth of the line at its nearest
    /// stretch (the ceiling is the floor there plus the headroom).
    /// </summary>
    /// <param name="path">The Path tool: its headroom.</param>
    /// <param name="c">The cave's plan.</param>
    /// <param name="x">The grid point's x.</param>
    /// <param name="z">The grid point's z.</param>
    internal static (float Dist, float Ceiling, float Depth) Nearest(PathTool path, PathTool.CavePlan c, int x, int z)
    {
        int n = c.Curve.Count;
        float best = float.MaxValue, ceiling = 0, depth = 0;
        for (int i = 1; i < n; i++)
        {
            Vector2 a = c.Curve[i - 1].P, d = c.Curve[i].P - a;
            float t = Math.Clamp(((x - a.X) * d.X + (z - a.Y) * d.Y) / MathF.Max(1e-9f, d.LengthSquared()), 0, 1);
            float dist = Vector2.Distance(new Vector2(x, z), a + d * t);
            if (dist < best)
            {
                best = dist;
                ceiling = c.Floor[i - 1] + (c.Floor[i] - c.Floor[i - 1]) * t + path.Headroom;
                depth = c.Depth[i - 1] + (c.Depth[i] - c.Depth[i - 1]) * t;
            }
        }
        return (best, ceiling, depth);
    }
}
