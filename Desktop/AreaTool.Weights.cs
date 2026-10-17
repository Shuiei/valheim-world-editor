using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>The grid points an action works on.</summary>
    /// <param name="Poly">The selection's outline.</param>
    /// <param name="X0">The box around it (grid points, one more all round, clamped to the block): west.</param>
    /// <param name="Z0">South edge of that box.</param>
    /// <param name="X1">East edge of that box.</param>
    /// <param name="Z1">North edge of that box.</param>
    /// <param name="Cells">The grid points (index z * width + x) with their weights, 0 to 1.</param>
    public sealed record Weights(List<Vector2> Poly, int X0, int Z0, int X1, int Z1, List<(int G, float W)> Cells);

    /// <summary>
    /// The selection's own box on the grid: the whole points round its outline, clamped to the block (every point the
    /// selection weighs is in it).
    /// </summary>
    /// <param name="w">The block's width in grid points.</param>
    /// <param name="h">The block's height in grid points.</param>
    /// <returns>West, south, east and north grid points; null while there is no selection.</returns>
    public (int X0, int Z0, int X1, int Z1)? GridBox(int w, int h)
    {
        if (Polygon() is not { } poly)
        {
            return null;
        }
        return (Math.Max(0, (int)MathF.Floor(poly.Min(p => p.X))), Math.Max(0, (int)MathF.Floor(poly.Min(p => p.Y))),
            Math.Min(w - 1, (int)MathF.Ceiling(poly.Max(p => p.X))), Math.Min(h - 1, (int)MathF.Ceiling(poly.Max(p => p.Y))));
    }

    /// <summary>
    /// The grid points inside the selection and their weights (the soft edge going inwards).
    /// </summary>
    /// <param name="w">The block's width in grid points.</param>
    /// <param name="h">The block's height in grid points.</param>
    /// <param name="mask">Also times the Mask (null: not).</param>
    /// <returns>The points, or null while there is no selection.</returns>
    public Weights? WeightsIn(int w, int h, Func<int, float>? mask = null)
    {
        if (Polygon() is not { } poly || GridBox(w, h) is not { } box)
        {
            return null;
        }
        int x0 = Math.Max(0, box.X0 - 1), x1 = Math.Min(w - 1, box.X1 + 1);
        int z0 = Math.Max(0, box.Z0 - 1), z1 = Math.Min(h - 1, box.Z1 + 1);
        var cells = new List<(int, float)>();
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                bool inPoly = Inside(poly, gx, gz);
                float wt;
                if (Soft > 0)
                {
                    if (!inPoly)
                    {
                        continue;
                    }
                    float t = MathF.Min(1, EdgeDistance(poly, gx, gz) / Soft);
                    wt = t * t * (3 - 2 * t);
                }
                else
                {
                    // No soft edge: the points the outline crosses (within half a metre) get a share, so the
                    // edge follows the outline straight instead of stepping from point to point.
                    float d = EdgeDistance(poly, gx, gz);
                    wt = inPoly ? MathF.Min(1, 0.5f + d) : 0.5f - d;
                }
                wt *= mask?.Invoke(gz * w + gx) ?? 1;
                if (wt > 0)
                {
                    cells.Add((gz * w + gx, wt));
                }
            }
        }
        return new Weights(poly, x0, z0, x1, z1, cells);
    }
}
