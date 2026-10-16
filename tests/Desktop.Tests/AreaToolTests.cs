using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool: a box or polygon selection, and the actions done inside it.
/// </summary>
public class AreaToolTests
{
    /// <summary>
    /// A box selection between two grid corners, with this soft edge.
    /// </summary>
    private static AreaTool Box(float x0, float z0, float x1, float z1, float soft = 0)
    {
        var a = new AreaTool { Soft = soft };
        a.Points.Add(new Vector2(x0, z0));
        a.Points.Add(new Vector2(x1, z1));
        return a;
    }

    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    [Fact]
    public void ABoxWithoutASoftEdgeHasStraightEdges()
    {
        var a = Box(10, 10, 20, 20);
        var w = a.WeightsIn(129, 129)!;
        var at = w.Cells.ToDictionary(c => c.G, c => c.W);
        Assert.Equal(1, at[15 * 129 + 15], 3);
        // On the outline: half.
        Assert.Equal(0.5f, at[15 * 129 + 10], 3);
        Assert.False(at.ContainsKey(15 * 129 + 9));
        Assert.Equal(100, AreaTool.Area(a.Polygon()!), 3);
    }

    [Fact]
    public void TheGridBoxIsTheWholePointsRoundTheOutlineInTheBlock()
    {
        Assert.Equal((10, 10, 20, 21), Box(10.4f, 10, 20, 20.6f).GridBox(129, 129));
        Assert.Equal((0, 0, 128, 10), Box(-5, -5, 200, 10).GridBox(129, 129));
        Assert.Null(new AreaTool().GridBox(129, 129));
    }

    [Fact]
    public void ACrossedOutlinesAreaIsWhatTheSelectionPicks()
    {
        // The shoelace formula let crossed parts cancel: a figure eight read 0 m² with both loops selected.
        List<Vector2> eight = [new(0, 0), new(10, 10), new(10, 0), new(0, 10)];
        Assert.Equal(50, AreaTool.Area(eight), 3);
        // A crossed star: its middle is not selected, nor counted.
        List<Vector2> star = [.. Enumerable.Range(0, 5).Select(i => i * 2 % 5 * MathF.Tau / 5).Select(t => new Vector2(20 + 10 * MathF.Cos(t), 20 + 10 * MathF.Sin(t)))];
        int picked = 0;
        for (int i = 0; i < 600; i++)
        {
            for (int j = 0; j < 600; j++)
            {
                picked += AreaTool.Inside(star, 5 + (j + 0.5f) * 0.05f, 5 + (i + 0.5f) * 0.05f) ? 1 : 0;
            }
        }
        Assert.Equal(picked * 0.0025f, AreaTool.Area(star), 1f);
        // The shoelace formula gave about 147 (the middle counted twice); the five points are about 77.7.
        Assert.InRange(AreaTool.Area(star), 76f, 79f);
    }

    [Fact]
    public void TheSoftEdgeFadesInwards()
    {
        var a = Box(10, 10, 30, 30, soft: 4);
        var at = a.WeightsIn(129, 129)!.Cells.ToDictionary(c => c.G, c => c.W);
        Assert.Equal(1, at[20 * 129 + 20], 3);
        Assert.True(at[20 * 129 + 12] is > 0 and < 1);
        Assert.False(at.ContainsKey(20 * 129 + 10));
    }

    [Fact]
    public void APolygonClosesOnADoubleClick()
    {
        var a = new AreaTool { Box = false };
        a.Down(new Vector2(0, 0), 1);
        a.Down(new Vector2(10, 0), 1);
        Assert.Null(a.Polygon());
        a.Down(new Vector2(10, 10), 1);
        Assert.NotNull(a.Polygon());
        Assert.False(a.Closed);
        a.Down(new Vector2(10, 10), 2);
        Assert.True(a.Closed);
        Assert.Equal(3, a.Points.Count);
        // A click after closing starts a new one.
        a.Down(new Vector2(50, 50), 1);
        Assert.Single(a.Points);
    }

