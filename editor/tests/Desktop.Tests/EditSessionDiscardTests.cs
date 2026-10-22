using System.Numerics;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Discard (EditSession.UndoUnapplied) once steps were saved, and the saved mark of the steps undone (EditSession.Save),
/// on a copy of the test world.
/// </summary>
[Collection("World files")]
public class EditSessionDiscardTests
{
    /// <summary>A tree placed beside the area's first one, as one step.</summary>
    private static void Plant(EditSession s)
    {
        var at = s.Scene.Things.First(t => !t.Piece);
        s.Commit("Placed", null, [], [(new NewObject(0, at.Prefab, at.Position + new Vector3(3, 0, 3), Vector3.Zero, 1f), false)]);
    }

    // A saved step undone is waiting too: Discard puts it back. It dropped it from the redo list instead, leaving its
    // undo waiting ("1 deleted") with nothing to redo it.
    [Fact]
    public void DiscardRedoesASavedStepThatWasUndone()
    {
        string dir = Fixtures.CopyFixture();
        var s = WorldScene.Load(dir, 0, 0, 1).Session!;
        try
        {
            Plant(s);
            Assert.True(s.Save().Saved);
            s.Undo();
            Assert.Equal((0, 1, 0, 0), s.Pending);
            Assert.Equal(0, s.UndoUnapplied());
            Assert.Equal((0, 0, 0, 0), s.Pending);
            Assert.Single(s.UndoList);
            Assert.Empty(s.RedoList);
        }
        finally
        {
            s.Scene.Owner!.Dispose();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    // Undone, then saved: the step is not in the world any more, so it is not marked saved (History's "applied"), and
    // redone it waits; Discard then undoes it again.
    [Fact]
    public void AStepUndoneWhenTheWorldIsSavedIsNoLongerMarkedSaved()
    {
        string dir = Fixtures.CopyFixture();
        var s = WorldScene.Load(dir, 0, 0, 1).Session!;
        try
        {
            Plant(s);
            Assert.True(s.Save().Saved);
            s.Undo();
            Assert.True(s.Save().Saved);
            Assert.False(Assert.Single(s.RedoList).Applied);
            s.Redo();
            Assert.Equal((0, 0, 1, 0), s.Pending);
            Assert.Equal(1, s.UndoUnapplied());
            Assert.Equal((0, 0, 0, 0), s.Pending);
        }
        finally
        {
            s.Scene.Owner!.Dispose();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
