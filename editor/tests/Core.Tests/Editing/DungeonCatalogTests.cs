using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.DungeonCaveSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The dungeon kinds and rooms the game defines, the room list in a dungeon object as the Dungeon tool writes it, and the
/// rotations of rooms (Euler angles as Unity turns them).
/// </summary>
public class DungeonCatalogTests
{
    [Fact]
    public void TheCatalogueKnowsTheKindsAndTheirRooms()
    {
        var cave = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
        Assert.True(cave.Interior);
        Assert.Equal(64, cave.ZoneSize.X);
        var rooms = Dungeons.RoomsFor(cave).Select(r => r.Name).ToList();
        Assert.Contains("cave_new_entrance02", rooms);
        Assert.Contains("cave_new_endcap02", rooms);
        Assert.DoesNotContain(rooms, r => r.StartsWith("sunkencrypt", StringComparison.Ordinal));
        // A goblin camp is laid out on the ground, not in the sky.
        Assert.False(Dungeons.KindOf(StableHash.Of("DG_GoblinCamp"))!.Interior);
        var entrance = Dungeons.RoomOf("cave_new_entrance02")!;
        Assert.True(entrance.Entrance);
        Assert.Equal(4, entrance.Openings.Length);
        Assert.Single(entrance.Openings, o => o.Entrance);
        Assert.NotEmpty(entrance.Contents);
    }

    [Fact]
    public void TheRoomListReadsAndWritesBack()
    {
        var rooms = Cave();
        Assert.Equal(30, rooms.Count);
        Assert.All(rooms, r => Assert.NotNull(r.Room));
        Assert.Equal("cave_new_entrance02", rooms[0].Name);
        Assert.Equal(new Vector3(0, 5165.023f, -2642), rooms[0].Position, new Vector3Comparer(1e-3f));
        var again = Dungeons.Read(Dungeons.Write(rooms));
        for (int i = 0; i < rooms.Count; i++)
        {
            Assert.Equal(rooms[i].Hash, again[i].Hash);
            Assert.Equal(rooms[i].Position, again[i].Position);
            Assert.True(Same(rooms[i].Rotation, again[i].Rotation), rooms[i].Name);
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 90, 0)]
    [InlineData(0, 270.00037, 0)]
    [InlineData(10, 200, 30)]
    [InlineData(-35, 45, 170)]
    [InlineData(89.5, 10, 0)]
    public void EulerAnglesTurnLikeUnitys(float x, float y, float z)
    {
        Quaternion q = BlueprintFormats.FromEuler(new Vector3(x, y, z));
        Assert.True(Same(q, BlueprintFormats.FromEuler(BlueprintFormats.ToEuler(q))));
        // Quaternion.Euler turns about z, then x, then y.
        Quaternion zxy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y * MathF.PI / 180) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, x * MathF.PI / 180)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z * MathF.PI / 180);
        Assert.True(Same(q, zxy));
    }

    [Fact]
    public void TheDungeonObjectGetsTheNewList()
    {
        int dg = StableHash.Of("DG_Cave");
        byte[] blank = ZdoBuilder.Blank(dg, 0, CaveAt, Vector3.Zero, 0f);
        var rooms = Cave().Take(4).ToList();
        byte[] edited = Dungeons.WithRooms(blank, rooms);
        var z = ZdoData.Parse(edited);
        Assert.Equal(dg, z.Prefab);
        var back = Dungeons.Read(z.GetBytes(Dungeons.RoomDataKey)!);
        Assert.Equal(rooms.Select(r => r.Hash), back.Select(r => r.Hash));
        Assert.Throws<InvalidDataException>(() => Dungeons.Read([200, 0, 0, 0, 1, 2]));
    }

    [Fact]
    public void ARoomOfNoKnownPrefabCountsAsInside()
    {
        var cave = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
        var unknown = new Dungeons.Placed(StableHash.Of("no_such_room"), CaveAt + new Vector3(9999, 0, 9999), Quaternion.Identity);
        Assert.Null(unknown.Room);
        Assert.True(Dungeons.Inside(cave, CaveAt, unknown));
    }

    [Fact]
    public void DamagedNodesInScanDataEndTheWalkUpThePartsParents()
    {
        // A node past the list threw, and parents looping (0 under 1 under 0) never ended.
        Dungeons.Content[] contents = [new("past", Vector3.Zero, Quaternion.Identity, 5), new("looped", Vector3.One, Quaternion.Identity, 0)];

        var made = Dungeons.Replay(contents, [], [], [1, 0], -1, 0, 1, Vector3.Zero, Quaternion.Identity);

        Assert.Equal(["past", "looped"], made.Select(m => m.Prefab));
    }
}
