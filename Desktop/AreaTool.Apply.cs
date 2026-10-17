
namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>The action inside the selection, faded by the soft edge; locked points are left alone.</summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="b">The brush: Naturalize's bumps and size, Erode's rest angle and random.</param>
    /// <param name="act">The action.</param>
    /// <param name="height">Flatten's height, in metres.</param>
    /// <param name="amount">Raise and Lower's amount, in metres.</param>
    /// <param name="paint">The colour (four weights, as the paint brushes put down).</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <returns>The grid points changed, the box to redraw, and whether a point reached the game's ±8 m limit.</returns>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, GroundAction act, float height, float amount, float[] paint, Func<int, float>? mask = null)
    {
        var touched = new List<int>();
        if (WeightsIn(g.W, g.H, mask) is not { } a)
        {
            return (touched, (0, 0, 0, 0), false);
        }
        if (act == GroundAction.Erode)
        {
            var set = new HashSet<int>();
            var ((x0, z0, x1, z1), eroded) = Erosion.Area(g, a, b.RestAngle, b.Random, set);
            return (set.ToList(), (x0 - 1, z0 - 1, x1 + 1, z1 + 1), eroded);
        }
        int rNat = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
        // The heights before the action, for Smooth and Naturalize's blur only: over the selection's box and the
        // blur's reach round it (every point a blur reads).
        int reach = act == GroundAction.Smooth ? 3 : act == GroundAction.Natural ? rNat : -1;
        var change = new CellChange(g, b, act, height, amount, paint, HeightSnapshot.Take(g, a, reach), rNat);
        bool clamped = false;
        foreach (var (p, w) in a.Cells)
        {
            if (g.Locked(p % g.W, p / g.W))
            {
                continue;
            }
            touched.Add(p);
            clamped |= change.Change(p, w);
        }
        return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1), clamped);
    }
}
