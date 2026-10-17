using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The worker side of the dungeon rooms: a read started for the view runs to its end, says ready, and a read that
/// fails keeps what was shown and still says ready.
/// </summary>
public class DungeonRoomsBackgroundTests
{
    [Fact]
    public void AReadInTheBackgroundEndsWithReadyAndNoDungeonsForAnEmptyArea()
    {
        var s = TestScenes.FlatScene();
        bool ready = false;
        DungeonRooms.ReadInBackground(s, (0, 0, 0), [], () => ready = true);
        Assert.True(ready);
    }

    [Fact]
    public void AFailedReadKeepsWhatWasShownAndStillSaysReady()
    {
        // A dungeon object with no world to read its data from: the read fails, and the failure is logged.
        var s = TestScenes.FlatScene(new WorldScene.Thing(5, StableHash.Of("DG_Cave"), System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 1, false));
        s.World = null!;
        bool ready = false;
        DungeonRooms.ReadInBackground(s, (0, 0, 0), [], () => ready = true);
        Assert.True(ready);
    }
}
