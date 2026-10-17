using System.Numerics;
using Avalonia;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool on a runestone (the game builds it from its location: it can be deleted, not moved or turned), and
/// the mouse with no area open or no drag under way.
/// </summary>
public class SelectToolRunestoneTests
{
    [AvaloniaFact]
    public void ARunestoneIsNotMovedAndSaysWhy()
    {
        var stone = new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        Assert.True(stone.Runestone);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(1, stone);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        var said = new List<(string Text, bool Warn)>();
        w.View.SelectTool.Message += (t, warn) => said.Add((t.Shown, warn));
        w.View.SelectTool.PlaceAt(5, 0, 5, 0, by: true);
        Assert.Contains(("Runestones cannot be moved or turned, only deleted (the game builds them from their location).", true), said);
        Assert.False(w.View.SelectTool.Moving);
        Assert.Equal(new Vector3(0, 30, 0), s.Scene.Things[0].Position);
        Assert.Empty(s.UndoList);
        // Deleted, it can be.
        s.Delete([0]);
        Assert.True(s.Scene.Things[0].Gone);
    }

    [AvaloniaFact]
    public void ATypedPlaceIsTheMovedObjectsNotTheRunestones()
    {
        var stone = new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 1, false);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(1, stone, tree);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.Select([0, 1]);
        var (x, _, z, _, count) = w.View.SelectTool.Where()!.Value;
        Assert.Equal((10f, 10f, 1), (x, z, count));
        w.View.SelectTool.PlaceAt(20, 30, 20, 0, by: false);
        // The tree's moved copy (a move adds one at the end), and the stone where it was.
        var moved = Assert.Single(s.Scene.Things.Skip(2));
        Assert.Equal(tree.Prefab, moved.Prefab);
        Assert.Equal(20, moved.Position.X, 3);
        Assert.Equal(20, moved.Position.Z, 3);
        Assert.Equal(new Vector3(0, 30, 0), s.Scene.Things[0].Position);
        Assert.False(s.Scene.Things[0].Gone);
    }

    [AvaloniaFact]
    public void TheMouseWithNoAreaOrNoDragDoesNothing()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var tool = w.View.SelectTool;
        tool.Down(new Point(10, 10), new Size(100, 100), shift: false, alt: false, clicks: 1);
        Assert.False(tool.Moving);
        var s = TestScenes.Flat(1);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        tool.Moved(new Point(20, 20), new Size(100, 100));
        Assert.False(tool.Moving);
        Assert.Empty(w.View.Selected);
    }
}
