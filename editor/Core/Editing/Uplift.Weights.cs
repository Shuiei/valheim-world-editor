using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>
    /// For each disc, the points it reaches and how much of its height each ends up with: the game's
    /// Heightmap.LevelTerrain (the point takes the disc's height) then SmoothTerrain2 (the point moves a
    /// share 1 - (d/r)^3 towards it), discs in TerrainModifier.SortByModifiers order.
    /// </summary>
    private static List<(int P, float W)>[] Weights(Block block, List<Vector3> discs)
    {
        var at = new Dictionary<int, List<(int K, float W)>>();
        foreach (int k in Enumerable.Range(0, discs.Count).OrderBy(k => (discs[k] + DiscOffset).LengthSquared()))
        {
            block.Disc(discs[k], DiscReach, (p, d) =>
            {
                if (d <= DiscRadius)
                {
                    at[p] = [(k, 1f)];
                    return;
                }
                float u = d / DiscReach, t = 1f - u * u * u;
                if (!at.TryGetValue(p, out var list))
                {
                    at[p] = [(k, t)];
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = (list[i].K, list[i].W * (1 - t));
                }
                list.Add((k, t));
            });
        }
        var columns = new List<(int, float)>[discs.Count];
        for (int k = 0; k < discs.Count; k++)
        {
            columns[k] = [];
        }
        foreach (var (p, list) in at)
        {
            foreach (var (k, w) in list)
            {
                if (w > 1e-4f)
                {
                    columns[k].Add((p, w));
                }
            }
        }
        return columns;
    }

    /// <summary>The points within r (a square, in points) of one whose value is on.</summary>
    private static bool[] Dilate(Block block, float[] values, Func<float, bool> on, int r)
    {
        bool[] src = new bool[block.N];
        for (int p = 0; p < block.N; p++)
        {
            src[p] = on(values[p]);
        }
        // Separable square dilation (rows, then columns): every point within r of one that is on.
        bool[] rows = new bool[block.N], res = new bool[block.N];
        block.Sweep(src, rows, r, false);
        block.Sweep(rows, res, r, true);
        return res;
    }
}
