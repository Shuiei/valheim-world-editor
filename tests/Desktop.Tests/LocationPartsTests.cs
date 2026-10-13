using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The locations whose parts the 3D view draws (LocationParts): the area's location proxies, but runestones, the
/// ground discs of No limit ground, proxies outside the area and proxies deleted in this session; found again when
/// the edits or the save's list change.
/// </summary>
public class LocationPartsTests
{
    private static readonly int Camp = StableHash.Of("Vendor_BlackForest"), Crypt = StableHash.Of("Crypt2");

    [Fact]
    public void TheAreasLocationProxiesAreDrawnButRunestonesDiscsAndDeletedOnes()
    {
        var session = TestScenes.Flat();
        var s = session.Scene;
        var world = s.World;
        void Proxy(int location, Vector3 at, float turn = 0)
        {
            world.Placed.Add(new PlacedObject(WorldSave.LocationProxyPrefab, location, at, new Vector3(0, turn, 0), 0));
            world.ObjectRefs.Add(new ObjectRef { File = new ChunkFile(), Start = 0, Prefab = WorldSave.LocationProxyPrefab, Position = at });
        }
        Proxy(Camp, new Vector3(10, 30, 10), 45);
        Proxy(Crypt, new Vector3(70, 30, 70));
        Proxy(StableHash.Of("Runestone_Meadows"), new Vector3(20, 30, 20));
        Proxy(Uplift.Disc, new Vector3(30, 30, 30));
        // Outside the area (zones 0 and 1: x and z from -32 to 96).
        Proxy(Camp, new Vector3(100, 30, 10));
        // A proxy whose location is not known, and an object that shapes the ground but is no proxy.
        world.Placed.Add(new PlacedObject(WorldSave.LocationProxyPrefab, 0, new Vector3(5, 30, 5), Vector3.Zero, 0));
        world.Placed.Add(new PlacedObject(StableHash.Of("rock4_coast"), 0, new Vector3(6, 30, 6), Vector3.Zero, 0));

        var shown = LocationParts.Of(s);
        Assert.Equal([new(Camp, new Vector3(10, 30, 10), new Vector3(0, 45, 0)), new(Crypt, new Vector3(70, 30, 70), Vector3.Zero)], shown);
        // Nothing changed: what was found is kept.
        Assert.Same(shown, LocationParts.Of(s));

        // The crypt's proxy deleted: found again without it.
        session.Edits.SetDeleted([1], true);
        Assert.Equal([Camp], LocationParts.Of(s).Select(l => l.Location));
        // One more in the save's list (live): found again with it.
        lock (world.Sync)
        {
            Proxy(Crypt, new Vector3(-10, 30, -10));
        }
        Assert.Equal([Camp, Crypt], LocationParts.Of(s).Select(l => l.Location));
    }
}
