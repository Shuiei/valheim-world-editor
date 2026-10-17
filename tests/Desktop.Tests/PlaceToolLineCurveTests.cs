using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The points of a Place line as they are drawn when the curve is off or there are too few points for one: each
/// point is paired with the segment it starts, the last point sharing the segment before it.
/// </summary>
public class PlaceToolLineCurveTests
{
    [Fact]
    public void AStraightLineOfThreePointsPairsEachPointWithItsSegment()
    {
        var tool = new PlaceTool { Curve = false };
        tool.Points.Add(new Vector2(40, 40));
        tool.Points.Add(new Vector2(80, 40));
        tool.Points.Add(new Vector2(80, 80));

        Assert.Equal(
            [(new Vector2(40, 40), 0), (new Vector2(80, 40), 1), (new Vector2(80, 80), 1)],
            tool.LineCurve());
    }

    [Fact]
    public void TwoPointsGiveTwoPointsOnTheOnlySegment()
    {
        var tool = new PlaceTool();
        tool.Points.Add(new Vector2(0, 0));
        tool.Points.Add(new Vector2(10, 0));

        Assert.Equal([(new Vector2(0, 0), 0), (new Vector2(10, 0), 0)], tool.LineCurve());
    }

    [Fact]
    public void NoPointsGiveNoLine()
    {
        Assert.Empty(new PlaceTool().LineCurve());
    }
}
