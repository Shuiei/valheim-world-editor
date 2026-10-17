using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A dungeon's entrance (DungeonEntrances): which location leads into a dungeon of a kind, and where the game puts
/// that location's dungeon and its door out for an entrance stood somewhere: with the entrance (a crypt), or at the
/// zone's middle, square to the world, for a Frost Cave (its custom interior transform), as the game's own worlds
/// show (World20118's crypts: the dungeon object 1 m behind the entrance, turned with it; a Frost Cave at 0, -2624:
/// its dungeon at 0, 5165, -2654).
/// </summary>
[Collection("World files")]
public class DungeonEntrancesTests
{
    /// <summary>A turn about the vertical, degrees.</summary>
    private static Quaternion Yaw(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);

    /// <summary>Whether two rotations are the same turn (either sign, nearly exactly).</summary>
    private static bool Same(Quaternion a, Quaternion b) => MathF.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b))) > 0.9999f;

    [Fact]
    public void EachKindOfDungeonHasItsEntranceAndQuestPlacesNeverServe()
    {
        Assert.Equal("Crypt2", DungeonEntrances.For("DG_ForestCrypt")?.Name);
        Assert.Equal("MountainCave02", DungeonEntrances.For("DG_Cave")?.Name);
        Assert.Equal("Crypt2", DungeonEntrances.ForBiome("Black Forest")?.Name);
        Assert.Equal("MountainCave02", DungeonEntrances.ForBiome("Deep North")?.Name);
        // Hildir's fortress is the Plains' only dungeon: no entrance serves there.
        Assert.Null(DungeonEntrances.ForBiome("Plains"));
        Assert.Null(DungeonEntrances.For("DG_NoSuchThing"));
        Assert.Equal("burial chamber", DungeonEntrances.Describe(LocationPrefabs.Get("Crypt2")!).English);
        Assert.Equal("Frost Cave", DungeonEntrances.Describe(LocationPrefabs.Get("MountainCave02")!).English);
        Assert.Equal("StartTemple", DungeonEntrances.Describe(LocationPrefabs.Get("StartTemple")!).English);
        Assert.Equal("DG_ForestCrypt", DungeonEntrances.Dungeon(LocationPrefabs.Get("Crypt2")!)?.Prefab);
        Assert.Null(DungeonEntrances.Dungeon(LocationPrefabs.Get("StartTemple")!));
    }

    [Fact]
    public void ACryptsDungeonAndDoorOutStandWithTheEntranceTurnedWithIt()
    {
        var crypt = LocationPrefabs.Get("Crypt2")!;
        var at = new Vector3(-837.1314f, 44.394974f, -1020.6193f);
        var turn = Yaw(22.5f);
        // As in World20118: the dungeon object 1 m behind the entrance, 5000 m up, turned as the entrance is.
        var dungeon = DungeonEntrances.DungeonPose(crypt, at, turn);
        Assert.NotNull(dungeon);
        Assert.True(Vector3.Distance(new Vector3(-837.5141f, 5044.395f, -1021.5432f), dungeon.Value.Position) < 0.01f, dungeon.Value.Position.ToString());
        Assert.True(Same(turn, dungeon.Value.Rotation));
        // The door out: 0.1 m over the dungeon's height, 1 m ahead of the dungeon object, facing the entrance's way.
        var exit = DungeonEntrances.ExitPose(crypt, at, turn);
        Assert.NotNull(exit);
        Assert.True(Vector3.Distance(at + Vector3.Transform(new Vector3(0, 5000.1f, 0), turn), exit.Value.Position) < 0.01f);
        Assert.True(Same(turn, exit.Value.Rotation));
        // A player arrives one metre ahead of the door, one down.
        Assert.True(Vector3.Distance(exit.Value.Position + Vector3.Transform(Vector3.UnitZ, turn) - Vector3.UnitY, DungeonEntrances.Landing(exit.Value)) < 0.001f);
        Assert.Equal(22.5f, DungeonEntrances.YawOf(exit.Value), 2);
        // Turned the other way: as YawToFace gives it, from 0 up to 360.
        Assert.Equal(337.5f, DungeonEntrances.YawOf(DungeonEntrances.ExitPose(crypt, at, Yaw(-22.5f))!.Value), 2);
    }

    [Fact]
    public void AFrostCavesDungeonGoesToTheZonesMiddleSquareToTheWorld()
    {
        var cave = LocationPrefabs.Get("MountainCave02")!;
        Assert.True(cave.CustomInterior);
        // The entrance anywhere in zone 0, -41, turned any way: the dungeon at the zone's middle (0, -2624) and 30 m
        // back, 5000 m over the entrance's ground, not turned, as the game's own cave there.
        var dungeon = DungeonEntrances.DungeonPose(cave, new Vector3(7.5f, 165.023f, -2610f), Yaw(135));
        Assert.NotNull(dungeon);
        Assert.True(Vector3.Distance(new Vector3(0, 5165.023f, -2654), dungeon.Value.Position) < 0.01f, dungeon.Value.Position.ToString());
        Assert.True(Same(Quaternion.Identity, dungeon.Value.Rotation));
        // Its door out stands with the interior: 0.5 m over the dungeon object and 1 m ahead of it, facing +z.
        var exit = DungeonEntrances.ExitPose(cave, new Vector3(7.5f, 165.023f, -2610f), Yaw(135));
        Assert.NotNull(exit);
        Assert.True(Vector3.Distance(new Vector3(0, 5165.523f, -2653), exit.Value.Position) < 0.01f, exit.Value.Position.ToString());
        Assert.True(Same(Quaternion.Identity, exit.Value.Rotation));
        Assert.Equal(new Vector3(0, 5164.523f, -2652), DungeonEntrances.Landing(exit.Value));
    }

    [Fact]
    public void TheEntranceIsTurnedToFaceAPoint()
    {
        var crypt = LocationPrefabs.Get("Crypt2")!;
        var at = new Vector3(10, 30, 10);
        // The crypt's door faces -z in its prefab: unturned, it faces a point south; turned 180°, a point north.
        Assert.Equal(0f, DungeonEntrances.YawToFace(crypt, at, at + new Vector3(0, 0, -20)), 2);
        Assert.Equal(180f, DungeonEntrances.YawToFace(crypt, at, at + new Vector3(0, 0, 20)), 2);
        Assert.Equal(270f, DungeonEntrances.YawToFace(crypt, at, at + new Vector3(20, 0, 0)), 2);
        // Facing the point where it stands, or without a door: unturned.
        Assert.Equal(0f, DungeonEntrances.YawToFace(crypt, at, at));
        Assert.Equal(0f, DungeonEntrances.YawToFace(LocationPrefabs.Get("StartTemple")!, at, at + Vector3.UnitX));
        // Once turned, the door's forward is the way asked.
        var turned = Yaw(DungeonEntrances.YawToFace(crypt, at, at + new Vector3(20, 0, 0)));
        var forward = Vector3.Transform(Vector3.Transform(Vector3.UnitZ, crypt.Entrance!.Rotation), turned);
        Assert.True(Vector3.Distance(Vector3.UnitX, forward) < 0.001f, forward.ToString());
    }

    [Fact]
    public void ALocationWithoutADungeonOrADoorHasNoPoses()
    {
        var temple = LocationPrefabs.Get("StartTemple")!;
        Assert.Null(DungeonEntrances.DungeonPose(temple, Vector3.Zero, Quaternion.Identity));
        Assert.Null(DungeonEntrances.ExitPose(temple, Vector3.Zero, Quaternion.Identity));
        // A custom interior whose anchor is turned: the door's place and turn are taken in the anchor's frame.
        var dg = new Dungeons.Content("DG_Cave", new Vector3(0, 5000, -30), Quaternion.Identity, 1);
        var made = new LocationPrefabs.Prefab("Made", StableHash.Of("Made"), false, false, true, [dg], [], [], [-1, 0], true,
            [new LocationPrefabs.Frame(new Vector3(4, 5000, 0), Yaw(90))], new LocationPrefabs.Frame(new Vector3(0, 5000, 0), Yaw(90)));
        var exit = DungeonEntrances.ExitPose(made, new Vector3(100, 20, 100), Yaw(45));
        Assert.NotNull(exit);
        // The anchor turned 90° in the prefab: 4 m along x there is 4 m along +z once the anchor is square to the world.
        Assert.True(Vector3.Distance(new Vector3(128, 5020, 128 - 30 + 4), exit.Value.Position) < 0.01f, exit.Value.Position.ToString());
        Assert.True(Same(Quaternion.Identity, exit.Value.Rotation));
        Assert.Equal(new Vector3(64, 0, -64), DungeonEntrances.ZoneCentre(new Vector3(40, 9, -40)));
    }
}
