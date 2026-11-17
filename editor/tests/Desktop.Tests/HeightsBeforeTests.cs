using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The heights taken before an action that reads the ground round its points (Smooth and Naturalize, of a brush, the
/// Area tool or the Path tool): the box and the reach round it, and the average of the points within a reach that the
/// ground has.
/// </summary>
public class HeightsBeforeTests
{
    /// <summary>
    /// A flat 129 × 129 ground at 30 m.
    /// </summary>
    private static Ground Flat()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        return g;
    }

    [Fact]
    public void TakeReadsTheBoxWithTheReachRoundIt()
    {
        var g = Flat();
        g.SetHeight(8 * g.W + 8, 31f);
        var snap = HeightsBefore.Take(g, 10, 10, 10, 10, 2);

        // The 5 × 5 box from 8 to 12.
        Assert.Equal(25, snap.Count);
        Assert.Equal(31f, snap.At(8 * g.W + 8), 3);
        Assert.Equal(30f, snap.At(12 * g.W + 12), 3);
    }

    [Fact]
    public void TheHeightsAreTheOnesFromBeforeLaterChanges()
    {
        var g = Flat();
        int p = 10 * g.W + 10;
        var snap = HeightsBefore.Take(g, 10, 10, 10, 10, 0);
        g.SetHeight(p, 35f);
        Assert.Equal(30f, snap.At(p), 3);
    }

    [Fact]
    public void TakeWithNoReachTakesNothing()
    {
        Assert.Equal(0, HeightsBefore.Take(Flat(), 10, 10, 10, 10, -1).Count);
        Assert.Equal(0, HeightsBefore.None(Flat()).Count);
        // A box turned inside out (a line off the ground) holds nothing either.
        Assert.Equal(0, HeightsBefore.Take(Flat(), 5, 5, 4, 4, 0).Count);
    }

    [Fact]
    public void AverageIsTheMeanOfTheHeightsWithinItsReach()
    {
        var g = Flat();
        int center = 10 * g.W + 10;
        g.SetHeight(center, 33f);
        var snap = HeightsBefore.Take(g, 10, 10, 10, 10, 1);

        // The 3 × 3 round the middle: one point 3 m up and eight at 30 m.
        Assert.Equal((8 * 30f + 33f) / 9f, snap.Average(center, 1), 3);
        // No reach: the point itself.
        Assert.Equal(33f, snap.Average(center, 0), 3);
    }

    [Fact]
    public void AverageAtTheCornerCountsOnlyThePointsInside()
    {
        var g = Flat();
        g.SetHeight(1 * g.W + 1, 34f);
        var snap = HeightsBefore.Take(g, 0, 0, 0, 0, 1);

        // The corner has four points inside the ground: three at 30 m and the raised one.
        Assert.Equal((3 * 30f + 34f) / 4f, snap.Average(0, 1), 3);
    }
}
