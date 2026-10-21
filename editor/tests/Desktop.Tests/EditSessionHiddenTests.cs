using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Objects the scene does not hold as things (mods' data holders) changed with the history (EditSession.CommitObjects):
/// added and taken away by id in one step with things, undone, redone and taken out of the history, and said in the
/// step's line; a step that changes nothing is no step.
/// </summary>
public sealed class EditSessionHiddenTests
{
    private static readonly int Fleet = StableHash.Of(ExampleMod.Fleet), Beech = StableHash.Of("Beech1");

    private static NewObject Holder(float x) => new(0, Fleet, new Vector3(x, 30, 5), Vector3.Zero, 1, Fresh: false, Raw: new ZdoData { Prefab = Fleet }.Serialize());

    [Fact]
    public void HiddenObjectsAreAddedAndTakenAwayWithTheHistory()
    {
        var s = TestScenes.Flat(1, TestScenes.Beech(4, new Vector3(1, 30, 1)));
        int things = s.Scene.Things.Count;
        var added = s.CommitObjects("Fleet", [0], [(new NewObject(0, Beech, new Vector3(2, 30, 2), Vector3.Zero, 1), false)], [7], [Holder(3)]);
        Assert.Single(added);
        Assert.Equal(things + 1, s.Scene.Things.Count);
        var step = Assert.Single(s.UndoList);
        int holder = Assert.Single(step.Hidden, h => h.Before).Id;
        Assert.Equal([(7, false, true), (holder, true, false)], step.Hidden);
        Assert.False(s.Edits.IsGone(holder));
        Assert.True(s.Edits.IsGone(7));
        Assert.Equal($"{Lang.T("history.change.new", 2)} · {Lang.T("history.change.removed", 2)}", step.Describe());

        Assert.True(s.Undo());
        Assert.True(s.Edits.IsGone(holder));
        Assert.False(s.Edits.IsGone(7));
        Assert.True(s.Redo());
        Assert.False(s.Edits.IsGone(holder));
        Assert.True(s.Edits.IsGone(7));

        // Taken out of the history: the holder goes, the saved object comes back.
        s.RemoveChange(step);
        Assert.True(s.Edits.IsGone(holder));
        Assert.False(s.Edits.IsGone(7));
        Assert.Equal([(7, true, false), (holder, false, true)], s.UndoList[^1].Hidden);
    }

    [Fact]
    public void AStepThatChangesNothingIsNoStep()
    {
        var s = TestScenes.Flat(1);
        s.Edits.SetDeleted([9], true);
        Assert.Empty(s.CommitObjects("Nothing", [], [], [9], []));
        Assert.Empty(s.UndoList);
        Assert.Empty(s.CommitObjects("More", [], [], [], [Holder(1)]));
        Assert.Equal(8, Assert.Single(s.UndoList).Bytes);
    }
}
