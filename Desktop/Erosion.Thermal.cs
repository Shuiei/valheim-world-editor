namespace TerrainEditor.Desktop;

/// <summary>
/// Thermal erosion, like the web editor's: material slides from steep points down to their lower neighbours.
/// </summary>
public static partial class Erosion
{
    /// <summary>
    /// The eight neighbours of a grid point.
    /// </summary>
    private static readonly (int Dx, int Dz)[] N8 = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// Thermal erosion: where the ground is steeper than its resting angle, material slides down to its lower
    /// neighbours. The window's edge points only take material, they never give it.
    /// </summary>
    /// <param name="h">Heights of a w × d window (row by row), changed in place.</param>
    /// <param name="wt">How much each point may change (0..1; 0 leaves it alone).</param>
    /// <param name="w">The window's width in points.</param>
    /// <param name="d">Its depth in points.</param>
    /// <param name="talus">Steepest slope (rise per metre) that stays put; see Talus.</param>
    /// <param name="amount">Share of the excess moved per pass.</param>
    /// <param name="passes">How many times it runs over the window.</param>
    public static void Thermal(float[] h, float[] wt, int w, int d, float talus, float amount, int passes)
    {
        float[] delta = new float[h.Length];
        var ds = new List<(int J, float Diff)>(8);
        for (int p = 0; p < passes; p++)
        {
            Array.Clear(delta);
            for (int z = 1; z < d - 1; z++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int i = z * w + x;
                    float k = wt[i];
                    if (k <= 0)
                    {
                        continue;
                    }
                    float total = 0, maxD = 0;
                    ds.Clear();
                    foreach (var (dx, dz) in N8)
                    {
                        int j = (z + dz) * w + x + dx;
                        float dist = dx != 0 && dz != 0 ? MathF.Sqrt(2) : 1, diff = h[i] - h[j] - talus * dist;
                        if (diff > 0 && wt[j] > 0)
                        {
                            ds.Add((j, diff));
                            total += diff;
                            maxD = MathF.Max(maxD, diff);
                        }
                    }
                    if (total == 0)
                    {
                        continue;
                    }
                    // Move half of the largest excess (so the slope settles instead of swinging), shared out.
                    float move = maxD * 0.5f * amount * k;
                    delta[i] -= move;
                    foreach (var (j, diff) in ds)
                    {
                        delta[j] += move * diff / total;
                    }
                }
            }
            for (int i = 0; i < h.Length; i++)
            {
                h[i] += delta[i];
            }
        }
    }
}
