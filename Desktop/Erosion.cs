namespace TerrainEditor.Desktop;

/// <summary>
/// Erosion, like the web editor's (editor/erosion.js; VoxelSniper's erode brush, Axiom's erosion): ground that weathers
/// like real ground. Thermal: where the ground is steeper than its resting angle, material slides down to its lower
/// neighbours (screes, softened cliffs). Water: drops of rain run downhill, dig where they speed up and leave what they
/// carry where they slow down (gullies, fans, smooth valleys). Both work on a local copy of the heights, written back
/// through SetHeight (so the ±8 m limit applies).
/// </summary>
public static partial class Erosion
{
    /// <summary>
    /// Copies the heights of a window of the ground, runs fn on them, and writes back what changed through SetHeight
    /// (so the ±8 m limit applies).
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="x0">The window's first grid x (clamped to the grid).</param>
    /// <param name="z0">Its first grid z.</param>
    /// <param name="x1">Its last grid x (included).</param>
    /// <param name="z1">Its last grid z (included).</param>
    /// <param name="weightOf">How much the point at (grid x, grid z, index) may change, 0..1; locked points get
    /// 0.</param>
    /// <param name="fn">The erosion: heights, weights, width, depth (as Thermal and Hydraulic take them).</param>
    /// <param name="touched">Gets the indices of the points changed.</param>
    /// <param name="clamped">Set when a point reached the ±8 m limit; left as it was otherwise.</param>
    /// <returns>The window, clamped to the grid; nothing runs when it is less than 3 points wide or deep.</returns>
    public static (int X0, int Z0, int X1, int Z1) Run(Ground g, int x0, int z0, int x1, int z1, Func<int, int, int, float> weightOf,
        Action<float[], float[], int, int> fn, ICollection<int> touched, ref bool clamped)
    {
        x0 = Math.Max(0, x0);
        z0 = Math.Max(0, z0);
        x1 = Math.Min(g.W - 1, x1);
        z1 = Math.Min(g.H - 1, z1);
        int w = x1 - x0 + 1, d = z1 - z0 + 1;
        if (w < 3 || d < 3)
        {
            return (x0, z0, x1, z1);
        }
        float[] h = new float[w * d];
        float[] wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x0 + x, gz = z0 + z, p = gz * g.W + gx;
                h[z * w + x] = g.HeightOf(p);
                wt[z * w + x] = g.Locked(gx, gz) ? 0 : weightOf(gx, gz, p);
            }
        }
        float[] before = (float[])h.Clone();
        fn(h, wt, w, d);
        for (int i = 0; i < h.Length; i++)
        {
            if (MathF.Abs(h[i] - before[i]) < 1e-5f)
            {
                continue;
            }
            int p = (z0 + i / w) * g.W + x0 + i % w;
            clamped |= g.SetHeight(p, h[i]);
            touched.Add(p);
        }
        return (x0, z0, x1, z1);
    }

    /// <summary>
    /// A resting angle in degrees as the slope Thermal takes (rise per metre).
    /// </summary>
    public static float Talus(float degrees) => MathF.Tan(degrees * MathF.PI / 180);

    /// <summary>
    /// The Area tool's Erode: both kinds over the whole selection, in one go (thermal, rain, then thermal again to
    /// settle).
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="a">The selection and how much each of its points may change.</param>
    /// <param name="restAngle">The resting angle in degrees.</param>
    /// <param name="rand">Where the drops fall.</param>
    /// <param name="touched">Gets the indices of the points changed.</param>
    /// <returns>
    /// The grid rectangle worked on (the selection and one point around it), and whether a point reached the game's
    /// ±8 m limit.
    /// </returns>
    public static ((int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Area(Ground g, AreaTool.Weights a, float restAngle, Random rand, ICollection<int> touched)
    {
        var weights = a.Cells.ToDictionary(c => c.G, c => c.W);
        bool clamped = false;
        float talus = Talus(restAngle);
        var rect = Run(g, a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1, (_, _, p) => weights.GetValueOrDefault(p), (h, wt, w, d) =>
        {
            Thermal(h, wt, w, d, talus, 0.8f, 12);
            Hydraulic(h, wt, w, d, (int)MathF.Ceiling(w * d * 0.6f), 1, rand);
            Thermal(h, wt, w, d, talus, 0.5f, 4);
        }, touched, ref clamped);
        return (rect, clamped);
    }
}
