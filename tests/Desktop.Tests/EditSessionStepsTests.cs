using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session's smaller rules: deleting what is already gone makes no step, a shape whose formula fails at some
/// points counts them and leaves them, the changed rectangle is given once, the game's changes wait for a stroke
/// to end, and a moved object added in the session keeps its values and takes its new size.
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
    public void AMovedObjectKeepsItsValuesAndTakesItsSize()
    {
        var s = TestScenes.Flat(1);
        var text = new Editing.ObjectField("strings", StableHash.Of("text"), "Hello");
        int sign = s.Commit("Put", null, [], [(new Editing.NewObject(0, StableHash.Of("sign"), new Vector3(20, 30, 20), Vector3.Zero, 0, Data: [text]), true)])[0];
        int moved = s.Transform([(sign, new Vector3(25, 30, 20), new Vector3(0, 90, 0), 2f)], "Moved")[0];
        Assert.True(s.Scene.Things[sign].Gone);
        var t = s.Scene.Things[moved];
        Assert.Equal((new Vector3(25, 30, 20), new Vector3(0, 90, 0), 2f), (t.Position, t.Rotation, t.Scale));
        Assert.Equal([text], s.Edits.FindAdded(t.Id)!.Data!);
        Assert.Equal("Moved", s.UndoLabel);
    }
}
