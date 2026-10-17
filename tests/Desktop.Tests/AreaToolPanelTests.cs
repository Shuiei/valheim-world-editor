using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaToolTestScene;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool in the window: its panel's info and actions (remove, replace, reset, select) and dragging a box to flatten it.
/// </summary>
public class AreaToolPanelTests
{
    private static readonly int Beech = StableHash.Of("Beech1"), Rock = StableHash.Of("rock4_coast"), Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// A window on a flat area holding three trees and rocks, a wall and a far tree, with the Area tool on and a box
    /// from grid (50, 50) to (70, 70) around the first four.
    /// </summary>
    private static (MainWindow W, EditSession S) Open()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // World x, z = grid + (-32): grid (60, 60) is world (28, 28).
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, Beech, new Vector3(28, 30, 28), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(30, 30, 28), Vector3.Zero, 1, false),
            new WorldScene.Thing(3, Rock, new Vector3(28, 30, 30), Vector3.Zero, 1, false),
            new WorldScene.Thing(4, Wall, new Vector3(29, 30, 29), Vector3.Zero, 0, true),
            new WorldScene.Thing(5, Beech, new Vector3(80, 30, 80), Vector3.Zero, 1, false));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        var a = w.View.Area;
        a.Points.Add(new Vector2(50, 50));
        a.Points.Add(new Vector2(70, 70));
        a.Notify();
        return (w, s);
    }

    [AvaloniaFact]
    public void TheInfoCountsWhatIsInside()
    {
        var (w, _) = Open();
        Assert.Equal(ToolMode.Area, w.View.Mode);
        Assert.True(w.AreaPanel.Card.IsVisible);
        Assert.StartsWith("400 m² selected · 4 shown object(s) inside.", w.AreaPanel.Info.Text);
        Assert.Equal("Trees & logs 2", w.AreaPanel.KindButtons[ObjectKind.Trees].Content);
        // Hidden kinds are not counted (ruins are off at first).
        Assert.Equal("Ruins & structures hidden", w.AreaPanel.KindButtons[ObjectKind.Ruins].Content);
    }

    [AvaloniaFact]
    public async Task RemoveTakesTheChosenKindsOnly()
    {
        var (w, s) = Open();
        w.AreaPanel.Choose(AreaPanel.Act.Remove);
        Assert.Equal(AreaPanel.Act.Remove, w.AreaPanel.Current);
        w.AreaPanel.KindButtons[ObjectKind.Rocks].IsChecked = false;
        await w.AreaPanel.Apply();
        Assert.True(s.Scene.Things[0].Gone && s.Scene.Things[1].Gone);
        Assert.False(s.Scene.Things[2].Gone);
        Assert.False(s.Scene.Things[3].Gone);
        Assert.False(s.Scene.Things[4].Gone);
        Assert.Equal(2, s.Pending.Deleted);
        s.Undo();
        Assert.Equal(0, s.Pending.Deleted);
    }

    [AvaloniaFact]
    public void ReplacePutsAnotherKindInTheSamePlaces()
    {
        var (w, s) = Open();
        var p = w.AreaPanel;
        p.Choose(AreaPanel.Act.Replace);
        Assert.StartsWith("Beech1 (2)", (string)p.FromBox.SelectedItem!);
        // Headless there is no world file, so nothing to choose from: replace directly.
        p.Replace(s, [0, 1], Rock);
        Assert.True(s.Scene.Things[0].Gone);
        var added = s.Scene.Things.Skip(5).ToList();
        Assert.Equal(2, added.Count);
        Assert.All(added, t => Assert.Equal(Rock, t.Prefab));
        Assert.Equal(new Vector3(28, 30, 28), added[0].Position);
        Assert.Equal((0, 2, 2, 0), s.Pending);
        s.Undo();
        Assert.Equal((0, 0, 0, 0), s.Pending);
    }

    [AvaloniaFact]
    public async Task ResetZonesMarksTheZonesUnderTheSelection()
    {
        var (w, s) = Open();
        var p = w.AreaPanel;
        string? asked = null;
        p.Confirm = q => { asked = q; return Task.FromResult(true); };
        s.Shape(60, 60, Formula.Compile("1", []), 3, 0, "x");
        p.Choose(AreaPanel.Act.Reset);
        await p.Apply();
        Assert.Contains("Reset 4 zone(s)", asked);
        Assert.Equal(4, s.Pending.Resets);
        Assert.Equal("Reset 4 zone(s)", s.UndoLabel);
        p.UnresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(0, s.Pending.Resets);
    }

    [AvaloniaFact]
    public async Task SelectObjectsSwitchesToTheSelectTool()
    {
        var (w, _) = Open();
        w.AreaPanel.Choose(AreaPanel.Act.Select);
        await w.AreaPanel.Apply();
        Assert.Equal(ToolMode.Select, w.View.Mode);
        Assert.Equal([0, 1, 2], w.View.Selected.Order());
    }

    [AvaloniaFact]
    public void DraggingABoxThenEnterFlattensIt()
    {
        var (w, s) = Open();
        w.View.Area.Clear();
        w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
        Avalonia.Point Screen(float gx, float gz)
        {
            var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
        }
        w.MouseDown(Screen(50, 50), MouseButton.Left);
        w.MouseMove(Screen(78, 70));
        w.MouseUp(Screen(78, 70), MouseButton.Left);
        var poly = w.View.Area.Polygon()!;
        Assert.Equal(28 * 20, AreaTool.Area(poly), 0);
        w.AreaPanel.HeightBox.Value = 32;
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        Assert.Equal(32, H(s, 64, 60), 2);
        Assert.Equal("Area: Flatten", s.UndoLabel);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(w.View.Area.Polygon());
    }
}
