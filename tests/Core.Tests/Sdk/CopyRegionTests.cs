using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>What a copy takes (CopyRegion): the points of the ground inside its outline, whatever its shape.</summary>
public class CopyRegionTests
{
    [Fact]
    public void OnlyPointsInsideTheOutlineAreIn()
    {
        // An L: the square 0..4 without its north-east quarter.
        var r = new CopyRegion(new Vector3(2, 0, 2), [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)]);
        Assert.True(r.Contains(1, 1));
        Assert.True(r.Contains(1, 3));
        Assert.True(r.Contains(3, 1));
        Assert.False(r.Contains(3, 3));
        Assert.False(r.Contains(-1, 1));
        Assert.Equal(new Vector3(2, 0, 2), r.Middle);
    }

    [Fact]
    public void NoOutlineHoldsNothing() => Assert.False(new CopyRegion(Vector3.Zero, []).Contains(0, 0));
}
