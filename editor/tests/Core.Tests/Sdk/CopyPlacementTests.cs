using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a pasted copy's places land (CopyPlacement): mirrored east to west, then turned counter-clockwise seen from
/// above, about the copy's new middle; yaws as the copy's objects turn.
/// </summary>
public class CopyPlacementTests
{
    private static void Near(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{actual} is not {expected}");

    [Fact]
    public void APlaceMovesWithTheMiddle()
    {
        var p = new CopyPlacement(new Vector3(100, 30, -50), 0, false);
        Near(new Vector3(102, 31, -47), p.Place(new Vector3(2, 1, 3)));
        Assert.Equal(45f, p.Yaw(45));
    }

    [Fact]
    public void ATurnGoesCounterClockwiseSeenFromAbove()
    {
        var p = new CopyPlacement(Vector3.Zero, 90, false);
        // East turns north, north turns west.
        Near(new Vector3(0, 0, 1), p.Place(Vector3.UnitX));
        Near(new Vector3(-1, 0, 0), p.Place(Vector3.UnitZ));
        Assert.Equal(-90f, p.Yaw(0));
    }

    [Fact]
    public void AMirrorFlipsEastToWestBeforeTheTurn()
    {
        var p = new CopyPlacement(new Vector3(10, 0, 10), 90, true);
        Near(new Vector3(10, 0, 9), p.Place(Vector3.UnitX));
        Near(new Vector3(9, 2, 10), p.Place(new Vector3(0, 2, 1)));
        Assert.Equal(-120f, p.Yaw(30));
    }
}
