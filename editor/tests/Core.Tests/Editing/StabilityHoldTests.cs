using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The two-sided hold of a piece: support from two contact points at least 100° apart (seen from above) averages them,
/// and only when that beats the best support so far.
/// </summary>
public class StabilityHoldTests
{
    [Fact]
    public void PointsOnOppositeSidesAverageTheirSupport()
    {
        var points = new List<(Vector3 P, float V)> { (new Vector3(1, 0, 0), 0.8f), (new Vector3(-1, 0, 0), 0.6f) };

        Assert.Equal(0.7f, Stability.HeldFromTwoSides(points, Vector3.Zero, 0.1f), 5);
    }

    [Fact]
    public void PointsLessThan100DegreesApartDoNotHold()
    {
        var points = new List<(Vector3 P, float V)> { (new Vector3(1, 0, 0), 0.8f), (new Vector3(0, 0, 1), 0.6f) };

        Assert.Equal(0.1f, Stability.HeldFromTwoSides(points, Vector3.Zero, 0.1f));
    }

    [Fact]
    public void AnAverageThatDoesNotBeatTheBestKeepsTheBest()
    {
        var points = new List<(Vector3 P, float V)> { (new Vector3(1, 0, 0), 0.8f), (new Vector3(-1, 0, 0), 0.6f) };

        Assert.Equal(0.9f, Stability.HeldFromTwoSides(points, Vector3.Zero, 0.9f));
    }
}
