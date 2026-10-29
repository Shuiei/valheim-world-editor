using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The move arrows and the turning ring.
/// </summary>
public class GizmoTests
{
    [Fact]
    public void TheClosestPointsOfTwoLines()
    {
        // A ray going down through (5, 10, 0), the x axis through 0.
        var (t, s, d) = Gizmo.Closest(new Vector3(5, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitX);
        Assert.Equal(10, t, 4);
        Assert.Equal(5, s, 4);
        Assert.Equal(0, d, 4);
        Assert.Equal(5, Gizmo.Along(new Vector3(5, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitX)!.Value, 4);
        // Looking straight down an axis: no answer.
        Assert.Null(Gizmo.Along(new Vector3(0, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitY));
    }

    [Fact]
    public void RaysHitTheArrowsAndTheRing()
    {
        var down = -Vector3.UnitY;
        Assert.Equal(Gizmo.Handle.X, Gizmo.Hit(new Vector3(0.7f, 10, 0), down, Vector3.Zero, 1));
        Assert.Equal(Gizmo.Handle.Z, Gizmo.Hit(new Vector3(0, 10, -0.7f), down, Vector3.Zero, 1));
        Assert.Equal(Gizmo.Handle.Ring, Gizmo.Hit(new Vector3(1.45f, 10, 0.05f), down, Vector3.Zero, 1));
        // The middle (the object itself) is not a handle; scaled up, the arrows are further out.
        Assert.Null(Gizmo.Hit(new Vector3(0.05f, 10, 0.05f), down, Vector3.Zero, 1));
        Assert.Equal(Gizmo.Handle.X, Gizmo.Hit(new Vector3(7, 10, 0), down, Vector3.Zero, 10));
        Assert.Equal(Gizmo.Handle.Y, Gizmo.Hit(new Vector3(-10, 0.7f, 0), Vector3.UnitX, Vector3.Zero, 1));
    }

    [Fact]
    public void HeadingsAreClockwiseFromNorth()
    {
        // View space: north is -z, east +x.
        Assert.Equal(0, Gizmo.Heading(new Vector3(0, 10, -1), -Vector3.UnitY, Vector3.Zero)!.Value, 3);
        Assert.Equal(90, Gizmo.Heading(new Vector3(1, 10, 0), -Vector3.UnitY, Vector3.Zero)!.Value, 3);
    }

    /// <summary>
    /// A shown main window with one half wall selected, looking down on it; with the screen point of a point in view
    /// space.
    /// </summary>
    private static (MainWindow W, EditSession S, Func<Vector3, Avalonia.Point> Screen) Open()
    {
        var (w, s, screen) = ShownEditor.Open(new WorldScene.Thing(11, StableHash.Of("wood_wall_half"), new Vector3(40, 30, 40), Vector3.Zero, 0, true));
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        // Looking down at the wall from above (a little to the south, so north is up on screen).
        var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
        w.View.SetCamera(c + new Vector3(0, 30, 3), c, 1);
        return (w, s, screen);
    }

    [AvaloniaFact]
    public void DraggingTheEastArrowMovesTheSelectionEast()
    {
        var (w, s, screen) = Open();
        var (c, scale) = w.View.GizmoAt()!.Value;
        var from = screen(c + new Vector3(0.7f * scale, 0, 0));
        var to = screen(c + new Vector3(0.7f * scale + 4, 0, 0));
        w.MouseMove(from);
        Assert.Equal(Gizmo.Handle.X, w.View.SelectTool.HotHandle);
        w.MouseDown(from, MouseButton.Left);
        w.MouseMove(to);
        w.MouseUp(to, MouseButton.Left);
        var moved = s.Scene.Things[^1];
        Assert.True(s.Scene.Things[0].Gone);
        Assert.Equal(44, moved.Position.X, 1);
        Assert.Equal(40, moved.Position.Z, 1);
    }

    [AvaloniaFact]
    public void DraggingTheRingTurnsTheSelection()
    {
        var (w, s, screen) = Open();
        var (c, scale) = w.View.GizmoAt()!.Value;
        float r = Gizmo.RingRadius * scale;
        // From the north of the ring to its east: a quarter turn clockwise.
        var from = screen(c + new Vector3(0, 0, -r));
        var to = screen(c + new Vector3(r, 0, 0));
        w.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        w.MouseMove(to, RawInputModifiers.Control);
        w.MouseUp(to, MouseButton.Left);
        Assert.Equal(90, s.Scene.Things[^1].Rotation.Y, 1);
        Assert.Equal(new Vector2(40, 40), new Vector2(s.Scene.Things[^1].Position.X, s.Scene.Things[^1].Position.Z));
    }

    [Fact]
    public void RingHitNeedsARayThatMeetsThePlaneNearTheRing()
    {
        // Straight down through the ring's radius at scale 1: hit.
        Assert.True(Gizmo.RingHit(new Vector3(Gizmo.RingRadius, 5, 0), -Vector3.UnitY, Vector3.Zero, 1));
        // Straight down through the middle: the plane point is too close to the axis.
        Assert.False(Gizmo.RingHit(new Vector3(0, 5, 0), -Vector3.UnitY, Vector3.Zero, 1));
        // Level ray: it never meets the plane.
        Assert.False(Gizmo.RingHit(new Vector3(Gizmo.RingRadius, 0, 0), Vector3.UnitX, Vector3.Zero, 1));
        // The plane is behind the ray: no hit.
        Assert.False(Gizmo.RingHit(new Vector3(Gizmo.RingRadius, -5, 0), -Vector3.UnitY, Vector3.Zero, 1));
    }
}
