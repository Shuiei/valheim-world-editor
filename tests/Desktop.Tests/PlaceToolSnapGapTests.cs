using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The length a snapped run leaves uncovered after its last piece: the start to the first point, then each segment after,
/// each added to the gap.
/// </summary>
public class PlaceToolSnapGapTests
{
    [Fact]
    public void TheGapIsTheLengthOfEverySegmentFromTheStart()
    {
        // Start at the origin: 3 metres to (3, 0), then 4 metres up to (3, 4).
        var tool = new PlaceTool();
        var pts = new List<Vector2> { new(0, 0), new(3, 0), new(3, 4) };

        tool.AddSnapGap(pts, 0, new Vector2(0, 0));

        Assert.Equal(7f, tool.SnapGap, 4);
    }

    [Fact]
    public void ASegmentBeforeTheSnappedOneIsNotCounted()
    {
        // The run starts on segment 1, from (3, 0): only the 4 metres of that segment count.
        var tool = new PlaceTool();
        var pts = new List<Vector2> { new(0, 0), new(3, 0), new(3, 4) };

        tool.AddSnapGap(pts, 1, new Vector2(3, 0));

        Assert.Equal(4f, tool.SnapGap, 4);
    }
}
