using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// The nearest points of the line to the grid points of a rectangle, row by row (x fastest).
    /// </summary>
    /// <param name="Dist">Distance to the line.</param>
    /// <param name="Seg">The nearest segment, by the index of its end point in the line; 0 where no segment is within
    /// reach.</param>
    /// <param name="T">How far along that segment, 0 to 1.</param>
    internal sealed record Nearest(float[] Dist, int[] Seg, float[] T);

    /// <summary>
    /// A segment of the dense line: its start, its run to the end, and its length squared (at least 1e-9, so a point
    /// segment still projects).
    /// </summary>
    /// <param name="curve">The dense line.</param>
    /// <param name="i">The index of the segment's end point (1 or more).</param>
    internal static (Vector2 A, Vector2 D, float L2) Segment(List<(Vector2 P, int Seg)> curve, int i)
    {
        Vector2 a = curve[i - 1].P, d = curve[i].P - a;
        return (a, d, MathF.Max(d.LengthSquared(), 1e-9f));
    }

    /// <summary>
    /// For each grid point of the rectangle <paramref name="x0"/>..<paramref name="x1"/> by
    /// <paramref name="z0"/>..<paramref name="z1"/>, the nearest segment of the line within
    /// <paramref name="reach"/>. Each segment visits only the points of its box widened by the reach, in the line's
    /// order, so a point gets what testing every segment would give (ties to the earlier one) without the cost of
    /// every point times every segment.
    /// </summary>
    /// <param name="curve">The dense line (<see cref="Curve"/>).</param>
    /// <param name="x0">First column.</param>
    /// <param name="z0">First row.</param>
    /// <param name="x1">Last column.</param>
    /// <param name="z1">Last row.</param>
    /// <param name="reach">How far from a segment its points are looked at, metres.</param>
    internal static Nearest NearestOnLine(List<(Vector2 P, int Seg)> curve, int x0, int z0, int x1, int z1, float reach)
    {
        int w = Math.Max(0, x1 - x0 + 1), h = Math.Max(0, z1 - z0 + 1);
        var near = new Nearest(new float[w * h], new int[w * h], new float[w * h]);
        Array.Fill(near.Dist, float.MaxValue);
        for (int i = 1; i < curve.Count; i++)
        {
            var (a, d, l2) = Segment(curve, i);
            Vector2 b = curve[i].P;
            int sx0 = Math.Max(x0, (int)MathF.Floor(MathF.Min(a.X, b.X) - reach)), sx1 = Math.Min(x1, (int)MathF.Ceiling(MathF.Max(a.X, b.X) + reach));
            int sz0 = Math.Max(z0, (int)MathF.Floor(MathF.Min(a.Y, b.Y) - reach)), sz1 = Math.Min(z1, (int)MathF.Ceiling(MathF.Max(a.Y, b.Y) + reach));
            for (int gz = sz0; gz <= sz1; gz++)
            {
                for (int gx = sx0; gx <= sx1; gx++)
                {
                    float t = Math.Clamp(((gx - a.X) * d.X + (gz - a.Y) * d.Y) / l2, 0, 1);
                    float dist = Vector2.Distance(new Vector2(gx, gz), a + d * t);
                    int k = (gz - z0) * w + (gx - x0);
                    if (dist <= reach && dist < near.Dist[k])
                    {
                        near.Dist[k] = dist;
                        near.Seg[k] = i;
                        near.T[k] = t;
                    }
                }
            }
        }
        return near;
    }
}
