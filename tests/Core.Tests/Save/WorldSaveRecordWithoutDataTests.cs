using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// An object whose data is not read: its bytes are kept to its end, it is listed as an object when it is shown, and it is
/// listed as placed only when it is tracked and is not a location proxy.
/// </summary>
[Collection("World files")]
public class WorldSaveRecordWithoutDataTests
{
    // Ids and a place no saved object of the test world has.
    private const int Id = -7101;
    private static readonly Vector3 Place = new(1000, 2000, 3000), Turn = new(0, 90, 0);

    /// <summary>Records one object without its data through the save's own step, and returns its reference.</summary>
    private static ObjectRef Record(WorldSave world, int prefab, bool tracked, bool shown, long end)
    {
        var file = new ChunkFile();
        var reference = new ObjectRef { File = file, Start = 10 };
        Type site = PrivateMember.Nested(typeof(WorldSave), "ZdoSite");
        object s = PrivateMember.New(site, Id, prefab, Place, Turn, file, 10L, (ushort)0, reference, tracked, shown);
        PrivateMember.Call(typeof(WorldSave), "RecordWithoutData", world, s, end);
        return reference;
    }

    [Fact]
    public void TheObjectEndsWhereItsBytesEnd()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        ObjectRef reference = Record(world, StableHash.Of("Stone"), tracked: false, shown: false, end: 50);
        Assert.Equal(50, reference.End);
    }

    [Fact]
    public void AShownObjectIsListedWithItsPlaceAndTurn()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        int prefab = StableHash.Of("Stone");
        Record(world, prefab, tracked: false, shown: true, end: 50);
        Assert.Contains(world.Objects, o => o == (Id, prefab, Place, Turn, Vector3.Zero));
    }

    [Fact]
    public void ATrackedObjectIsPlacedAtItsPlaceAndTurn()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        int prefab = StableHash.Of("Stone");
        Record(world, prefab, tracked: true, shown: false, end: 50);
        Assert.Contains(world.Placed, p => p == new PlacedObject(prefab, 0, Place, Turn, 0));
    }

    [Fact]
    public void ATrackedLocationProxyIsNotPlaced()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Record(world, WorldSave.LocationProxyPrefab, tracked: true, shown: false, end: 50);
        Assert.DoesNotContain(world.Placed, p => p.Prefab == WorldSave.LocationProxyPrefab && p.Position == Place);
    }

    [Fact]
    public void AnUntrackedHiddenObjectIsNeitherListedNorPlaced()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Record(world, StableHash.Of("Stone"), tracked: false, shown: false, end: 50);
        Assert.DoesNotContain(world.Objects, o => o.Id == Id);
        Assert.DoesNotContain(world.Placed, p => p.Position == Place);
    }
}
