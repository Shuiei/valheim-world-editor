using Avalonia.Headless.XUnit;
using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectAreaRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select panel's exact place: typed places, By amounts on each axis, a typed move too small to be dragged still
/// made, and a move past the area's edge keeping the height.
/// </summary>
[Collection("World files")]
public class SelectToolPlaceTests
{
    [AvaloniaFact]
    public void ExactPlaceMovesThereOrBy()
    {
        var (w, s) = Window();
        w.View.Select([1]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var p = w.SelectPanel;
        Assert.Equal(40, (double)p.XBox.Value!, 2);
        Assert.Equal(90, (double)p.TurnBox.Value!, 2);
        p.XBox.Value = 50;
        p.ZBox.Value = 44;
        p.TurnBox.Value = 0;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        var moved = Last(s);
        Assert.Equal(new Vector3(50, 30, 44), moved.Position);
        Assert.Equal(0, moved.Rotation.Y, 3);
        // By: from where it is now.
        p.ByButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(0, (double)p.XBox.Value!);
        p.YBox.Value = 2;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(new Vector3(50, 32, 44), Last(s).Position);
        // By, with all four boxes: each amount goes to its own axis.
        p.ByButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        p.XBox.Value = 1;
        p.YBox.Value = 2;
        p.ZBox.Value = -3;
        p.TurnBox.Value = 45;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        moved = Last(s);
        Assert.Equal(51, moved.Position.X, 3);
        Assert.Equal(34, moved.Position.Y, 3);
        Assert.Equal(41, moved.Position.Z, 3);
        Assert.Equal(45, moved.Rotation.Y, 3);
    }

    // A typed move smaller than the centimetre across or millimetre up a dragged move needs is still made: the place
    // was asked for exactly.
    [AvaloniaFact]
    public void ATypedMoveUnderACentimetreIsMade()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.View.SelectTool.PlaceAt(0.006f, 0, 0, 0, by: true);
        Assert.True(s.CanUndo);
        Assert.Equal(20.006f, Last(s).Position.X, 4);
        float y = Last(s).Position.Y;
        w.View.SelectTool.PlaceAt(0, 0.0005f, 0, 0, by: true);
        Assert.InRange(Last(s).Position.Y - y, 0.00049f, 0.00051f);
        Assert.Equal(20.006f, Last(s).Position.X, 4);
        w.CloseWithoutAsking();
    }

    // Put on the ground, an object moved past the area's edge (no ground there) keeps its height, and the others are
    // not thrown off by it: neither a wall sinking 1000 m with the tree inside, nor the wall flying up with the tree out.
    [AvaloniaFact]
    public void AMovePastTheAreasEdgeKeepsTheHeight()
    {
        foreach (float by in new[] { 70f, -60f })
        {
            var (w, s) = Window();
            w.View.Select([0, 1]);
            w.View.SelectTool.PlaceAt(by, 0, 0, 0, by: true);
            var moved = s.Scene.Things.Skip(2).ToList();
            Assert.Equal(2, moved.Count);
            var tree = moved.Single(t => t.Prefab == Beech);
            var wall = moved.Single(t => t.Prefab == Wall);
            var (outside, inside) = by > 0 ? (wall, tree) : (tree, wall);
            Assert.True(outside.Position.X is > 96 or < -32);
            Assert.Equal(30, outside.Position.Y, 3);
            // Inside, on the ground: the tree at its foot, the wall with its bottom (0.5 m below its middle) on it.
            Assert.Equal(inside == tree ? 30 : 30.5f, inside.Position.Y, 3);
            w.CloseWithoutAsking();
        }
    }
}
