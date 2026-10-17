using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A press on a shape or the grid, and a press that is dragged: a grid press starts the box, and a dragged press on a
/// line or zone adds its points.
/// </summary>
public class PlaceInputPressTests
{
    [AvaloniaFact]
    public void APressOnTheGridStartsABox()
    {
        var r = new PlaceInputTests.Rig(PlaceTool.Modes.Grid);
        r.In.DownShape(r.At(64, 64), new Vector2(40, 40), false, 1);
        Assert.Equal(new Vector2(40, 40), r.T.GridA);
        Assert.Null(r.T.GridB);
    }

    [AvaloniaFact]
    public void ADraggedPressOnAZoneAddsItsPoints()
    {
        var r = new PlaceInputTests.Rig(PlaceTool.Modes.Zone);
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        Assert.Empty(r.T.Points);
        // Far enough from the press (over 6 px) to be a drag: the press's point starts the zone.
        Assert.True(r.In.DragPress(r.At(90, 64), new Vector2(60, 60)));
        Assert.Single(r.T.Points);
    }

    [AvaloniaFact]
    public void ANotDraggedPressIsNotAMove()
    {
        var r = new PlaceInputTests.Rig(PlaceTool.Modes.Zone);
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        // Within 6 px of the press: still a click, so the move is not taken here.
        Assert.False(r.In.DragPress(r.At(65, 64), new Vector2(41, 40)));
        Assert.Empty(r.T.Points);
    }
}
