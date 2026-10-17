using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The world's history across its areas: steps made in one area are kept aside while another is open (not dropped,
/// as they were), come back where they were made, and go to disk and back each in its own frame.
/// </summary>
[Collection("World files")]
public class EditSessionOtherAreasTests
{
    /// <summary>Deletes the copy of the test world (its temporary parent folder).</summary>
    private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

    /// <summary>
    /// Runs the test on its own copy of the test world, which is deleted after.
    /// </summary>
    private static void WithCopy(Action<string> test)
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            test(dir);
        }
        finally
        {
            Done(dir);
        }
    }

    /// <summary>Raises the ground 3 m in a 4 m circle at grid point (32, 32) of the area, as one step with this label.</summary>
    private static void Raise(EditSession s, string label) => s.Shape(32, 32, Formula.Compile("3", []), 4, 0, label);

    /// <summary>The copy's area at (0, 0) with the step "Raise here" made in it: the world's session and that area's.</summary>
    private static (WorldSession Owner, EditSession Here) RaiseHere(string dir)
    {
        var first = WorldScene.Load(dir, 0, 0, 1);
        var owner = first.Owner!;
        var a = first.Session!;
        Raise(a, "Raise here");
        return (owner, a);
    }

    /// <summary>
    /// <see cref="RaiseHere"/>, its history kept with the world, then the area at (3, 3) with the step "Raise there" made in
    /// it: the world's session and both areas.
    /// </summary>
    private static (WorldSession Owner, EditSession Here, EditSession There) RaiseHereThenThere(string dir)
    {
        var (owner, a) = RaiseHere(dir);
        owner.History = a.Export();
        var b = WorldScene.Load(owner, 3, 3, 1).Session!;
        Raise(b, "Raise there");
        return (owner, a, b);
    }

    /// <summary>The height at grid point (32, 32) of the area.</summary>
    private static float Middle(EditSession s) => s.Scene.Heights[32 * s.Scene.W + 32];

    [AvaloniaFact]
    public void StepsMadeInAnotherAreaAreKeptAsideAndComeBackThere()
    {
        WithCopy(dir =>
        {
            var first = WorldScene.Load(dir, 0, 0, 1);
            var owner = first.Owner!;
            var a = first.Session!;
            float was = Middle(a);
            Raise(a, "Raise here");
            Assert.Single(a.UndoList);
            // Leaving for another area (MainWindow keeps the history with the world), where the step does not fit.
            owner.History = a.Export();
            var b = WorldScene.Load(owner, 3, 3, 1).Session!;
            Assert.Empty(b.UndoList);
            Assert.Equal(1, b.Shelved);
            Raise(b, "Raise there");
            Assert.Equal("Raise there", Assert.Single(b.UndoList).Label);
            // Both steps go on with the world, the first one in its own frame, oldest first.
            var kept = b.Export();
            Assert.Equal(new[] { "Raise here", "Raise there" }, kept.Undo.Select(c => c.Label).ToArray());
            Assert.NotEqual(kept.FrameOf(kept.Undo[0]), kept.FrameOf(kept.Undo[1]));
            Assert.Equal((0, 0), (kept.FrameOf(kept.Undo[0]).X0, kept.FrameOf(kept.Undo[0]).Z0));
            Assert.Equal((3, 3), (kept.FrameOf(kept.Undo[1]).X0, kept.FrameOf(kept.Undo[1]).Z0));
            // Back in the first area: its step can be undone, the other one is kept aside.
            owner.History = kept;
            var again = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Equal("Raise here", Assert.Single(again.UndoList).Label);
            Assert.Equal(1, again.Shelved);
            Assert.True(again.Undo());
            Assert.Equal(was, Middle(again), 3);
            Assert.Single(again.RedoList);
            // An area holding both: both can be undone there, nothing kept aside.
            owner.History = again.Export();
            var both = WorldScene.LoadBlock(owner, 0, 0, 4, 4).Session!;
            Assert.Equal(0, both.Shelved);
            Assert.Equal("Raise there", Assert.Single(both.UndoList).Label);
            Assert.Equal("Raise here", Assert.Single(both.RedoList).Label);
        });
    }

    [AvaloniaFact]
    public void StepsKeptAsideAndThisAreasGoBackInTheOrderTheyWereMade()
    {
        WithCopy(dir =>
        {
            var (owner, _, b) = RaiseHereThenThere(dir);
            // Back in the first area: its older step is undoable, the newer one kept aside; leaving again, the newer
            // one must still come after it, whichever list each was in.
            owner.History = b.Export();
            var again = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Equal("Raise here", Assert.Single(again.UndoList).Label);
            Assert.Equal(1, again.Shelved);
            var kept = again.Export();
            Assert.Equal(new[] { "Raise here", "Raise there" }, kept.Undo.Select(c => c.Label).ToArray());
            // An area holding both: the newer step is undone first.
            owner.History = kept;
            var both = WorldScene.LoadBlock(owner, 0, 0, 4, 4).Session!;
            Assert.Equal(new[] { "Raise here", "Raise there" }, both.UndoList.Select(c => c.Label).ToArray());
            float near = both.Scene.Heights[32 * both.Scene.W + 32];
            Assert.True(both.Undo());
            Assert.Equal("Raise there", Assert.Single(both.RedoList).Label);
            Assert.Equal(near, both.Scene.Heights[32 * both.Scene.W + 32], 3);
        });
    }

    [AvaloniaFact]
    public void ANewEditDropsTheRedoKeptAsideFromAnotherAreaToo()
    {
        WithCopy(dir =>
        {
            var (owner, a) = RaiseHere(dir);
            Assert.True(a.Undo());
            Assert.Single(a.RedoList);
            // In another area the undone step is kept aside (as a redo); an edit there drops it, as a redo of the same
            // area would be.
            owner.History = a.Export();
            var b = WorldScene.Load(owner, 3, 3, 1).Session!;
            Assert.Equal(1, b.Shelved);
            Raise(b, "Raise there");
            Assert.Equal(0, b.Shelved);
            owner.History = b.Export();
            var again = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Empty(again.RedoList);
            Assert.False(again.CanRedo);
            Assert.Equal(1, again.Shelved);
        });
    }

    [AvaloniaFact]
    public void DiscardDropsTheRedoKeptAsideFromAnotherAreaToo()
    {
        WithCopy(dir =>
        {
            var first = WorldScene.Load(dir, 0, 0, 1);
            var owner = first.Owner!;
            Raise(first.Session!, "Raise here");
            owner.History = first.Session!.Export();
            var b = WorldScene.Load(owner, 3, 3, 1).Session!;
            Raise(b, "Raise there");
            // Back in the first area, its step undone; then in the other area it is kept aside as a redo.
            owner.History = b.Export();
            var a = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.True(a.Undo());
            owner.History = a.Export();
            var there = WorldScene.Load(owner, 3, 3, 1).Session!;
            Assert.Equal(1, there.Shelved);
            // Discard there undoes its own step and drops every redo, the one kept aside too.
            Assert.Equal(1, there.UndoUnapplied());
            Assert.Equal(0, there.Shelved);
            owner.History = there.Export();
            var again = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Empty(again.RedoList);
        });
    }

    [AvaloniaFact]
    public void SavingFromAnotherAreaMarksTheStepsKeptAsideAsSaved()
    {
        WithCopy(dir =>
        {
            var (owner, a) = RaiseHere(dir);
            Assert.Equal(1, a.UnappliedSteps);
            // Saved from another area: the world's pending changes go together, the first area's step among them.
            owner.History = a.Export();
            var b = WorldScene.Load(owner, 3, 3, 1).Session!;
            Raise(b, "Raise there");
            Assert.True(b.Save().Saved);
            Assert.Equal(0, b.UnappliedSteps);
            // Back in the first area: its step is saved too, so Discard has nothing to undo there.
            owner.History = b.Export();
            var again = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.True(Assert.Single(again.UndoList).Applied);
            Assert.Equal(0, again.UnappliedSteps);
            Assert.Equal(0, again.UndoUnapplied());
        });
    }

    [AvaloniaFact]
    public void AStepPartlyInTheAreaStillKeepsTheOlderOnesFromBeingUndoneThere()
    {
        WithCopy(dir =>
        {
            var owner = WorldScene.Load(dir, 0, 0, 1).Owner!;
            // Two zones wide: a raise wholly in the west zone, then one astride the two.
            var wide = WorldScene.LoadBlock(owner, 0, 0, 2, 1).Session!;
            Raise(wide, "Older");
            wide.Shape(64, 32, Formula.Compile("3", []), 6, 0, "Over the edge");
            Assert.Equal(2, wide.UndoList.Count);
            owner.History = wide.Export();
            // The west zone alone: the edge step is partly in, so it and the older step are kept aside, nothing is
            // undoable (undoing the older one under it would not be safe).
            var west = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Empty(west.UndoList);
            Assert.Equal(2, west.Shelved);
            // The two zones again: both fit, in their order.
            owner.History = west.Export();
            var home = WorldScene.LoadBlock(owner, 0, 0, 2, 1).Session!;
            Assert.Equal(new[] { "Older", "Over the edge" }, home.UndoList.Select(c => c.Label).ToArray());
            Assert.Equal(0, home.Shelved);
        });
    }

    // A step kept aside does not bring the things it took away into the area: nothing there shows or undoes them.
    [AvaloniaFact]
    public void AStepKeptAsideAddsNoThingsToTheArea()
    {
        WithCopy(dir =>
        {
            var owner = WorldScene.Load(dir, 0, 0, 1).Owner!;
            // A block of zones: one step raising the ground of zone (1, 0) and deleting a thing of zone (0, 0) (the
            // fixture's things are all there).
            var block = WorldScene.LoadBlock(owner, 0, 0, 4, 4).Session!;
            var world = block.Scene.World!;
            int near = block.Scene.Things.FindIndex(t => !t.Gone && t.Id >= 0 && world.ObjectRefs[t.Id].Zone == (0, 0));
            Assert.True(near >= 0);
            int id = block.Scene.Things[near].Id;
            block.Commit("Both zones", g =>
            {
                int p = 32 * g.W + 96;
                g.SetHeight(p, g.HeightOf(p) + 2);
                return ([p], (95, 31, 97, 33));
            }, [near], []);
            owner.History = block.Export();
            // The thing's zone alone: the step is partly in, so kept aside, and the thing it deleted stays out.
            var area = WorldScene.Load(owner, 0, 0, 1).Session!;
            Assert.Equal(1, area.Shelved);
            Assert.DoesNotContain(area.Scene.Things, t => t.Id == id);
        });
    }

    [AvaloniaFact]
    public void TheHistoryFileKeepsStepsOfSeveralAreasEachWhereItWasMade()
    {
        WithCopy(dir =>
        {
            var (owner, _, b) = RaiseHereThenThere(dir);
            Assert.True(b.Save().Saved);
            // Written at the save (both steps, the first from the other area), read again into an area holding both.
            var restored = HistoryFile.Read(owner);
            Assert.NotNull(restored);
            Assert.Equal(2, restored.Steps);
            owner.History = restored.Kept;
            var both = WorldScene.LoadBlock(owner, 0, 0, 4, 4).Session!;
            Assert.Equal(new[] { "Raise here", "Raise there" }, both.UndoList.Select(c => c.Label).ToArray());
            Assert.Equal(0, both.Shelved);
            // Each stands where it was made: undoing the far one leaves the near one.
            float near = both.Scene.Heights[32 * both.Scene.W + 32];
            Assert.True(both.Undo());
            Assert.Equal(near, both.Scene.Heights[32 * both.Scene.W + 32], 3);
        });
    }
}
