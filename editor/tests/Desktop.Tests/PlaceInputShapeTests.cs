using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

using Rig = TerrainEditor.Desktop.Tests.PlaceInputTests.Rig;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's input for shapes in the 3D view: lines and their points, a freehand drag, zones clicked and turned,
/// grid boxes, circles and rectangles dragged out, the keys that turn them, and what nothing listening says.
/// </summary>
public class PlaceInputShapeTests
{
    /// <summary>A point of the view off the area (its top left corner): no ground under it.</summary>
    private static readonly Avalonia.Point Sky = new(2, 2);

    [AvaloniaFact]
    public void LinePointsOffTheAreaAndDoubleClicksAwayFromTheEnd()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        // Off the area: nothing.
        r.Click(Sky);
        Assert.Empty(r.T.Points);
        // A double-click on the first point: no line yet to place.
        r.Click(40, 64, clicks: 2);
        r.Click(64, 64);
        // A double-click away from the last point adds one.
        r.Click(30, 30, clicks: 2);
        Assert.Equal(3, r.T.Points.Count);
        Assert.Equal(0, r.S.Pending.Added);
        // Esc takes the line; with none left Backspace and Esc are not the tool's.
        Assert.True(r.Key(Key.Escape));
        Assert.Empty(r.T.Points);
        Assert.False(r.Key(Key.Back));
        Assert.False(r.Key(Key.Escape));
        r.In.RemoveLastPoint();
        Assert.Empty(r.T.Points);
    }

    [AvaloniaFact]
    public void AFreehandDragAddsAPointEveryTwoMetres()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.Drag((30, 40), (33, 40), (36, 40), (39, 40));
        // Let go where the last point went: none added there again.
        Assert.Equal(4, r.T.Points.Count);
        // Pressed and barely moved: no drag yet; then on from the line's end.
        r.In.Down(r.At(50, 40), r.Size, false, false, false, 1);
        r.In.Moved(r.At(50.5f, 40), r.Size);
        Assert.Equal(4, r.T.Points.Count);
        r.In.Moved(r.At(54, 40), r.Size);
        r.In.Moved(r.At(55, 40), r.Size);
        Assert.Equal(6, r.T.Points.Count);
        r.In.Up(r.At(56, 40), r.Size);
        Assert.Equal(7, r.T.Points.Count);
    }

    [AvaloniaFact]
    public void AZoneIsClickedEditedTurnedAndPlaced()
    {
        var r = new Rig(PlaceTool.Modes.Zone);
        Assert.True(r.Key(Key.OemPeriod));
        Assert.Equal("Draw a zone first, then turn it.", r.Last);
        foreach (var (x, z) in new[] { (40f, 40f), (90f, 40f), (90f, 90f) })
        {
            r.Click(x, z);
        }
        Assert.Equal(3, r.T.Points.Count);
        // Turned by 15° (Shift); its points are kept unturned and dragged where they are drawn.
        Assert.True(r.Key(Key.OemPeriod, shift: true));
        Assert.Equal(15, r.T.ShapeTurn, 3);
        var first = r.T.Xf(r.T.Points[0]);
        var to = first + new Vector2(0, -6);
        r.In.Down(r.At(first), r.Size, false, false, false, 1);
        r.In.Moved(r.At(to), r.Size);
        r.In.Up(r.At(to), r.Size);
        Assert.Equal(3, r.T.Points.Count);
        Assert.True(Vector2.Distance(r.T.Xf(r.T.Points[0]), to) < 0.5f, $"{r.T.Xf(r.T.Points[0])}");
        // A new point, kept unturned.
        r.Click(60, 110);
        Assert.Equal(4, r.T.Points.Count);
        Assert.True(Vector2.Distance(r.T.Xf(r.T.Points[^1]), new Vector2(60, 110)) < 0.5f);
        int shown = r.In.Shown.Length;
        Assert.True(shown > 0);
        Assert.True(r.Key(Key.Enter));
        Assert.Equal($"Placed {shown} in a zone", r.S.UndoLabel);
        Assert.True(r.Key(Key.Escape));
        Assert.Empty(r.T.Points);
    }

    [AvaloniaFact]
    public void AGridBoxIsPlacedAndClearedWithTheKeys()
    {
        var r = new Rig(PlaceTool.Modes.Grid);
        r.Drag((50, 50), (60, 60), (70, 66));
        Assert.NotNull(r.T.GridB);
        int shown = r.In.Shown.Length;
        Assert.True(shown > 0);
        Assert.True(r.Key(Key.Enter));
        Assert.Equal($"Placed {shown} in a grid", r.S.UndoLabel);
        // Backspace is no grid key; Esc clears the box.
        Assert.False(r.Key(Key.Back));
        r.In.RemoveLastPoint();
        Assert.True(r.Key(Key.Escape));
        Assert.Null(r.T.GridA);
    }

    [AvaloniaFact]
    public void CirclesAndRectanglesAreDraggedOutAndCleared()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.T.LineShape = PlaceTool.LineShapes.Circle;
        r.Click(64, 64);
        Assert.Equal("Press at the centre and drag out to the size.", r.Last);
        Assert.Null(r.T.FigA);
        r.Drag((64, 64), (70, 68));
        Assert.NotNull(r.T.FigA);
        Assert.NotNull(r.T.FigB);
        Assert.True(r.In.Shown.Length > 0);
        // Dragged off the area and let go there: as a click.
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        r.In.Moved(Sky, r.Size);
        r.In.Up(Sky, r.Size);
        Assert.Null(r.T.FigA);
        r.T.LineShape = PlaceTool.LineShapes.Rect;
        r.Click(64, 64);
        Assert.Equal("Press at a corner and drag to the opposite one.", r.Last);
        r.Drag((64, 64), (70, 68));
        Assert.True(r.Key(Key.Escape));
        Assert.Null(r.T.FigA);
    }

    [AvaloniaFact]
    public void KeysTurnByTheirStepAndLeaveTheRest()
    {
        var r = new Rig(PlaceTool.Modes.Brush);
        Assert.False(r.In.Key(Key.R, false, true));
        // The Workshop's own step; Shift: 1°.
        r.In.TurnStep = 22.5f;
        Assert.True(r.Key(Key.OemPeriod));
        Assert.True(r.Key(Key.OemPeriod, shift: true));
        Assert.Equal(23.5f, r.T.Rotation, 3);
        r.In.TurnStep = null;
        Assert.True(r.Key(Key.OemComma, shift: true));
        Assert.Equal(8.5f, r.T.Rotation, 3);
        // Not the brush's keys.
        Assert.False(r.Key(Key.Enter));
        Assert.False(r.Key(Key.Escape));
        Assert.False(r.Key(Key.Back));
        Assert.True(r.Key(Key.R));
        Assert.True(r.Key(Key.PageDown, shift: true));
        Assert.StartsWith("Elevation: above the ground by -0.1", r.Last);
    }

    [AvaloniaFact]
    public void PlacingAShapeNeedsAnAreaOpen()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.Click(40, 64);
        r.Click(60, 64);
        Assert.True(r.In.Shown.Length > 0);
        r.In.Session = () => null;
        r.In.PlaceShape();
        Assert.Equal("Draw a line first (click points on the ground).", r.Last);
        Assert.Equal(0, r.S.Pending.Added);
    }

    [AvaloniaFact]
    public void WithNobodyListeningNothingIsSaid()
    {
        var r = new Rig(PlaceTool.Modes.Brush, listen: false);
        r.In.Moved(r.At(64, 64), r.Size);
        r.In.PickOnce = (_, _) => { };
        r.Click(10, 10);
        r.Click(40, 40, alt: true);
        r.In.Lift(0.5f);
        Assert.True(r.Key(Key.PageUp));
        r.In.PickOnce = (_, _) => { };
        Assert.True(r.Key(Key.Escape));
        // A click places, a Shift click erases.
        r.Click(64, 64);
        int placed = r.S.Pending.Added;
        Assert.True(placed > 0);
        r.Click(20, 20, shift: true);
        // Building, with nothing placed.
        r.T.Building = true;
        r.Click(Sky);
        r.T.Building = false;
        r.T.Mode = PlaceTool.Modes.Grid;
        Assert.True(r.Key(Key.OemPeriod));
        r.In.PlaceShape();
        r.T.Mode = PlaceTool.Modes.Line;
        r.T.LineShape = PlaceTool.LineShapes.Circle;
        r.Click(64, 64);
        r.T.LineShape = PlaceTool.LineShapes.Points;
        r.Click(40, 64);
        r.Click(60, 64);
        Assert.True(r.Key(Key.Enter));
        Assert.True(r.S.Pending.Added > placed);
    }
}
