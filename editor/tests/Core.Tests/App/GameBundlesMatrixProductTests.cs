using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The product of two row-major 4 by 4 matrices (the game's transforms as the editor reads them): the identity leaves a
/// matrix as it is, and the entries are rows of the first times columns of the second, so the order matters.
/// </summary>
public class GameBundlesMatrixProductTests
{
    private static readonly float[] Identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    [Fact]
    public void TheIdentityLeavesAMatrixAsItIs()
    {
        float[] m = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Assert.Equal(m, GameBundles.Matrix.Mul(Identity, m));
        Assert.Equal(m, GameBundles.Matrix.Mul(m, Identity));
    }

    [Fact]
    public void EachEntryIsARowOfTheFirstTimesAColumnOfTheSecond()
    {
        float[] a = [1, 2, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        float[] b = [1, 0, 0, 0, 3, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        // Row 0 of a times b: 1 * row 0 of b + 2 * row 1 of b = [7, 2, 0, 0].
        float[] ab = [7, 2, 0, 0, 3, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        Assert.Equal(ab, GameBundles.Matrix.Mul(a, b));
        // Row 1 of b times a: 3 * row 0 of a + 1 * row 1 of a = [3, 7, 0, 0].
        float[] ba = [1, 2, 0, 0, 3, 7, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        Assert.Equal(ba, GameBundles.Matrix.Mul(b, a));
    }
}
