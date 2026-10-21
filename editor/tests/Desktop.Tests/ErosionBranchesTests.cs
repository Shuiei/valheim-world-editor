using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Erosion's window: too small to run, and the area's locked edge given no weight.
/// </summary>
public class ErosionBranchesTests
{
    [Fact]
    public void AWindowUnderThreePointsDoesNotRun()
    {
        var g = TestScenes.Flat(1).Ground;
        bool ran = false, clamped = false;
        var rect = Erosion.Run(g, 5, 5, 6, 20, (_, _, _) => 1, (_, _, _, _) => ran = true, [], ref clamped);
        Assert.Equal((5, 5, 6, 20), rect);
        Assert.False(ran);
    }

    [Fact]
    public void TheLockedEdgeHasNoWeight()
    {
        var g = TestScenes.Flat(1).Ground;
        float[]? weights = null;
        bool clamped = false;
        var rect = Erosion.Run(g, -4, -4, 6, 6, (_, _, _) => 1, (_, wt, _, _) => weights = (float[])wt.Clone(), [], ref clamped);
        Assert.Equal((0, 0, 6, 6), rect);
        Assert.NotNull(weights);
        // The first row and column are the area's edge; the rest may change.
        Assert.Equal(0, weights[0]);
        Assert.Equal(0, weights[3]);
        Assert.Equal(1, weights[1 * 7 + 1]);
    }
}
