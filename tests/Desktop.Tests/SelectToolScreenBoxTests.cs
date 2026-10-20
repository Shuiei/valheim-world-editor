using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's rectangle on the screen (Pick only what is seen): it picks the objects seen inside it, not those
/// behind others; a click deselects, Shift adds, Esc drops it, and it is shown once dragged past 6 px.
/// </summary>
[Collection("World files")]
public class SelectToolScreenBoxTests
{
    /// <summary>
    /// Looking straight down: a floor, a second floor 4 m above it covering it, and a tree beside them.
    /// </summary>
    private static View TwoFloorsAndATree()
    {
        var v = Open(T(1, Floor, 40, 30, 40), T(2, Floor, 40, 34, 40), T(3, Beech, 44, 30, 44, false));
        v.Tool.SeenOnly = true;
        return v;
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
        Assert.Equal(new Avalonia.Rect(from, to), v.Tool.ScreenBox);
        Assert.True(v.Tool.Key(Key.Escape, false));
        Assert.Null(v.Tool.ScreenBox);
        v.Tool.Up(to, v.Size, false);
        Assert.Empty(v.Selected);
    }
}
