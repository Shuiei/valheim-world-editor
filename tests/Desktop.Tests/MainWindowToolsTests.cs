using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window's tools that change the ground in one go: the Path tool's cave (roofed or too shallow, its rocks by biome
/// or chosen, the trees in its way taken away), the Mountain tool (too big for the area, with and without growing,
/// growing on a real world), the Shape tool's empty formula, a stamp, and the Script tool's Run.
/// </summary>
[Collection("World files")]
public class MainWindowToolsTests
{
    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area (ground at 30 m) with these objects.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return (w, s);
    }

    /// <summary>
    /// A beech at grid point (64, 64): the area's middle (grid x = world x + 32).
    /// </summary>
    private static WorldScene.Thing Beech() => new(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 1, false);

    [AvaloniaFact]
    public void ACaveIsDugRoofedAndTakesTheTreesInItsWayAway()
    {
        var (w, s) = Open(Beech());
        var path = w.View.Path;
        // Fewer than two points: nothing to apply.
        path.Points.Add(new Vector2(20, 64));
        w.ApplyPath();
        Assert.Equal("Draw a line with at least two points first.", w.MessageText.Text);
        path.Points.Add(new Vector2(108, 64));
        path.Act = PathTool.Action.Cave;
        path.Width = 8;
        path.Soft = 2.5f;
        path.CaveDepth = 7.5f;
        path.Headroom = 4.5f;
        int steps = s.UndoList.Count;
        w.ApplyPath();
        // One undo step; the trench is deeper than the ground was, the beech in it is gone, boulders roof it.
        Assert.Equal(steps + 1, s.UndoList.Count);
        Assert.StartsWith("Dug a cave", w.MessageText.Text);
        Assert.Contains("roofed with", w.MessageText.Text);
        Assert.Contains("1 tree(s) and rock(s) in the way taken away", w.MessageText.Text);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.Contains(s.Scene.Things, t => !t.Gone && t.Prefab == StableHash.Of("rock4_forest"));
        Assert.True(s.Ground.HeightOf(64 * s.Ground.W + 64) < 30 - 4);
    }

    [AvaloniaFact]
    public void ACaveWithChosenRocksAndOneTooShallowForARoof()
    {
        var (w, s) = Open();
        var path = w.View.Path;
        path.Points.AddRange([new Vector2(20, 64), new Vector2(108, 64)]);
        path.Act = PathTool.Action.Cave;
        // Chosen rather than by biome (the tests' copy of the game's data has the forest's boulder alone).
        path.Rock = PathTool.CaveRock.Forest;
        w.ApplyPath();
        Assert.Contains(s.Scene.Things, t => !t.Gone && t.Prefab == StableHash.Of("rock4_forest"));
        // No deeper than the headroom: only the trench.
        path.Points.Clear();
        path.Points.AddRange([new Vector2(20, 30), new Vector2(108, 30)]);
        path.CaveDepth = 3;
        path.Headroom = 4.5f;
        int things = s.Scene.Things.Count;
        w.ApplyPath();
        Assert.StartsWith("Dug the cave's trench, but it is nowhere deep enough for a roof", w.MessageText.Text);
        Assert.Equal(things, s.Scene.Things.Count);
    }

    [AvaloniaFact]
    public async Task AMountainTooBigForTheAreaIsRefusedAndOneThatFitsIsPlaced()
    {
        var (w, s) = Open();
        var m = w.MountainPanel;
        m.RadiusSlider.Value = 30;
        // Near the edge: it would reach out of the area.
        int steps = s.UndoList.Count;
        await w.PutMountain(5, 5);
        Assert.Contains("does not fit in the open area", w.MessageText.Text);
        Assert.Equal(steps, s.UndoList.Count);
        // In the middle, without growing anything.
        m.GrowBox.IsChecked = false;
        await w.PutMountain(64, 64);
        Assert.StartsWith($"Placed a {m.Preset.Name.ToLowerInvariant()}", w.MessageText.Text);
        Assert.EndsWith("Ctrl+Z takes it back; saving turns it into ground discs.", w.MessageText.Text);
        Assert.Equal(steps + 1, s.UndoList.Count);
        Assert.True(s.Ground.HeightOf(64 * s.Ground.W + 64) > 30 + 1);
        // Growing, where there is no world generator: the mountain is placed, nothing grows.
        m.GrowBox.IsChecked = true;
        s.Undo();
        await w.PutMountain(64, 64);
        Assert.EndsWith("Ctrl+Z takes it back (twice with what grew); saving turns it into ground discs.", w.MessageText.Text);
        Assert.DoesNotContain("Grew", w.MessageText.Text);
    }

    [AvaloniaFact]
    public async Task AMountainOnAWorldGrowsTheBiomesTreesAndRocks()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
            await w.EditArea(0, 0, 3);
            var s = w.Session!;
            w.MountainPanel.RadiusSlider.Value = 25;
            w.MountainPanel.GrowBox.IsChecked = true;
            int steps = s.UndoList.Count;
            await w.PutMountain((s.Ground.W - 1) / 2f, (s.Ground.H - 1) / 2f);
            Assert.Contains("Grew ", w.MessageText.Text);
            // The mountain, then what grew on it: two undo steps.
            Assert.Equal(steps + 2, s.UndoList.Count);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AMountainUndoneWhileItsTreesAreWorkedOutGrowsNothing()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
            await w.EditArea(0, 0, 3);
            var s = w.Session!;
            w.MountainPanel.RadiusSlider.Value = 25;
            w.MountainPanel.GrowBox.IsChecked = true;
            int steps = s.UndoList.Count;
            var putting = w.PutMountain((s.Ground.W - 1) / 2f, (s.Ground.H - 1) / 2f);
            // Ctrl+Z while the game's trees are worked out: they would grow in the air where the mountain was.
            s.Undo();
            await putting;
            Assert.EndsWith("Nothing grew on it: the area changed while its trees were worked out.", w.MessageText.Text);
            Assert.Equal(steps, s.UndoList.Count);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void AShapeOfNothingChangesNothingAndAStampSaysHowFar()
    {
        var (w, s) = Open();
        w.ShapePanel.FormulaBox.Text = "0";
        w.PutShape(64, 64);
        Assert.Equal("The formula gives 0 everywhere within the radius: nothing changed.", w.MessageText.Text);
        // A formula that does not read: fix it first.
        w.ShapePanel.FormulaBox.Text = "h * ";
        w.PutShape(64, 64);
        Assert.Equal("Fix the formula first.", w.MessageText.Text);
        // A stamp, raised then lowered.
        w.Tools.Choose(BrushTool.Raise);
        int steps = s.UndoList.Count;
        w.StampAt(64, 64);
        Assert.Matches(@"^Stamped [\d.]+ m up\. Ctrl\+Z undoes it\.$", w.MessageText.Text);
        w.Tools.Choose(BrushTool.Lower);
        w.StampAt(64, 64);
        Assert.Matches(@"^Stamped [\d.]+ m down\.", w.MessageText.Text);
        Assert.Equal(steps + 2, s.UndoList.Count);
    }

    [AvaloniaFact]
    public async Task TheScriptToolRunsThePanelsScriptOnTheOpenArea()
    {
        var empty = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        empty.Show();
        await empty.RunScript();
        Assert.Equal("Open an area first: a script works on the open area.", empty.MessageText.Text);
        var (w, s) = Open();
        // World metres: grid point 64 is at x = z = 32.
        w.ScriptPanel.CodeBox.Text = "Ground.Raise(32, 32, 10);";
        int steps = s.UndoList.Count;
        await w.RunScript();
        Assert.StartsWith("The script changed", w.MessageText.Text);
        Assert.Contains("The script changed", w.ScriptPanel.Output.Text);
        Assert.Equal(steps + 1, s.UndoList.Count);
        Assert.True(s.Ground.HeightOf(64 * s.Ground.W + 64) > 30);
    }
}
