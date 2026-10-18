using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>Whether a paste fits the ground (PasteTool.Fits.cs).</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Whether a paste at grid point at, every copy of it, lies inside a ground of w × h points (its edge
    /// points excepted): past the open area nothing could shape the ground or clear the site under it.
    /// </summary>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="w">The ground's points across.</param>
    /// <param name="h">The ground's points down.</param>
    public bool Fits(Vector2 at, int w, int h)
    {
        if (Clip is not { } c)
        {
            return false;
        }
        for (int k = 0; k < Count; k++)
        {
            var (ca, _) = CopyAt(at, k);
            foreach (var p in c.Poly.Concat(c.Objects.Select(o => new Vector2(o.Dx, o.Dz))).Select(d => ca + Xf(d)))
            {
                if (p.X < 1 || p.Y < 1 || p.X > w - 2 || p.Y > h - 2)
                {
                    return false;
                }
            }
        }
        return true;
    }
}
