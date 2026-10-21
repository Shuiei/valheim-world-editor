using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ground discs of a world as the uplift sees them: the saved discs and the added disc objects, less those deleted or
/// left out, and each one as the placed object the game spawns for it.
/// </summary>
[Collection("World files")]
public class UpliftDiscListTests
{
    // Ids no saved object of the test world has. Saved objects have ids of zero or more (EditStore.SetDeleted takes a
    // negative id for an object added in the session), so the saved discs are positive.
    private const int SavedDisc = 7001, KeptDisc = 7002, AddedDisc = -7003, AddedStone = -7004;

    [Fact]
    public void ASavedDiscThatIsDeletedIsNotCurrent()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        world.Discs.Add((SavedDisc, new Vector3(1, 50, 2)));
        world.Discs.Add((KeptDisc, new Vector3(3, 50, 4)));
        var edits = new EditStore(world);
        edits.SetDeleted([SavedDisc], true);
        var current = Uplift.CurrentDiscs(world, edits);
        Assert.Contains(current, d => d.Id == KeptDisc);
        Assert.DoesNotContain(current, d => d.Id == SavedDisc);
    }

    [Fact]
    public void AnAddedDiscObjectIsCurrentAndAnAddedStoneIsNot()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var disc = new Vector3(5, 60, 6);
        var edits = new EditStore(world);
        edits.AddObjects([
            new NewObject(AddedDisc, WorldSave.LocationProxyPrefab, disc, Vector3.Zero, 0f, Raw: Uplift.DiscBytes(disc)),
            new NewObject(AddedStone, StableHash.Of("Stone"), disc, Vector3.Zero, 1f),
        ]);
        var current = Uplift.CurrentDiscs(world, edits);
        Assert.Contains(current, d => d.Id == AddedDisc && d.Position == disc);
        Assert.DoesNotContain(current, d => d.Id == AddedStone);
    }

    [Fact]
    public void APlacedDiscLeftOutOfTheListIsNotPlacedNow()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var at = new Vector3(7, 70, 8);
        world.Discs.Add((SavedDisc, at));
        var edits = new EditStore(world);
        Assert.Contains(Uplift.PlacedNow(world, edits), o => o.Location == Uplift.Disc && o.Position == at);
        Assert.DoesNotContain(Uplift.PlacedNow(world, edits, [(SavedDisc, at)]), o => o.Location == Uplift.Disc && o.Position == at);
    }

    [Fact]
    public void ADiscIsPlacedAsALocationProxyForDevGroundAtItsPoint()
    {
        var at = new Vector3(9, 80, 10);
        var placed = PrivateMember.Call<PlacedObject>(typeof(Uplift), "DiscAt", null, at);
        Assert.Equal(WorldSave.LocationProxyPrefab, placed.Prefab);
        Assert.Equal(Uplift.Disc, placed.Location);
        Assert.Equal(at, placed.Position);
    }

    [Fact]
    public void AnObjectIsADiscWhenItIsALocationProxyForDevGround()
    {
        var at = new Vector3(1, 2, 3);
        var disc = new NewObject(AddedDisc, WorldSave.LocationProxyPrefab, at, Vector3.Zero, 0f, Raw: Uplift.DiscBytes(at));
        Assert.True(PrivateMember.Call<bool>(typeof(Uplift), "IsDiscObject", null, disc));
    }

    [Fact]
    public void AProxyWithoutItsDataIsNotADisc()
    {
        var proxy = new NewObject(AddedDisc, WorldSave.LocationProxyPrefab, Vector3.Zero, Vector3.Zero, 0f);
        Assert.False(PrivateMember.Call<bool>(typeof(Uplift), "IsDiscObject", null, proxy));
    }

    [Fact]
    public void AnObjectOfAnotherPrefabIsNotADiscEvenWithDiscData()
    {
        var at = new Vector3(1, 2, 3);
        var stone = new NewObject(AddedStone, StableHash.Of("Stone"), at, Vector3.Zero, 1f, Raw: Uplift.DiscBytes(at));
        Assert.False(PrivateMember.Call<bool>(typeof(Uplift), "IsDiscObject", null, stone));
    }
}
