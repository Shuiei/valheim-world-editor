using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The zone controls a generated world is given (WorldCreator.PlaceZoneControls): every zone in the radius that is not
/// left to the game is marked generated and gets one, and a zone left to the game gets neither.
/// </summary>
public class WorldCreatorZoneControlTests
{
    private static int PlaceZoneControls(ZoneDb db, WorldCreator.ChunkBatch batch, HashSet<(int X, int Z)> left, int radius) =>
        (int)PrivateCall.Static(typeof(WorldCreator), "PlaceZoneControls", db, batch, left, radius)!;

    [Fact]
    public void EveryZoneInTheRadiusIsGeneratedWhenNoneIsLeftToTheGame()
    {
        var db = new ZoneDb();
        int zones = PlaceZoneControls(db, new WorldCreator.ChunkBatch(), [], 1);
        Assert.Equal(9, zones);
        Assert.Equal(9, db.Generated.Count);
        Assert.Contains(((short)-1, (short)1), db.Generated);
    }

    [Fact]
    public void AZoneLeftToTheGameGetsNoControlAndIsNotGenerated()
    {
        var db = new ZoneDb();
        int zones = PlaceZoneControls(db, new WorldCreator.ChunkBatch(), [(0, 0)], 1);
        Assert.Equal(8, zones);
        Assert.DoesNotContain(((short)0, (short)0), db.Generated);
        Assert.Contains(((short)1, (short)0), db.Generated);
    }
}
