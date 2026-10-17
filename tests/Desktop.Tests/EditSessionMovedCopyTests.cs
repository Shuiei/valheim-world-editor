using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.TestThings;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The copy that stands for a moved thing: what it keeps of the thing, and what a copy starts from.
/// </summary>
public class EditSessionMovedCopyTests
{
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
}
