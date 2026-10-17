using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>Shaping the ground under a paste (PasteTool.Shape.cs): the copy's height and paint, point by point.</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Shapes the ground of one copy: each grid point of its rectangle (x0 to x1, z0 to z1) takes the copy's height and paint
    /// (see <see cref="ShapeCell"/>).
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="anchor">The height the copy's ground is laid at.</param>
    /// <param name="x0">The rectangle's first grid x.</param>
    /// <param name="x1">The rectangle's last grid x.</param>
    /// <param name="z0">The rectangle's first grid z.</param>
    /// <param name="z1">The rectangle's last grid z.</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <param name="shaped">The points given the copy's ground in full (weight 1); added to.</param>
    /// <param name="touched">The points changed; added to.</param>
    internal void ShapeGround(Ground g, CopyData c, Vector2 ca, float anchor, int x0, int x1, int z0, int z1, Func<int, float>? mask, HashSet<int> shaped, List<int> touched)
    {
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                ShapeCell(g, c, ca, anchor, gx, gz, mask, shaped, touched);
            }
        }
    }

    /// <summary>
    /// Shapes one grid point of the ground from the copy: the copy's height (blended by its weight, 1 under the building)
    /// and its paint, where the copy has any. A point the copy does not cover, or that is locked, is left as it is.
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="anchor">The height the copy's ground is laid at.</param>
    /// <param name="gx">The grid point's x.</param>
    /// <param name="gz">The grid point's z.</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <param name="shaped">The points given the copy's ground in full (weight 1); added to.</param>
    /// <param name="touched">The points changed; added to.</param>
    internal void ShapeCell(Ground g, CopyData c, Vector2 ca, float anchor, int gx, int gz, Func<int, float>? mask, HashSet<int> shaped, List<int> touched)
    {
        var src = Inv(new Vector2(gx - ca.X, gz - ca.Y));
        int ix = (int)MathF.Round(src.X + (c.W - 1) / 2f), iz = (int)MathF.Round(src.Y + (c.H - 1) / 2f);
        if (ix < 0 || iz < 0 || ix >= c.W || iz >= c.H)
        {
            return;
        }
        int i = iz * c.W + ix;
        if (float.IsNaN(c.Rel[i]) || g.Locked(gx, gz))
        {
            return;
        }
        int p = gz * g.W + gx;
        float w = c.Wt[i] * (mask?.Invoke(p) ?? 1);
        if (w <= 0)
        {
            return;
        }
        float h = g.HeightOf(p);
        g.SetHeight(p, h + (anchor + c.Rel[i] - h) * w);
        if (w >= 0.999f)
        {
            shaped.Add(p);
        }
        if (c.Pnt[i * 4] >= 0)
        {
            g.StartPaint(p);
            for (int ch = 0; ch < 4; ch++)
            {
                g.Paint[p * 4 + ch] += (c.Pnt[i * 4 + ch] - g.Paint[p * 4 + ch]) * w;
            }
        }
        touched.Add(p);
    }
}
