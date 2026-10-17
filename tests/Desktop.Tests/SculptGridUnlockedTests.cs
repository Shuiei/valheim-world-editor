using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The grid points a sculpt may change: the rectangle's points the ground does not lock, row by row, and nothing for a
/// rectangle that is empty or back to front.
/// </summary>
public sealed class SculptGridUnlockedTests
{
    /// <summary>The unlocked points of a rectangle are its inner points, row by row.</summary>
    [Fact]
    public void TheInnerPointsOfARectangleAreGivenRowByRow()
    {
        // In a 5 by 5 ground the edge points are locked, so a 3 by 3 rectangle from 0 has four unlocked points.
        var g = new Ground(5, 5, 0, 0, 2);

        var points = Sculpt.Unlocked(g, 0, 0, 2, 2).ToList();

        Assert.Equal([(1, 1), (2, 1), (1, 2), (2, 2)], points);
    }

    /// <summary>A rectangle that runs back to front has no points.</summary>
    [Fact]
    public void ARectangleRunningBackToFrontHasNoPoints()
    {
        var g = new Ground(5, 5, 0, 0, 2);

        Assert.Empty(Sculpt.Unlocked(g, 2, 2, 1, 1));
    }
}
