using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Runestones and tamed animals in the save: a runestone is a location proxy, listed as an object under its location's
/// name (so it can be shown, picked and deleted); a creature with "tamed" set is its own View kind. The objects are
/// saved into the test world through the editor's own writer first.
/// </summary>
[Collection("World files")]
public class ObjectKindsTests
{
    private static readonly int Proxy = StableHash.Of("LocationProxy");
    private static readonly int Meadows = StableHash.Of("Runestone_Meadows");
    private static readonly int Boar = StableHash.Of("Boar");

    /// <summary>
    /// A new blank object of this prefab at this spot, with these ints set.
    /// </summary>
    private static NewObject Make(int prefab, Vector3 at, params (string Key, int Value)[] ints)
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(prefab, 0x100, at, new Vector3(0, 30, 0), 0f));
        foreach (var (key, value) in ints)
        {
            z.Set("ints", StableHash.Of(key), value.ToString(CultureInfo.InvariantCulture));
        }
        return new NewObject(-1, prefab, at, new Vector3(0, 30, 0), 0f, Fresh: false, Raw: z.Serialize());
    }

    /// <summary>
    /// The test world with a Meadows runestone, a tamed boar and others saved into it, read again.
    /// </summary>
    private static WorldSave WithStoneAndBoars(TempWorld w)
    {
        var world = w.Load();
        var added = new[]
        {
            Make(Proxy, new Vector3(10, 30, 10), ("location", Meadows)),
            Make(Boar, new Vector3(14, 30, 10), ("tamed", 1)),
            Make(Boar, new Vector3(18, 30, 10)),
        };
        var r = WorldWriter.Save(world, [], added: added);
        Assert.True(r.Saved, r.Message);
        return w.Load();
    }

    [Fact]
    public void ARunestoneIsListedUnderItsLocationAndOnlyTamedBoarsAreAnimals()
    {
        using var w = new TempWorld();
        var world = WithStoneAndBoars(w);
        var (id, prefab, position, rotation, scale) = Assert.Single(world.Objects, o => o.Prefab == Meadows);
        Assert.Equal(10, position.X, 0.01f);
        Assert.Equal(Proxy, world.ObjectRefs[id].Prefab);
        Assert.Equal("Runestone_Meadows", PrefabCatalog.NameOf(Meadows));
        Assert.True(PrefabCatalog.IsRunestone(Meadows));
        Assert.Equal(ObjectKind.Runestones, ObjectKinds.Of("Runestone_Meadows", false));
        // Not something a new object can be built from: copies and pastes leave it out.
        Assert.False(world.CanCreate(Meadows));

        var boars = world.Objects.Where(o => o.Prefab == Boar).ToList();
        Assert.Equal(2, boars.Count);
        var (_, _, tamePosition, _, _) = Assert.Single(boars, b => world.Tamed.Contains(b.Id));
        Assert.Equal(14, tamePosition.X, 0.01f);
        Assert.Equal(ObjectKind.Animals, ObjectKinds.Of("Boar", false, tamed: true));
        Assert.Equal(ObjectKind.Creatures, ObjectKinds.Of("Boar", false));
    }

    [Theory]
    [InlineData("Spawner_Boar", ObjectKind.SpawnPoints)]
    [InlineData("Spawner_Skeleton_respawn_30", ObjectKind.SpawnPoints)]
    [InlineData("TriggerSpawner_Seeker", ObjectKind.SpawnPoints)]
    [InlineData("Spawner_GreydwarfNest", ObjectKind.Nests)]
    [InlineData("BonePileSpawner", ObjectKind.Nests)]
    [InlineData("Spawner_Hole_double", ObjectKind.Nests)]
    [InlineData("Spawner_NotInTheGame", ObjectKind.SpawnPoints)]
    [InlineData("Greydwarf", ObjectKind.Creatures)]
    [InlineData("Haldor", ObjectKind.Creatures)]
    [InlineData("Crow", ObjectKind.Creatures)]
    [InlineData("vfx_swamp_mist", ObjectKind.Effects)]
    [InlineData("Music_FulingCamp", ObjectKind.Effects)]
    [InlineData("eventzone_eikthyr", ObjectKind.Effects)]
    [InlineData("giant_helmet1_destruction", ObjectKind.Effects)]
    [InlineData("giant_skull", ObjectKind.Remains)]
    [InlineData("Wolf_Ragdoll", ObjectKind.Remains)]
    [InlineData("TrollFrost_Dead", ObjectKind.Remains)]
    [InlineData("dvergrprops_crate", ObjectKind.Furniture)]
    [InlineData("Morkhalla_Table", ObjectKind.Furniture)]
    [InlineData("prop_ashwood_bed", ObjectKind.Furniture)]
    [InlineData("Morkhalla_Floor_4x4", ObjectKind.Ruins)]
    [InlineData("Ashlands_Wall_2x2", ObjectKind.Ruins)]
    [InlineData("dvergrtown_arch", ObjectKind.Ruins)]
    [InlineData("AshlandsTree1", ObjectKind.Trees)]
    [InlineData("FirTree_Big_plantable_Stub", ObjectKind.Trees)]
    [InlineData("YggaShoot1", ObjectKind.Trees)]
    [InlineData("UnstableLavaRock", ObjectKind.Rocks)]
    [InlineData("rock4_forest_frac", ObjectKind.Rocks)]
    [InlineData("WallSnow1", ObjectKind.Rocks)]
    [InlineData("VineAsh", ObjectKind.Bushes)]
    [InlineData("LootSpawner_pineforest", ObjectKind.Pickables)]
    [InlineData("Fire", ObjectKind.Other)]
    [InlineData("Player_tombstone", ObjectKind.Other)]
    public void EachObjectHasTheKindItLooksLike(string name, ObjectKind want) => Assert.Equal(want, ObjectKinds.Of(name, false));

    [Fact]
    public void ANameIsSortedByTheRulesOnlyOnce()
    {
        string name = "rock_" + Guid.NewGuid().ToString("N");
        int before = ObjectKinds.Finds;
        Assert.Equal(ObjectKind.Rocks, ObjectKinds.Of(name, false));
        Assert.Equal(ObjectKind.Rocks, ObjectKinds.Of(name, false));
        Assert.Equal(before + 1, ObjectKinds.Finds);
        // A placed piece or a tamed creature needs no rules.
        Assert.Equal(ObjectKind.Buildings, ObjectKinds.Of(name, true));
        Assert.Equal(ObjectKind.Animals, ObjectKinds.Of(name, false, tamed: true));
        Assert.Equal(before + 1, ObjectKinds.Finds);
    }

    [Fact]
    public void EveryKindHasItsOwnLabel()
    {
        Assert.Equal(ObjectKinds.All.Length, ObjectKinds.All.Select(ObjectKinds.Label).Distinct().Count());
        Assert.Equal("Spawn points", ObjectKinds.Label(ObjectKind.SpawnPoints));
        Assert.Equal("Other objects", ObjectKinds.Label(ObjectKind.Other));
    }

    [Fact]
    public void DeletingARunestoneRemovesItsProxyFromTheSave()
    {
        using var w = new TempWorld();
        var world = WithStoneAndBoars(w);
        int stone = world.Objects.Single(o => o.Prefab == Meadows).Id;
        int before = world.ObjectRefs.Count;
        var r = WorldWriter.Save(world, [], deleted: [stone]);
        Assert.True(r.Saved, r.Message);
        world = w.Load();
        Assert.DoesNotContain(world.Objects, o => o.Prefab == Meadows);
        Assert.DoesNotContain(world.Locations, l => l.Location == Meadows);
        Assert.Equal(before - 1, world.ObjectRefs.Count);
        // The tamed boar is still tamed after the save.
        Assert.Single(world.Objects, o => o.Prefab == Boar && world.Tamed.Contains(o.Id));
    }
}
