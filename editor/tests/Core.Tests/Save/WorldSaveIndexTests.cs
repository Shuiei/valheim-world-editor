using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The live world's lookups by ZDOID and by zone: an object is taken out of both when it is untracked, and put back
/// into both when it is tracked again.
/// </summary>
public class WorldSaveIndexTests
{
    /// <summary>
    /// A live world from the snapshot, with its lookups built, and the first object that is not terrain.
    /// </summary>
    private static (WorldSave World, int Id) LiveWorldWithIndex()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        PrivateCall.Instance(world, "Index");
        int id = Enumerable.Range(0, world.ObjectRefs.Count).First(i => !world.ObjectRefs[i].IsTerrain);
        return (world, id);
    }

    [Fact]
    public void UntrackingAnObjectTakesItOutOfTheZdoidAndZoneLookups()
    {
        var (world, id) = LiveWorldWithIndex();
        var o = world.ObjectRefs[id];

        PrivateCall.Instance(world, "Untrack", id);

        var byLiveId = (Dictionary<(long, uint), int>)PrivateCall.Field(world, "_byLiveId")!;
        var byZone = (Dictionary<(int, int), HashSet<int>>)PrivateCall.Field(world, "_byZone")!;
        Assert.False(byLiveId.ContainsKey(o.LiveId));
        Assert.DoesNotContain(id, byZone[o.Zone]);
    }

    [Fact]
    public void TrackingAnObjectAgainPutsItBackIntoTheZdoidAndZoneLookups()
    {
        var (world, id) = LiveWorldWithIndex();
        var o = world.ObjectRefs[id];
        PrivateCall.Instance(world, "Untrack", id);

        PrivateCall.Instance(world, "Track", id);

        var byLiveId = (Dictionary<(long, uint), int>)PrivateCall.Field(world, "_byLiveId")!;
        var byZone = (Dictionary<(int, int), HashSet<int>>)PrivateCall.Field(world, "_byZone")!;
        Assert.Equal(id, byLiveId[o.LiveId]);
        Assert.Contains(id, byZone[o.Zone]);
    }
}
