using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's rectangle on the screen (a drag's default): it picks the objects seen inside it, not those behind
/// others, whichever way it is dragged; a click deselects, Ctrl adds, Shift takes out, Esc drops it, and it is shown
/// once dragged past 6 px.
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

    /// <summary>A drag with Alt held (over objects), and these keys, from one world point on the ground to another.</summary>
    private static void Box(View v, (float X, float Z) from, (float X, float Z) to, KeyModifiers keys = KeyModifiers.None)
    {
        v.Tool.Down(v.At(from.X, 30, from.Z), v.Size, keys | KeyModifiers.Alt, 1);
        v.Tool.Moved(v.At(to.X, 30, to.Z), v.Size);
        v.Tool.Up(v.At(to.X, 30, to.Z), v.Size, keys);
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
    public void AClickDeselectsCtrlAddsAndShiftTakesOut()
    {
        var v = TwoFloorsAndATree();
        v.W.View.Select([0]);
        var p = v.At(48, 30, 48);
        v.Tool.Down(p, v.Size, KeyModifiers.Alt, 1);
        v.Tool.Up(p, v.Size, KeyModifiers.None);
        Assert.Empty(v.Selected);
        v.W.View.Select([2]);
        Box(v, (38, 38), (42, 42), KeyModifiers.Control);
        Assert.Equal([1, 2], v.Selected);
        // Shift: what is seen inside taken out.
        var said = MessageReader(v.Tool);
        Box(v, (38, 38), (42, 42), KeyModifiers.Shift);
        Assert.Equal([2], v.Selected);
        Assert.Equal("Took 1 object(s) inside the zone out of the selection (1 left).", said());
    }

    [AvaloniaFact]
    public void TheRectangleShowsOnceDraggedAndEscDropsIt()
    {
        var v = TwoFloorsAndATree();
        var from = v.At(36, 30, 36);
        v.Tool.Down(from, v.Size, KeyModifiers.Alt, 1);
        v.Tool.Moved(new Avalonia.Point(from.X + 3, from.Y), v.Size);
        Assert.Null(v.Tool.ScreenBox);
        var to = v.At(46, 30, 46);
        v.Tool.Moved(to, v.Size);
        Assert.Equal(SelectTool.Spanning(from, to), v.Tool.ScreenBox);
        Assert.True(v.Tool.ScreenBox!.Value.Width > 0 && v.Tool.ScreenBox.Value.Height > 0);
        Assert.True(v.Tool.Key(Key.Escape, false));
        Assert.Null(v.Tool.ScreenBox);
        v.Tool.Up(to, v.Size, KeyModifiers.None);
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
}
