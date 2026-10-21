using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The ground a location's SnapToGround objects are put on in New world (WorldCreator.SnapGround): the zone's ground as
/// generated with the location's own terrain modifiers, between the mesh's points, in whichever zone the point is.
/// </summary>
[Collection("World files")]
public class WorldCreatorSnapGroundTests
{
    [Fact]
    public void TheGroundHasTheLocationsOwnTerrainModifiers()
    {
        using var w = new TempWorld();
        var terrain = new TerrainService(w.Load(), null);
        var spot = new Vector3(10, 45, 20);
        var temple = new WorldCreator.SnapGround(terrain, LocationPrefabs.Get("StartTemple")!.Hash, spot, Quaternion.Identity);
        var none = new WorldCreator.SnapGround(terrain, StableHash.Of("NoSuchLocation"), spot, Quaternion.Identity);
        // The temple levels its middle to its spot; a location without modifiers leaves the ground as generated.
        Assert.Equal(45f, temple.HeightAt(spot), 2);
        float generated = Regrow.GridHeight(terrain.BaseZone(0, 0), EditStore.Grid, -32f, -32f, spot.X, spot.Z);
        Assert.Equal(generated, none.HeightAt(spot), 4);
        // A point in the next zone: that zone's ground.
        var next = new Vector3(70, 0, 20);
        Assert.Equal(Regrow.GridHeight(terrain.BaseZone(1, 0), EditStore.Grid, 32f, -32f, next.X, next.Z), none.HeightAt(next), 4);
    }

    [Fact]
    public void TheCatalogueKeepsEachObjectsSnapOffset()
    {
        var temple = LocationPrefabs.Get("StartTemple")!;
        Assert.All(temple.Contents.Where(c => c.Prefab is "Pickable_Stone" or "Pickable_Branch"), c => Assert.Equal(0f, c.Snap));
        Assert.Null(temple.Contents.First(c => c.Prefab == "BossStone_Eikthyr").Snap);
    }
}
