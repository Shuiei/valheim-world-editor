using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session's smaller rules: deleting what is already gone makes no step, a shape whose formula fails at some
/// points counts them and leaves them, the changed rectangle is given once, the game's changes wait for a stroke
/// to end, things moved, turned and sized in one step, the copy that stands for a moved thing, what a copy starts
/// from, a step keyed by itself, zones marked for reset, unmarked and marked again with other options, and steps
/// marked applied up to the first the game lacks.
/// </summary>
public class EditSessionStepsTests
{
    private static WorldScene.Thing Rock() => new(3, StableHash.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);

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
        Assert.Equal("That change is no longer in the history to undo: nothing was removed.", s.RemoveChange(b));
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
    public void TakingAShapeOutOrClearingItsZoneRefreshesOnlyAroundIt()
    {
        var s = TestScenes.Flat(3);
        void ShapeOnly()
        {
            var (x0, z0, x1, z1) = s.TakeDirty()!.Value;
            Assert.True(x0 >= 27 && z0 >= 27 && x1 <= 37 && z1 <= 37, $"{(x0, z0, x1, z1)}");
        }
        s.Shape(32, 32, _ => 1, 3, 0, "Shape");
        s.TakeDirty();
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]));
        ShapeOnly();
        s.Shape(32, 32, _ => 1, 3, 0, "Shape");
        s.TakeDirty();
        s.MarkResets([(0, 0)], keepBuildings: true, ground: true, "Reset");
        ShapeOnly();
        Assert.Equal(30, s.Ground.HeightOf(32 * s.Ground.W + 32));
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

    [Fact]
    public void AMovedCopyKeepsWhatTheThingHolds()
    {
        var s = TestScenes.Flat(1, Rock());
        // A thing of the world: copied from it, with its data.
        var (fromWorld, piece) = s.MovedCopy(0, new Vector3(1, 30, 1), Vector3.Zero, 0);
        Assert.Equal((3, false, false), (fromWorld.SourceId, fromWorld.Fresh, piece));
        // One added in the session: its values and its own source go with it.
        var text = new Editing.ObjectField("strings", StableHash.Of("text"), "Hello");
        int sign = s.Commit("Put", null, [], [(new Editing.NewObject(0, StableHash.Of("sign"), new Vector3(20, 30, 20), Vector3.Zero, 0, 3, false, Data: [text]), true)])[0];
        var (copy, isPiece) = s.MovedCopy(sign, new Vector3(25, 30, 20), new Vector3(0, 90, 0), 2);
        Assert.Equal((3, false, true, 2f), (copy.SourceId, copy.Fresh, isPiece, copy.Scale));
        Assert.Equal([text], copy.Data!);
    }

    [Fact]
    public void ACopyStartsFromTheSavedObjectItComesFrom()
    {
        var s = TestScenes.Flat(1, Rock());
        // A thing of the world: itself; one added from it: the same; one put new: none.
        int copied = s.Commit("Put", null, [], [(new Editing.NewObject(0, StableHash.Of("Rock_4"), new Vector3(20, 30, 20), Vector3.Zero, 0, 3, false), false)])[0];
        int put = s.Commit("Put", null, [], [(new Editing.NewObject(0, StableHash.Of("Rock_4"), new Vector3(25, 30, 20), Vector3.Zero, 0, null, true), false)])[0];
        Assert.Equal<int?>([3, 3, null], [.. new[] { 0, copied, put }.Select(i => s.CopySourceOf(s.Scene.Things[i]).SourceId)]);
    }

    [Fact]
    public void AStepKeysByItselfWhateverIsSetOnItLater()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "A");
        var c = s.UndoList[0];
        var ids = new Dictionary<EditSession.Change, int[]> { [c] = [7] };
        c.Applied = true;
        c.Removed = true;
        Assert.Equal([7], ids[c]);
        // A copy with the same values is another step.
        Assert.NotEqual(c, c with { });
    }

    [Fact]
    public void ZonesMarkedForResetLoseTheirGroundEditsInOneStep()
    {
        var s = TestScenes.Flat(2);
        s.Shape(10, 10, _ => 1, 3, 0, "Bump");
        float H() => s.Ground.HeightOf(10 * s.Ground.W + 10);
        Assert.Equal(31, H(), 3);
        s.MarkResets([(0, 0), (1, 0)], keepBuildings: true, ground: true, "Reset");
        Assert.Equal([new Editing.ZoneReset(0, 0, true, true), new Editing.ZoneReset(1, 0, true, true)], s.Resets.OrderBy(r => r.X));
        Assert.Equal(30, H(), 3);
        Assert.True(s.Undo());
        Assert.Empty(s.Resets);
        Assert.Equal(31, H(), 3);
        // Without ground, the ground stays.
        s.MarkResets([(0, 0)], keepBuildings: false, ground: false, "Reset");
        Assert.Equal(31, H(), 3);
        Assert.Equal(new Editing.ZoneReset(0, 0, false, false), Assert.Single(s.Resets));
    }

    [Fact]
    public void OnlyMarkedZonesAreUnmarked()
    {
        var s = TestScenes.Flat(2);
        Assert.Equal(0, s.UnmarkResets([(0, 0)], "Unmark"));
        Assert.Empty(s.UndoList);
        s.MarkResets([(0, 0), (1, 1)], true, false, "Reset");
        Assert.Equal(1, s.UnmarkResets([(0, 0), (0, 0), (1, 0)], "Unmark"));
        Assert.Equal((1, 1), (s.Resets.Single().X, s.Resets.Single().Z));
        Assert.Equal("Unmark", s.UndoLabel);
    }

    // Taking out a step keeps the modified flag a later step set at its points: here a paint, then a flatten back to
    // the original height (modified, no level). Removing the paint cleared the flatten's flag.
    [Fact]
    public void RemovingAStepKeepsALaterStepsModifiedFlag()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        s.EditGround("Paint", g =>
        {
            g.PMod[p] = 1;
            g.Paint[p * 4] = 1;
            return ([p], (31, 31, 33, 33));
        });
        s.EditGround("Flatten", g =>
        {
            g.SetHeight(p, g.HeightOf(p));
            return ([p], (31, 31, 33, 33));
        });
        Assert.Equal(1, s.Ground.Mod[p]);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[0]));
        Assert.Equal(1, s.Ground.Mod[p]);
    }

    // Marking a marked zone again with other options: undo, redo and removing the step each bring back the options
    // of their side, not the newest ones.
    [Fact]
    public void MarkingAgainWithOtherOptionsIsUndoneToTheOldOptions()
    {
        var s = TestScenes.Flat(2);
        var kept = new Editing.ZoneReset(0, 0, KeepBuildings: true, Ground: false);
        var wiped = new Editing.ZoneReset(0, 0, KeepBuildings: false, Ground: false);
        s.MarkResets([(0, 0)], keepBuildings: true, ground: false, "Keep");
        s.MarkResets([(0, 0)], keepBuildings: false, ground: false, "Wipe");
        Assert.Equal(wiped, Assert.Single(s.Resets));
        Assert.Equal("1 zone reset", s.UndoList[^1].Describe());
        Assert.True(s.Undo());
        Assert.Equal(kept, Assert.Single(s.Resets));
        Assert.True(s.Redo());
        Assert.Equal(wiped, Assert.Single(s.Resets));
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]));
        Assert.Equal(kept, Assert.Single(s.Resets));
        Assert.True(s.Undo());
        Assert.Equal(wiped, Assert.Single(s.Resets));
    }

    // A move replaces its object by a copy. Removing the move while a later step moved the copy again would bring the
    // original back beside the later copy (two rocks): it is refused, and neither rock changes.
    [Fact]
    public void RemovingAMoveWhoseCopyMovedAgainIsRefused()
    {
        var s = TestScenes.Flat(1, Rock());
        int first = s.Move([(0, new Vector3(4, 30, 0), Vector3.Zero)], "Claude: moved")[0];
        int second = s.Move([(first, new Vector3(8, 30, 0), Vector3.Zero)])[0];
        Assert.StartsWith("That change cannot be removed:", s.RemoveChange(s.UndoList[^2]));
        Assert.False(s.UndoList[^2].Removed);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[first].Gone);
        Assert.False(s.Scene.Things[second].Gone);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
        // Taking the later move out first makes the first one removable.
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]));
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^3]));
        Assert.False(s.Scene.Things[0].Gone);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
    }

    // A copy that went in the game (live follow-up, no history step) does not stand in the way: removing the move
    // brings the original back and the copy stays gone, as before.
    [Fact]
    public void RemovingAMoveWhoseCopyVanishedInTheGameIsAllowed()
    {
        var s = TestScenes.Flat(1, Rock());
        int copy = s.Move([(0, new Vector3(4, 30, 0), Vector3.Zero)], "Claude: moved")[0];
        Assert.True(s.TakeGameChanges(new WorldSession.Followed(new WorldSave.Merged([], [], [], []), [s.Scene.Things[copy].Id], [], [])));
        Assert.True(s.Scene.Things[copy].Gone);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]));
        Assert.False(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[copy].Gone);
    }

    // Removing an older reset step while a later step marked the zone again keeps the later marking.
    [Fact]
    public void RemovingAnOlderMarkingKeepsALaterOne()
    {
        var s = TestScenes.Flat(2);
        var later = new Editing.ZoneReset(0, 0, KeepBuildings: true, Ground: false);
        s.MarkResets([(0, 0)], keepBuildings: false, ground: false, "Wipe");
        s.MarkResets([(0, 0)], keepBuildings: true, ground: false, "Keep");
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^2]));
        Assert.Equal(later, Assert.Single(s.Resets));
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
