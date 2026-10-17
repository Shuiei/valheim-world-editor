using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The two ends of a placed piece's footprint: its long side, at the middle of the footprint, and nothing for a piece
/// too short to have a side (PlaceTool.EndsFrom).
/// </summary>
public class PlaceToolEndsFromTests
{
    [Fact]
    public void AFootprintLongerAcrossXHasItsEndsOnTheMiddleOfZ()
    {
        var ends = PlaceTool.EndsFrom([0, 10], [5, 7], [0, 2]);

        Assert.NotNull(ends);
        Assert.Equal(new Vector3(0, 5, 1), ends.A);
        Assert.Equal(new Vector3(10, 5, 1), ends.B);
        Assert.Equal(10f, ends.Len);
        Assert.Equal(2f, ends.H);
    }

    [Fact]
    public void AFootprintLongerAcrossZHasItsEndsOnTheMiddleOfX()
    {
        var ends = PlaceTool.EndsFrom([0, 2], [0, 0], [0, 10]);

        Assert.NotNull(ends);
        Assert.Equal(new Vector3(1, 0, 0), ends.A);
        Assert.Equal(new Vector3(1, 0, 10), ends.B);
    }

    [Fact]
    public void AFootprintTooShortToHaveASideHasNoEnds()
    {
        Assert.Null(PlaceTool.EndsFrom([0, 0.01f], [0, 0], [0, 0.01f]));
    }
}
