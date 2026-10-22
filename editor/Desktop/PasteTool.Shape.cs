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
        int i = CopyPoint(c, ca, gx, gz);
        if (i < 0 || g.Locked(gx, gz))
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

    /// <summary>
    /// The copy's ground point that lands on a grid point (an index into its arrays), or -1 when the copy has no ground
    /// there (outside it, or not copied).
    /// </summary>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="gx">The grid point's x.</param>
    /// <param name="gz">The grid point's z.</param>
    private int CopyPoint(CopyData c, Vector2 ca, int gx, int gz)
    {
        var src = Inv(new Vector2(gx - ca.X, gz - ca.Y));
        int ix = (int)MathF.Round(src.X + (c.W - 1) / 2f), iz = (int)MathF.Round(src.Y + (c.H - 1) / 2f);
        return ix < 0 || iz < 0 || ix >= c.W || iz >= c.H || float.IsNaN(c.Rel[iz * c.W + ix]) ? -1 : iz * c.W + ix;
    }

    /// <summary>
    /// The Mask's weight at a grid point (x, z) as the paste applies it (null: no Mask), so that what is shown before
    /// the click stands where the paste puts it; the 3D view sets it from the session it shows.
    /// </summary>
    internal Func<int, int, float>? MaskAt { get; set; }

    /// <summary>
    /// The ground's height at a point once the copy's ground is pasted there (Ground on), as ShapeCell lays it on the
    /// nearest grid point, the Mask included (MaskAt): where an object that follows the ground stands after the paste.
    /// </summary>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="anchor">The height the copy's ground is laid at.</param>
    /// <param name="p">The point, on the grid.</param>
    /// <param name="h">The ground's height there before the paste.</param>
    internal float GroundAfter(CopyData c, Vector2 ca, float anchor, Vector2 p, float h)
    {
        int gx = (int)MathF.Round(p.X), gz = (int)MathF.Round(p.Y);
        int i = Ground ? CopyPoint(c, ca, gx, gz) : -1;
        float w = i < 0 ? 0 : c.Wt[i] * (MaskAt?.Invoke(gx, gz) ?? 1);
        return w <= 0 ? h : h + (anchor + c.Rel[i] - h) * w;
    }
}
