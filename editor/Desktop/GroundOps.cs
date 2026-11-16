namespace TerrainEditor.Desktop;

/// <summary>
/// What each ground action does to one grid point, written once. The brushes (<see cref="Sculpt"/>), the Area tool
/// and the Path tool only say where: which points, and how much each one counts (its weight, or its share of the way
/// for a brush held down). What then happens to a point is here, so Raise, Lower, Flatten, Smooth, Naturalize, Restore
/// and Paint mean the same thing under a brush, inside a selection and along a line. Erode works on a neighbourhood
/// at a time, not on a point: see <see cref="Erosion"/>.
/// </summary>
public static partial class GroundOps
{
    /// <summary>Raise and Lower: moves a point up (or down, below zero) by so many metres.</summary>
    /// <param name="g">The ground.</param>
    /// <param name="p">The grid point (index z * width + x).</param>
    /// <param name="metres">How far up, in metres; below zero: down.</param>
    /// <returns>Whether the point reached the game's ±8 m limit.</returns>
    public static bool Move(Ground g, int p, float metres) => g.SetHeight(p, g.HeightOf(p) + metres);

    /// <summary>
    /// Flatten, Smooth, Naturalize and a ramp: moves a point a share of the way towards a height, never past it.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="target">The height aimed at, in metres.</param>
    /// <param name="k">The share of the way, 0 to 1.</param>
    /// <returns>Whether the point reached the game's ±8 m limit.</returns>
    public static bool Toward(Ground g, int p, float target, float k)
    {
        float h = g.HeightOf(p);
        return g.SetHeight(p, h + (target - h) * k);
    }

    /// <summary>
    /// Restore: takes a point a share of the way back to the ground as the world generated it, its edit and its No
    /// limit lift both (to the generated ground under saved No limit ground too: the next save takes its ground discs
    /// away), and removes its paint where the point counts for more than half.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="k">The share of the way back, 0 to 1; above 0.98 the point is back for good.</param>
    /// <param name="w">The point's weight in the brush, the selection or along the line, 0 to 1.</param>
    public static void Restore(Ground g, int p, float k, float w)
    {
        bool whole = k > 0.98f;
        if (g.Mod[p] != 0)
        {
            g.Level[p] *= 1 - k;
            g.Smooth[p] *= 1 - k;
            if (whole || MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
            {
                g.Level[p] = 0;
                g.Smooth[p] = 0;
                g.Mod[p] = 0;
            }
        }
        float bare = g.BareLift(p), lift = g.Lift[p] + (bare - g.Lift[p]) * k;
        g.Lift[p] = whole || MathF.Abs(lift - bare) < 0.01f ? bare : lift;
        if (g.PMod[p] != 0 && w > 0.5f)
        {
            g.PMod[p] = 0;
        }
    }

    /// <summary>Paint: moves a point's paint a share of the way towards a colour.</summary>
    /// <param name="g">The ground.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="colour">The colour (four weights, as <see cref="Brush.PaintOf"/> gives them).</param>
    /// <param name="k">The share of the way, 0 to 1.</param>
    public static void Paint(Ground g, int p, float[] colour, float k)
    {
        g.StartPaint(p);
        for (int c = 0; c < 4; c++)
        {
            g.Paint[p * 4 + c] += (colour[c] - g.Paint[p * 4 + c]) * k;
        }
    }
}
