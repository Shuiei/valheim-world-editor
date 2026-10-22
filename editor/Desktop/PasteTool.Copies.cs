using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>The copies of a paste (PasteTool.Copies.cs): where each copy is set down and its objects stand.</summary>
public sealed partial class PasteTool
{
    /// <summary>The step between two copies: the size of the copy plus the gap along the chosen direction.</summary>
    private (Vector2 D, float Up) Step()
    {
        var c = Clip!;
        if (Direction == Along.Up)
        {
            float y0 = c.Objects.Count > 0 ? c.Objects.Min(o => o.Dy) : 0, y1 = c.Objects.Count > 0 ? c.Objects.Max(o => o.Dy) : 0;
            return (Vector2.Zero, (c.Objects.Count > 0 ? MathF.Max(1, y1 - y0 + 1) : 2) + Gap);
        }
        var (w, h) = Extent(c);
        return Direction == Along.Width ? (new Vector2(w + Gap, 0), 0) : (new Vector2(0, h + Gap), 0);
    }

    /// <summary>
    /// How far a copy reaches east and north, in metres: its outline's size; a copy without one (a blueprint that has
    /// none), its grid's.
    /// </summary>
    /// <param name="c">The copy.</param>
    internal static (float W, float H) Extent(CopyData c)
    {
        float x0 = c.Poly.Count > 0 ? c.Poly.Min(p => p.X) : -(c.W - 1) / 2f, x1 = c.Poly.Count > 0 ? c.Poly.Max(p => p.X) : (c.W - 1) / 2f;
        float z0 = c.Poly.Count > 0 ? c.Poly.Min(p => p.Y) : -(c.H - 1) / 2f, z1 = c.Poly.Count > 0 ? c.Poly.Max(p => p.Y) : (c.H - 1) / 2f;
        return (x1 - x0, z1 - z0);
    }

    /// <summary>
    /// Copy k's anchor (grid point) and the height added to it. The anchor is moved (half a metre at most) so that
    /// the copy's ground points land on the ground's: the ground goes in a whole metre at a time, and the objects
    /// must stand on it as they stood on the original.
    /// </summary>
    public (Vector2 At, float Up) CopyAt(Vector2 at, int k)
    {
        var (d, up) = Step();
        var c = Clip!;
        var corner = Xf(new Vector2(-(c.W - 1) / 2f, -(c.H - 1) / 2f));
        var ca = at + Xf(d * k) + corner;
        return (new Vector2(MathF.Round(ca.X), MathF.Round(ca.Y)) - corner, up * k);
    }

    /// <summary>The number of copies, 1 to 50.</summary>
    public int Count => Math.Clamp(Copies, 1, 50);

    /// <summary>
    /// Each copy's anchor point on the grid, its lift (see CopyAt), and the height its anchored objects stand at.
    /// </summary>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    private IEnumerable<(Vector2 Ca, float Up, float Anchor)> CopyAnchors(Vector2 at, Func<Vector2, float> heightAt)
    {
        float baseH = heightAt(at) + Offset;
        for (int k = 0; k < Count; k++)
        {
            var (ca, up) = CopyAt(at, k);
            float anchor = (Direction == Along.Up ? baseH : heightAt(ca) + Offset) + up;
            yield return (ca, up, anchor);
        }
    }

    /// <summary>
    /// Where one object of a copy stands: its grid point and its height (following the ground as the paste leaves it, its
    /// copied ground included: GroundAfter; or at the anchor).
    /// </summary>
    /// <param name="o">The copied object.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="up">The copy's lift (see CopyAt).</param>
    /// <param name="anchor">The height the copy's anchored objects stand at.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    private (Vector2 P, float Y) ObjectSpot(CopyData.Obj o, Vector2 ca, float up, float anchor, Func<Vector2, float> heightAt)
    {
        var p = ca + Xf(new Vector2(o.Dx, o.Dz));
        // Stacked upwards, the ground is the first copy's, laid at the anchor without the lift.
        float y = o.Follow ? GroundAfter(Clip!, ca, anchor - up, p, heightAt(p)) + o.Dy + up : anchor + o.Dy;
        return (p, y);
    }
}
