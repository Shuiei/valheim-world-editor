namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The grounds the mask tests slope: a flat zone at 30 m with a step up.
/// </summary>
internal static class CliffGround
{
    /// <summary>
    /// A ground of n by n grid points at 30 m, with every point from column <paramref name="fromColumn"/> east at 40 m: a
    /// 10 m cliff.
    /// </summary>
    /// <param name="n">The ground's width and height in grid points.</param>
    /// <param name="fromColumn">The first column of the higher ground.</param>
    internal static Ground Make(int n, int fromColumn)
    {
        var g = new Ground(n, n, 0, 0, 1);
        for (int p = 0; p < n * n; p++)
        {
            g.Base[p] = p % n >= fromColumn ? 40 : 30;
        }
        return g;
    }
}