    [Fact]
    public void GroundActionsChangeOnlyTheInside()
    {
        var s = TestScenes.Flat(2);
        var a = Box(40, 40, 80, 80, soft: 2);
        Assert.Contains("dig 0 m³, fill", a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 33, 2));
        s.EditGround("Area: Flatten", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Flatten, 33, 0, new float[4]));
        Assert.Equal(33, H(s, 60, 60), 3);
        Assert.Equal(30, H(s, 90, 60), 3);
        Assert.Equal(33, a.Average(s.Ground)!.Value, 0);
        Assert.Contains("raised", a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 2));
        s.EditGround("Area: Restore", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Restore, 0, 0, new float[4]));
        Assert.Equal(30, H(s, 60, 60), 3);
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.OrderBy(z => z), a.ZonesUnder(s.Ground).OrderBy(z => z));
    }

    // The zones under a selection do not depend on the Soft slider: a selection narrower than the soft edge (every
    // point's weight well under one half) still names its zones, and one that crosses a zone border names both sides.
    [Fact]
    public void ZonesUnderDoNotDependOnTheSoftEdge()
    {
        var s = TestScenes.Flat(2);
        Assert.Equal([(0, 0)], Box(40, 40, 55, 55, soft: 20).ZonesUnder(s.Ground));
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.OrderBy(z => z), Box(56, 56, 72, 72, soft: 20).ZonesUnder(s.Ground).OrderBy(z => z));
        Assert.Equal([(0, 0)], Box(40, 40, 55, 55).ZonesUnder(s.Ground));
        // A grid point on the zones' shared edge belongs to both.
        Assert.Equal(new[] { (0, 0), (1, 0) }.OrderBy(z => z), Box(63.5f, 10.5f, 64.5f, 11.5f, soft: 20).ZonesUnder(s.Ground).OrderBy(z => z));
    }

    [Fact]
    public void VolumeCountsEverythingWithNoLimit()
    {
        var s = TestScenes.Flat(2);
        var a = Box(40, 40, 80, 80);
        static string Fill(string text) => System.Text.RegularExpressions.Regex.Match(text, @"fill [\d.k]+ m³").Value;
        // 30 m up on 30 m ground: the limit reaches 8 m of it, No limit all of it.
        string flatten = a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 60, 0), raise = a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 30);
        Assert.Contains("out of reach: ±8 m limit", flatten);
        s.Ground.NoLimit = true;
        string flattenAll = a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 60, 0), raiseAll = a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 30);
        Assert.DoesNotContain("out of reach", flattenAll);
        Assert.NotEqual(Fill(raise), Fill(raiseAll));
        Assert.Equal(Fill(flattenAll), Fill(raiseAll));
        Assert.Equal(Fill(raiseAll).Replace("fill", "dig"), System.Text.RegularExpressions.Regex.Match(a.Volume(s.Ground, AreaTool.GroundAction.Lower, 0, 30), @"dig [\d.k]+ m³").Value);
        // Smoothing kept by the game's limit takes from the reach on its side: a point smoothed 1 m up gets down
        // to 7 m below the generated ground, not 8, as SetHeight has it.
        s.Ground.NoLimit = false;
        int q = 60 * s.Ground.W + 60;
        s.Ground.Mod[q] = 1;
        s.Ground.Smooth[q] = 1;
        var one = Box(59.5f, 59.5f, 60.5f, 60.5f);
        Assert.True(s.Ground.SetHeight(q, 20));
        Assert.Equal(23, s.Ground.HeightOf(q), 3);
        s.Ground.Level[q] = 0;
        Assert.Contains("dig 8 m³", one.Volume(s.Ground, AreaTool.GroundAction.Lower, 0, 30));
        s.Ground.Mod[q] = 0;
        s.Ground.Smooth[q] = 0;
        s.Ground.NoLimit = true;
        // The lift stops at ±400 m of the generated ground: 500 m up is 100 m out of reach. Checked on the one
        // point (weight 1): the box's outline runs through its edge points, which get half the amount, so
        // 500 m lifts them more than 400 m does.
        Assert.Contains("out of reach: ±400 m lift limit", a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 530, 0));
        Assert.Contains("fill 400 m³", one.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 400));
        Assert.Contains("fill 400 m³", one.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 500));
    }

    // Restore over No limit ground (a mountain, held as lift past the ±8 m limit) brings it back down
    // too (it changed nothing to be seen).
    [Fact]
    public void RestoreTakesAMountainAway()
    {
        var s = TestScenes.Flat(5);
        Assert.NotNull(s.Mountain(160, 160, new MountainSpec(MountainKind.Peak, 80, 60, 0.6f, 0, 42), clear: false, "Mountain: test"));
        Assert.True(H(s, 160, 160) > 80);
        var a = Box(100, 100, 220, 220);
        s.EditGround("Area: Restore", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Restore, 0, 0, new float[4]));
        Assert.Equal(30, H(s, 160, 160), 3);
        Assert.Equal(0, s.Ground.Lift[160 * s.Ground.W + 160]);
    }

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
