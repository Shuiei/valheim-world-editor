using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's rectangle on the screen (a drag's default): it picks the objects seen inside it, not those behind
/// others, whichever way it is dragged; a click deselects, Shift adds, Esc drops it, and it is shown once dragged past
/// 6 px.
/// </summary>
[Collection("World files")]
public class SelectToolScreenBoxTests
{
    /// <summary>
    /// Looking straight down: a floor, a second floor 4 m above it covering it, and a tree beside them.
    /// </summary>
    private static View TwoFloorsAndATree()
    {
        return Open(T(1, Floor, 40, 30, 40), T(2, Floor, 40, 34, 40), T(3, Beech, 44, 30, 44, false));
    }

    /// <summary>A drag with Alt held (over objects) from one world point on the ground to another.</summary>
    private static void Box(View v, (float X, float Z) from, (float X, float Z) to, bool shift = false)
    {
        v.Tool.Down(v.At(from.X, 30, from.Z), v.Size, shift, true, 1);
        v.Tool.Moved(v.At(to.X, 30, to.Z), v.Size);
        v.Tool.Up(v.At(to.X, 30, to.Z), v.Size, shift);
    }

    [AvaloniaFact]
    public void TheRectanglePicksWhatIsSeenNotWhatIsBehind()
    {
        var v = TwoFloorsAndATree();
        var said = MessageReader(v.Tool);
        Box(v, (36, 36), (46, 46));
        Assert.Equal([1, 2], v.Selected);
        Assert.Equal("Selected 2 object(s) inside the zone.", said());
        Assert.Null(v.Tool.ScreenBox);
        // Dragged the other way round (up and left on the screen): the same.
        v.W.View.Select([]);
        Box(v, (46, 46), (36, 36));
        Assert.Equal([1, 2], v.Selected);
    }

    [AvaloniaFact]
    public void AClickDeselectsAndShiftAdds()
    {
        var v = TwoFloorsAndATree();
        v.W.View.Select([0]);
        var p = v.At(48, 30, 48);
        v.Tool.Down(p, v.Size, false, true, 1);
        v.Tool.Up(p, v.Size, false);
        Assert.Empty(v.Selected);
        v.W.View.Select([2]);
        Box(v, (38, 38), (42, 42), shift: true);
        Assert.Equal([1, 2], v.Selected);
    }

    [AvaloniaFact]
    public void TheRectangleShowsOnceDraggedAndEscDropsIt()
    {
        var v = TwoFloorsAndATree();
        var from = v.At(36, 30, 36);
        v.Tool.Down(from, v.Size, false, true, 1);
        v.Tool.Moved(new Avalonia.Point(from.X + 3, from.Y), v.Size);
        Assert.Null(v.Tool.ScreenBox);
        var to = v.At(46, 30, 46);
        v.Tool.Moved(to, v.Size);
        Assert.Equal(SelectTool.Spanning(from, to), v.Tool.ScreenBox);
        Assert.True(v.Tool.ScreenBox!.Value.Width > 0 && v.Tool.ScreenBox.Value.Height > 0);
        Assert.True(v.Tool.Key(Key.Escape, false));
        Assert.Null(v.Tool.ScreenBox);
        v.Tool.Up(to, v.Size, false);
        Assert.Empty(v.Selected);
    }

    // Select through (Alt + Z, like Blender's X-ray): the box also takes the floor hidden under the other.
    [AvaloniaFact]
    public void SelectThroughAlsoTakesWhatIsHidden()
    {
        var v = TwoFloorsAndATree();
        v.W.KeyPress(Key.Z, RawInputModifiers.Alt, PhysicalKey.Z, "z");
        Assert.True(v.W.SelectPanel.ThroughBox.IsChecked);
        Assert.True(v.Tool.SelectThrough);
        Box(v, (36, 36), (46, 46));
        Assert.Equal([0, 1, 2], v.Selected);
        v.W.KeyPress(Key.Z, RawInputModifiers.Alt, PhysicalKey.Z, "z");
        Assert.False(v.Tool.SelectThrough);
    }

    // With Select through, a thin box across the upper floor that misses its middle and its corners pulled in (x 40
    // and 40 ± 0.67 m; ground points under a camera 40 m over the ground, so the floor 4 m up is 10% further out) still
    // takes the floor it sees there.
    [AvaloniaFact]
    public void SelectThroughStillTakesWhatIsSeenInAThinBox()
    {
        var v = TwoFloorsAndATree();
        v.Tool.SelectThrough = true;
        Box(v, (40.12f, 39.4f), (40.34f, 40.6f));
        Assert.Contains(1, v.Selected);
    }

    // Inside a box with Select through: nothing without an area or a view size; a box's points tried are its middle
    // and its corners pulled a third of the way in.
    [AvaloniaFact]
    public void InsideTheBoxIsNothingWithoutAnAreaOrASizeAndTriesNinePoints()
    {
        var r = new Avalonia.Rect(0, 0, 100, 100);
        Assert.Empty(new GlView().InsideRect(r, new Avalonia.Size(800, 600)));
        var v = TwoFloorsAndATree();
        Assert.Empty(v.W.View.InsideRect(r, new Avalonia.Size(0, 0)));
        var points = GlView.SamplesOf((new System.Numerics.Vector3(0, 0, 0), new System.Numerics.Vector3(3, 6, 9))).ToList();
        Assert.Equal(9, points.Count);
        Assert.Equal(new System.Numerics.Vector3(1.5f, 3, 4.5f), points[0]);
        Assert.Equal(new System.Numerics.Vector3(0.5f, 1, 1.5f), points[1]);
        Assert.Equal(new System.Numerics.Vector3(2.5f, 5, 7.5f), points[8]);
    }

    // A plain drag on empty ground (no Alt) draws the box too.
    [AvaloniaFact]
    public void APlainDragOnEmptyGroundDrawsTheBox()
    {
        var v = TwoFloorsAndATree();
        var from = v.At(30, 30, 30);
        v.Tool.Down(from, v.Size, false, false, 1);
        var to = v.At(36, 30, 36);
        v.Tool.Moved(to, v.Size);
        Assert.Equal(SelectTool.Spanning(from, to), v.Tool.ScreenBox);
        Assert.Null(v.Tool.Lasso);
        v.Tool.Up(to, v.Size, false);
        Assert.Empty(v.Selected);
    }

    // The box between two corners, whichever way round; a view point's place on the screen, none behind the camera.
    [AvaloniaFact]
    public void TheBoxAndTheScreenPlacesAreFoundEitherWayRound()
    {
        Assert.Equal(new Avalonia.Rect(10, 20, 30, 40), SelectTool.Spanning(new Avalonia.Point(40, 60), new Avalonia.Point(10, 20)));
        Assert.Equal(new Avalonia.Rect(10, 20, 30, 40), SelectTool.Spanning(new Avalonia.Point(10, 60), new Avalonia.Point(40, 20)));
        var v = TwoFloorsAndATree();
        var c = v.S.Scene;
        var at = v.W.View.ScreenOf(new System.Numerics.Vector3(44 - c.Cx, 30, -(44 - c.Cz)), v.Size)!.Value;
        var want = v.At(44, 30, 44);
        Assert.Equal(want.X, at.X, 1);
        Assert.Equal(want.Y, at.Y, 1);
        // Above the camera (40 m over the ground, looking down): behind it.
        Assert.Null(v.W.View.ScreenOf(new System.Numerics.Vector3(40 - c.Cx, 200, -(40 - c.Cz)), v.Size));
    }
}
