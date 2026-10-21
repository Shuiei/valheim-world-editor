using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The verdict per piece: full support on a piece gives the blue colour value, a broken piece shows red, and a piece the
/// game does not check is free.
/// </summary>
public class StabilityVerdictTests
{
    [Fact]
    public void APieceAtItsFullSupportIsBlue()
    {
        var r = Stability.Verdict([100f], [new Stability.Data(0, true, Vector3.Zero, [])], []);

        Assert.Equal(-1f, r.Colour[0]);
        Assert.False(r.Breaks[0]);
        Assert.False(r.Free[0]);
    }

    [Fact]
    public void APieceThatFallsIsRedAndBreaks()
    {
        var r = Stability.Verdict([50f], [new Stability.Data(0, true, Vector3.Zero, [])], [0]);

        Assert.True(r.Breaks[0]);
        Assert.Equal(0f, r.Colour[0]);
    }

    [Fact]
    public void APieceWithNoDataIsFree()
    {
        var r = Stability.Verdict([0f], [null], []);

        Assert.True(r.Free[0]);
        Assert.Equal(0f, r.Colour[0]);
    }
}
