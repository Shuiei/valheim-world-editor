using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.TestThings;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Taking steps out of the history: what a removal keeps of later steps (their modified flags, their marks on zones
/// and their moves), and the refusals when a later step depends on it.
/// </summary>
public class EditSessionRemovalTests
{
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
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[0]).Shown);
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
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]).Shown);
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
        Assert.StartsWith("That change cannot be removed:", s.RemoveChange(s.UndoList[^2]).Shown);
        Assert.False(s.UndoList[^2].Removed);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[first].Gone);
        Assert.False(s.Scene.Things[second].Gone);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
        // Taking the later move out first makes the first one removable.
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]).Shown);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^3]).Shown);
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
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]).Shown);
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
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^2]).Shown);
        Assert.Equal(later, Assert.Single(s.Resets));
    }
}
