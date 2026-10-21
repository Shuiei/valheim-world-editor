using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.TestThings;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session's rules for steps: deleting what is already gone makes no step, a shape whose formula fails at some points
/// counts them and leaves them, the changed rectangle is given once, the game's changes wait for a stroke to end, things moved,
/// turned and sized in one step, and steps marked applied up to the first the game lacks.
/// </summary>
public class EditSessionStepsTests
{
    [Fact]
    public void DeletingWhatIsGoneMakesNoStep()
    {
        var s = TestScenes.Flat(1, Rock());
        s.Delete([0]);
        Assert.Single(s.UndoList);
        Assert.Equal("Deleted 1", s.UndoList[0].Label);
        Assert.Equal("1 removed", s.UndoList[0].Describe());
        s.Delete([0]);
        Assert.Single(s.UndoList);
    }

    // A step picked in the history, then undone or dropped before the action ran (the panel waits for the user to
    // allow it, while Claude undoes): going back to it, forward to it or taking it out does nothing.
    [Fact]
    public void AStepNoLongerInTheHistoryIsLeftAlone()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "A");
        float afterA = s.Ground.HeightOf(32 * s.Ground.W + 32);
        s.Shape(32, 32, _ => 3, 3, 0, "B");
        var b = s.UndoList[^1];
        Assert.True(s.Undo());
        Assert.Equal("That change is no longer in the history to undo: nothing was removed.", s.RemoveChange(b).Shown);
        Assert.Equal(afterA, s.Ground.HeightOf(32 * s.Ground.W + 32));
        s.Shape(32, 32, _ => 2, 3, 0, "C");
        // B is dropped (a new step clears the redo list): neither undoes A and C nor redoes anything.
        s.BackTo(b);
        Assert.Equal(["A", "C"], s.UndoList.Select(c => c.Label));
        Assert.True(s.Undo());
        s.ForwardTo(b);
        Assert.Equal(["A"], s.UndoList.Select(c => c.Label));
        Assert.Equal(["C"], s.RedoList.Select(c => c.Label));
    }

    [Fact]
    public void GoingBackOrForwardSeveralStepsTellsOfTheEditOnce()
    {
        var s = TestScenes.Flat(1);
        foreach (string label in new[] { "A", "B", "C", "D" })
        {
            s.Shape(32, 32, _ => 1, 3, 0, label);
        }
        var a = s.UndoList[0];
        var d = s.UndoList[^1];
        int changed = 0, made = 0;
        s.Changed += () => changed++;
        s.EditMade += () => made++;
        s.BackTo(a);
        Assert.Equal(["A"], s.UndoList.Select(c => c.Label));
        Assert.Equal((1, 1), (changed, made));
        s.ForwardTo(d);
        Assert.Equal(["A", "B", "C", "D"], s.UndoList.Select(c => c.Label));
        Assert.Equal((2, 2), (changed, made));
        // Already there: nothing to tell.
        s.ForwardTo(d);
        Assert.Equal((2, 2), (changed, made));
    }

    [Fact]
    public void AFormulaFailingAtSomePointsLeavesThemAsTheyWere()
    {
        var s = TestScenes.Flat(1);
        // Fails east of the middle, 2 m elsewhere.
        var (touched, _, bad) = s.Shape(32, 32, e => e.Vars["x"] > 0 ? throw new InvalidOperationException("east") : 2, 6, 0, "Shape");
        Assert.True(bad > 0);
        Assert.True(touched > 0);
        Assert.Equal(30, s.Ground.HeightOf(32 * s.Ground.W + 36));
        Assert.Equal(32, s.Ground.HeightOf(32 * s.Ground.W + 28), 3);
    }

    [Fact]
    public void TheChangedRectangleIsGivenOnce()
    {
        var s = TestScenes.Flat(1);
        Assert.Null(s.TakeDirty());
        s.Shape(32, 32, _ => 1, 3, 0, "Shape");
        var (x0, z0, x1, z1) = s.TakeDirty()!.Value;
        Assert.True(x0 <= 29 && z0 <= 29 && x1 >= 35 && z1 >= 35);
        Assert.Null(s.TakeDirty());
    }

    [Fact]
    public void TheGamesChangesWaitForAStrokeToEnd()
    {
        var s = TestScenes.Flat(1);
        var none = new WorldSession.Followed(new WorldSave.Merged([], [], [], []), [], [], []);
        s.BeginStroke(BrushTool.Raise, 32, 32);
        Assert.False(s.TakeGameChanges(none));
        s.EndStroke();
        Assert.True(s.TakeGameChanges(none));
    }

    [Fact]
    public void ThingsAreMovedTurnedAndSizedInOneStep()
    {
        var s = TestScenes.Flat(1, Rock());
        int moved = s.Transform([(0, new Vector3(5, 30, 0), new Vector3(0, 90, 0), 2f)], "Moved")[0];
        Assert.True(s.Scene.Things[0].Gone);
        var t = s.Scene.Things[moved];
        Assert.Equal((new Vector3(5, 30, 0), new Vector3(0, 90, 0), 2f), (t.Position, t.Rotation, t.Scale));
        Assert.Single(s.UndoList);
        Assert.Equal("Moved", s.UndoLabel);
    }

    // After a partial Apply live, steps are applied oldest first while the game has all they did; one it lacks stops
    // it (Discard undoes from the end), and a step applied before does not.
    [Fact]
    public void StepsAreAppliedUpToTheFirstTheGameLacks()
    {
        var s = TestScenes.Flat(1, Rock(), Rock() with { Id = 4 });
        var two = Formula.Compile("2", []);
        s.Delete([0]);
        s.Shape(32, 32, two, 3, 0, "raise");
        s.Delete([1]);
        s.Shape(20, 20, two, 3, 0, "raise");
        var steps = s.UndoList;
        Assert.Equal(4, steps.Count);
        steps[0].Applied = true;
        EditSession.MarkSent(steps, c => c.Things.Length == 0);
        Assert.Equal([true, true, false, false], steps.Select(c => c.Applied));
    }
}
