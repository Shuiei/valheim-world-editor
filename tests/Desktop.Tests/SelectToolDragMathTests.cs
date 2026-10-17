using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The pure part of the Select tool's handle drags: a ring drag turns the move to the heading under the pointer (Ctrl in
/// 15° steps, wrapped to -180..180), and an axis drag slides it by how far along the handle's axis the pointer is from
/// where the drag began (Ctrl in half-metre steps). A pointer with no heading, or no place along the axis, changes nothing.
/// </summary>
public sealed class SelectToolDragMathTests
{
    /// <summary>A move with the given turn (degrees) and offsets (metres).</summary>
    private static SelectTool.Move Move(float turn = 0, float dx = 0, float dy = 0, float dz = 0) =>
        new() { Items = [], Turn = turn, Dx = dx, Dy = dy, Dz = dz };

    /// <summary>A handle drag that began at the given start and move, around the middle at the origin.</summary>
    private static (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn) Drag(
        Gizmo.Handle handle, float start = 0, float dx = 0, float dz = 0, float dy = 0, float turn = 0) =>
        (handle, Vector3.Zero, start, dx, dz, dy, turn);

    [Fact]
    public void TurnByTurnsToTheHeadingUnderThePointer()
    {
        // Looking straight down at a point one metre east of the middle: its heading is 90 degrees.
        var mv = Move();
        Assert.True(SelectTool.TurnBy(mv, Drag(Gizmo.Handle.Ring), new Vector3(1, 10, 0), -Vector3.UnitY, false));
        Assert.Equal(90f, mv.Turn, 3);
    }

    [Fact]
    public void TurnByWrapsTheTurnIntoMinus180ToPlus180()
    {
        // The drag began at 170 degrees and the heading is 90: 260 wraps to -100.
        var mv = Move();
        Assert.True(SelectTool.TurnBy(mv, Drag(Gizmo.Handle.Ring, turn: 170), new Vector3(1, 10, 0), -Vector3.UnitY, false));
        Assert.Equal(-100f, mv.Turn, 3);
    }

    [Fact]
    public void TurnByWithCtrlRoundsTheTurnToFifteenDegrees()
    {
        // The drag's turn 2 plus the heading 90 is 92, which rounds to 90 in 15 degree steps.
        var mv = Move();
        Assert.True(SelectTool.TurnBy(mv, Drag(Gizmo.Handle.Ring, turn: 2), new Vector3(1, 10, 0), -Vector3.UnitY, true));
        Assert.Equal(90f, mv.Turn, 3);
    }

    [Fact]
    public void TurnByLeavesTheMoveAsItWasWhenThePointerHasNoHeading()
    {
        // Looking level has no heading: nothing changes.
        var mv = Move(turn: 7);
        Assert.False(SelectTool.TurnBy(mv, Drag(Gizmo.Handle.Ring), Vector3.Zero, Vector3.UnitX, false));
        Assert.Equal(7f, mv.Turn);
    }

    [Fact]
    public void SlideByMovesAlongTheXAxisByHowFarThePointerIs()
    {
        // The ray passes the X axis at 5 metres; the drag began at 2, so it moves 3 metres east.
        var mv = Move();
        Assert.True(SelectTool.SlideBy(mv, Drag(Gizmo.Handle.X, start: 2, dx: 1, dz: 4, dy: 6), new Vector3(5, 0, 10), -Vector3.UnitZ, false));
        Assert.Equal(4f, mv.Dx, 3);
        Assert.Equal(4f, mv.Dz, 3);
        Assert.Equal(6f, mv.Dy, 3);
    }

    [Fact]
    public void SlideByWithCtrlRoundsTheSlideToHalfMetres()
    {
        // The ray passes at 5.3 metres: 3.3 metres from the start, which rounds to 3.5.
        var mv = Move();
        Assert.True(SelectTool.SlideBy(mv, Drag(Gizmo.Handle.X, start: 2, dx: 1), new Vector3(5.3f, 0, 10), -Vector3.UnitZ, true));
        Assert.Equal(4.5f, mv.Dx, 3);
    }

    [Fact]
    public void SlideByOnTheYHandleMovesVertically()
    {
        // The ray passes the Y axis at 4 metres; the drag began at 1, so it rises 3 metres.
        var mv = Move();
        Assert.True(SelectTool.SlideBy(mv, Drag(Gizmo.Handle.Y, start: 1, dy: 2), new Vector3(0, 4, 5), -Vector3.UnitZ, false));
        Assert.Equal(5f, mv.Dy, 3);
        Assert.Equal(0f, mv.Dx, 3);
    }

    [Fact]
    public void SlideByLeavesTheMoveAsItWasWhenTheRayRunsAlongTheAxis()
    {
        // A ray parallel to the X axis has no place along it.
        var mv = Move(dx: 7);
        Assert.False(SelectTool.SlideBy(mv, Drag(Gizmo.Handle.X), new Vector3(0, 5, 0), Vector3.UnitX, false));
        Assert.Equal(7f, mv.Dx);
    }
}
