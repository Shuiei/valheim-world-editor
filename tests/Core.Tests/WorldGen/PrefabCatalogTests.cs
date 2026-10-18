using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// New objects built without a model in the world, and the prefab catalogue they rely on.
/// </summary>
public class PrefabCatalogTests
{
    [Fact]
    public void CatalogueKnowsPlaceableKinds()
    {
        Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("sapling_turnip")));
        Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("Beech1")));
        // Creatures and item drops are not placeable.
        Assert.Null(PrefabCatalog.Get(Fixtures.Hash("Boar")));
        Assert.True(PrefabCatalog.Placeable.Count() > 500);
    }

    [Fact]
    public void RangesHoldWardsAndStationsOnce()
    {
        Assert.Equal((32f, 0f), PrefabCatalog.Ranges["guard_stone"]);
        Assert.Equal((0f, 20f), PrefabCatalog.Ranges["piece_workbench"]);
        Assert.False(PrefabCatalog.Ranges.ContainsKey("Beech1"));
        // Made once per catalogue, not on every overlay build.
        Assert.Same(PrefabCatalog.Ranges, PrefabCatalog.Ranges);
    }

    [Fact]
    public void SaplingsKnowTheirGrowRadius()
    {
        var turnip = PrefabCatalog.Get(Fixtures.Hash("sapling_turnip"))!;
        Assert.Equal(0.5f, turnip.GrowRadius, 2);
        Assert.True(turnip.NeedsCultivated);
        Assert.Equal(0f, PrefabCatalog.Get(Fixtures.Hash("Beech1"))!.GrowRadius);
    }

    [Fact]
    public void BlankObjectHasTheGameFlagsAndNoData()
    {
        var info = PrefabCatalog.Get(Fixtures.Hash("Pickable_Mushroom_Magecap"))!;
        byte[] b = ZdoBuilder.Blank(Fixtures.Hash("Pickable_Mushroom_Magecap"), info.Flags, new Vector3(10, 40, -5), Vector3.Zero, 0f);
        ushort flags = BitConverter.ToUInt16(b, 0);
        Assert.True((flags & 0x100) != 0, "saved by the game (persistent)");
        Assert.Equal(0, flags & 0xFF);
        Assert.Equal(2 + 12 + 4, b.Length);
        Assert.Equal(10f, BitConverter.ToSingle(b, 2));
        Assert.Equal(Fixtures.Hash("Pickable_Mushroom_Magecap"), BitConverter.ToInt32(b, 14));
    }

    [Fact]
    public void BlankObjectStoresRotationAndScale()
    {
        var info = PrefabCatalog.Get(Fixtures.Hash("sapling_turnip"))!;
        byte[] b = ZdoBuilder.Blank(Fixtures.Hash("sapling_turnip"), info.Flags, Vector3.Zero, new Vector3(0, 45, 0), 1.25f);
        ushort flags = BitConverter.ToUInt16(b, 0);
        Assert.True((flags & 0x1000) != 0, "rotation");
        Assert.True((flags & 0x4) != 0, "a Vector3 section (scale)");
    }

    [Fact]
    public void StableHashMatchesTheGame()
    {
        // Known values from the game's own saves.
        Assert.Equal(-367065113, StableHash.Of("_TerrainCompiler"));
    }

    [Fact]
    public void PiecesKnowTheirSnapPoints()
    {
        var wall = PieceCatalog.Get(Fixtures.Hash("woodwall"))!;
        Assert.Equal(4, wall.Snaps.Length);
        // A 2 m wall: snap points at x = -1 and 1.
        Assert.Equal(2f, wall.Snaps.Max(p => p[0]) - wall.Snaps.Min(p => p[0]), 2);
        Assert.Contains(PieceCatalog.WithSnaps, i => i.Name == "wood_fence");
        Assert.True(PieceCatalog.WithSnaps.Count() > 50);
    }

    [Fact]
    public void SpawnPointsKnowTheirCreatureLevelsAndRespawn()
    {
        var boar = PrefabCatalog.SpawnOf("Spawner_Boar")!;
        Assert.Equal(["Boar"], boar.Creatures);
        Assert.False(boar.Nest);
        Assert.False(boar.Respawns);
        Assert.Equal((1, 3), boar.Levels);
        Assert.Equal("Boar · 0–2 ★ · spawns once", boar.Summary());
        Assert.True(PrefabCatalog.IsSpawnPoint("Spawner_Boar"));
        Assert.False(PrefabCatalog.IsNest("Spawner_Boar"));
        // Placed like any other object.
        Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("Spawner_Boar")));

        var back = PrefabCatalog.SpawnOf("Spawner_Skeleton_respawn_30")!;
        Assert.True(back.Respawns);
        Assert.Equal("Skeleton · 0–2 ★ · back 60 min after it dies", back.Summary());
        Assert.Equal("Skeleton_NoArcher · 0–2 ★ · spawns once · by night only", PrefabCatalog.SpawnOf("Spawner_Skeleton_night_noarcher")!.Summary());
        // The game's data says 3 to 1: the game never rolls above level 1 then.
        var draugr = PrefabCatalog.SpawnOf("Spawner_Draugr")!;
        Assert.Equal((1, 1), draugr.Levels);
        Assert.Equal("Draugr · spawns once", draugr.Summary());
        Assert.Equal("Seeker, SeekerBrood · when an event wakes it", PrefabCatalog.SpawnOf("TriggerSpawner_Seeker")!.Summary());
        Assert.Null(PrefabCatalog.SpawnOf("Beech1"));
    }

    [Fact]
    public void NestsKnowWhatTheySpawnAroundThem()
    {
        var nest = PrefabCatalog.SpawnOf("Spawner_GreydwarfNest")!;
        Assert.True(nest.Nest);
        Assert.False(nest.Respawns);
        Assert.Equal(["Greydwarf", "Greydwarf_Elite", "Greydwarf_Shaman"], nest.Creatures);
        Assert.Equal(3, nest.MaxNear);
        Assert.Equal(4f, nest.Radius);
        Assert.True(PrefabCatalog.IsNest("Spawner_GreydwarfNest"));
        Assert.False(PrefabCatalog.IsSpawnPoint("Spawner_GreydwarfNest"));
        Assert.Equal("Skeleton · up to 2 around, within 2.3 m", PrefabCatalog.SpawnOf("BonePileSpawner")!.Summary());
    }
}
