namespace TerrainEditor.Desktop;

/// <summary>
/// The heights of a box of the ground as they were before an action (or before a brush's frame), for the actions that
/// read the ground round a point: Smooth and Naturalize. Every tool reads them the same way
/// (<see cref="GroundOps.Average"/>), so a point never moves towards heights the action itself just changed.
/// </summary>
public sealed class HeightsBefore
{
    /// <summary>The ground the heights were read from.</summary>
    private readonly Ground _g;
    /// <summary>The heights, row by row over the box.</summary>
    private readonly float[] _heights;
    /// <summary>The box's west and south grid points, and its width in grid points.</summary>
    private readonly int _x0, _z0, _w;
    /// <summary>A kept grid point's height, by its index (<see cref="At"/>, made once).</summary>
    private readonly Func<int, float> _at;

    /// <summary>Keeps the heights of a box.</summary>
    private HeightsBefore(Ground g, float[] heights, int x0, int z0, int w)
    {
        _g = g;
        _heights = heights;
        _x0 = x0;
        _z0 = z0;
        _w = w;
        _at = At;
    }

    /// <summary>How many heights are kept (none for an action that reads no ground round its points).</summary>
    public int Count => _heights.Length;

    /// <summary>No heights: for the actions that read none (Flatten, Raise, Lower, Restore, Paint).</summary>
    /// <param name="g">The ground.</param>
    /// <returns>An empty snapshot.</returns>
    public static HeightsBefore None(Ground g) => new(g, [], 0, 0, 0);

    /// <summary>Reads the heights of a box of the grid and of the reach round it (the part inside the ground).</summary>
    /// <param name="g">The ground.</param>
    /// <param name="x0">The box's west grid point.</param>
    /// <param name="z0">Its south grid point.</param>
    /// <param name="x1">Its east grid point (included).</param>
    /// <param name="z1">Its north grid point (included).</param>
    /// <param name="reach">How far round the box the action reads, in grid points; below zero: nothing is read.</param>
    /// <returns>The heights taken.</returns>
    public static HeightsBefore Take(Ground g, int x0, int z0, int x1, int z1, int reach)
    {
        if (reach < 0)
        {
            return None(g);
        }
        int bx0 = Math.Max(0, x0 - reach), bz0 = Math.Max(0, z0 - reach);
        int bx1 = Math.Min(g.W - 1, x1 + reach), bz1 = Math.Min(g.H - 1, z1 + reach);
        if (bx0 > bx1 || bz0 > bz1)
        {
            return None(g);
        }
        int bw = bx1 - bx0 + 1;
        float[] heights = new float[bw * (bz1 - bz0 + 1)];
        for (int z = bz0; z <= bz1; z++)
        {
            for (int x = bx0; x <= bx1; x++)
            {
                heights[(z - bz0) * bw + x - bx0] = g.HeightOf(z * g.W + x);
            }
        }
        return new HeightsBefore(g, heights, bx0, bz0, bw);
    }

    /// <summary>A grid point's height as it was (the point must be in the box or the reach round it).</summary>
    /// <param name="p">The grid point (index z * width + x).</param>
    /// <returns>Its height, in metres.</returns>
    public float At(int p) => _heights[(p / _g.W - _z0) * _w + p % _g.W - _x0];

    /// <summary>The average of the heights within r grid points of a point, as they were (the one kernel of Smooth and Naturalize).</summary>
    /// <param name="p">The grid point.</param>
    /// <param name="r">The reach, in grid points.</param>
    /// <returns>The average, in metres.</returns>
    public float Average(int p, int r) => GroundOps.Average(_g.W, _g.H, p, r, _at);
}
