using System.Numerics;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What is waiting to be saved once the world was saved (WorldSession.Pending), on a copy of the test world.
/// </summary>
[Collection("World files")]
public class WorldSessionHistoryTests
{
    // After a save, an undo is pending as what the next save does: a tree placed and saved, then undone, is taken out
    // of the world (1 deleted); a tree deleted and saved, then undone, is put back (1 added). They were counted the
    // other way round, so Save to world asked to write "1 added" for a tree it was about to take out.
    [Fact]
    public void AfterASaveAnUndoCountsAsWhatTheNextSaveDoes()
    {
        string dir = Fixtures.CopyFixture();
        var s = WorldScene.Load(dir, 0, 0, 1).Session!;
        try
        {
            int tree = s.Scene.Things.FindIndex(t => !t.Piece);
            var at = s.Scene.Things[tree];
            s.Commit("Placed", null, [], [(new NewObject(0, at.Prefab, at.Position + new Vector3(3, 0, 3), Vector3.Zero, 1f), false)]);
            Assert.True(s.Save().Saved);
            s.Undo();
            Assert.Equal((0, 1, 0, 0), s.Pending);
            s.Redo();
            Assert.Equal((0, 0, 0, 0), s.Pending);
            s.Delete([tree]);
            Assert.True(s.Save().Saved);
            s.Undo();
            Assert.Equal((0, 0, 1, 0), s.Pending);
        }
        finally
        {
            s.Scene.Owner!.Dispose();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
