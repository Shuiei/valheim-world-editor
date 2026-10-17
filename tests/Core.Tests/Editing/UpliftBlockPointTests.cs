using System.Numerics;
using Xunit;
using static WorldEditor.Tests.UpliftBlockFixture;

namespace WorldEditor.Tests;

/// <summary>
/// A block of points: the point at a world position, the points near a position, the points of a disc, and the dilation
/// of a set of points along rows, on the one zone at (0, 0).
/// </summary>
public class UpliftBlockPointTests
{
    [Fact]
    public void TheOriginIsTheMiddlePointOfTheZone()
    {
        var block = SingleZone();

        Assert.Equal(Middle, block.At(Vector3.Zero));
    }

    [Fact]
    public void APositionOutsideTheZonesHasNoPoint()
    {
        var block = SingleZone();

        Assert.Null(block.At(new Vector3(1000, 0, 0)));
    }

    [Fact]
    public void APositionInsideAZoneIsNear()
    {
        var block = SingleZone();

        Assert.True(block.Near(Vector3.Zero, 0f));
    }

    [Fact]
    public void APositionFarFromTheZonesIsNotNear()
    {
        var block = SingleZone();

        Assert.False(block.Near(new Vector3(1000, 0, 0), 0f));
    }

    [Fact]
    public void ADiscOfRadiusOneCoversItsMiddleAndFourNeighbours()
    {
        var block = SingleZone();
        List<int> hits = [];

        block.Disc(Vector3.Zero, 1f, hits.Add);

        Assert.Equal(5, hits.Count);
        Assert.Contains(Middle, hits);
    }

    [Fact]
    public void ASweepWithNoReachMarksOnlyThePointItself()
    {
        var block = SingleZone();
        bool[] from = Flags(block, false);
        bool[] to = Flags(block, false);
        from[Middle] = true;

        block.Sweep(from, to, 0, columns: false);

        Assert.True(to[Middle]);
        Assert.Equal(1, to.Count(x => x));
    }

    [Fact]
    public void ASweepWithReachOneMarksTheThreePointsOfTheRow()
    {
        var block = SingleZone();
        bool[] from = Flags(block, false);
        bool[] to = Flags(block, false);
        from[Middle] = true;

        block.Sweep(from, to, 1, columns: false);

        Assert.True(to[Middle - 1]);
        Assert.True(to[Middle]);
        Assert.True(to[Middle + 1]);
        Assert.Equal(3, to.Count(x => x));
    }
}
