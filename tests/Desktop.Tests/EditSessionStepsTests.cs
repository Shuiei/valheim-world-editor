using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session's smaller rules: deleting what is already gone makes no step, a shape whose formula fails at some
/// points counts them and leaves them, the changed rectangle is given once, the game's changes wait for a stroke
/// to end, things moved, turned and sized in one step, and the copy that stands for a moved thing.
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
}
