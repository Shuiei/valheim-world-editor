using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.DungeonCaveSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The rules for joining rooms at their openings (the Dungeon tool's joints, end caps, overlaps and doors), checked against
/// the Frost Cave the game generated itself.
/// </summary>
public class DungeonCaveJointTests
{
    [Fact]
    public void AGeneratedCaveHasNoOpenEndAndNoOverlap()
    {
        var rooms = Cave();
        Assert.Empty(Dungeons.Openings(rooms, freeOnly: true));
        for (int i = 0; i < rooms.Count; i++)
        {
            Assert.Empty(Dungeons.Overlaps(rooms, rooms[i], i));
            Assert.True(Dungeons.Inside(Dungeons.KindOf(StableHash.Of("DG_Cave"))!, CaveAt, rooms[i]), rooms[i].Name);
        }
    }

    [Fact]
    public void JoiningAtAnOpeningPutsEachRoomWhereTheGameDid()
    {
        var rooms = Cave();
        var ends = Dungeons.Openings(rooms, freeOnly: false);
        int joined = 0;
        for (int i = 1; i < rooms.Count; i++)
        {
            // The room's openings that meet an earlier room's: rebuilt from that one, it lands in place.
            var room = rooms[i].Room!;
            foreach (var mine in ends.Where(e => e.Room == i))
            {
                var other = ends.FirstOrDefault(e => e.Room < i && Vector3.DistanceSquared(e.Position, mine.Position) < 0.01f);
                if (other == null)
                {
                    continue;
                }
                var made = Dungeons.Attach(room, room.Openings[mine.Index], other.Position, other.Rotation);
                Assert.Equal(rooms[i].Position, made.Position, new Vector3Comparer(1e-3f));
                Assert.True(Same(rooms[i].Rotation, made.Rotation), room.Name);
                joined++;
            }
        }
        Assert.True(joined >= 29, $"{joined} joints");
    }

    [Fact]
    public void ARoomAddedAtAnOpenEndClosesItAndAnOverlapIsFound()
    {
        // The entrance alone: three open ends; an end cap on each closes them all.
        var rooms = Cave().Take(1).ToList();
        var cap = Dungeons.RoomOf("cave_new_endcap02")!;
        Assert.Equal(3, Dungeons.Openings(rooms, true).Count);
        foreach (var end in Dungeons.Openings(rooms, true))
        {
            rooms.Add(Dungeons.Attach(cap, Dungeons.Matching(cap, end.Type).Single(), end.Position, end.Rotation));
        }
        Assert.Empty(Dungeons.Openings(rooms, true));
        // The same caps the game would place (the one-room cave tested in game).
        Assert.Contains(rooms, r => r.Hash == cap.Hash && Vector3.Distance(r.Position, new Vector3(0, 5165.023f, -2630)) < 1e-3f);
        Assert.Contains(rooms, r => r.Hash == cap.Hash && Vector3.Distance(r.Position, new Vector3(6, 5165.023f, -2636)) < 1e-3f);
        Assert.Contains(rooms, r => r.Hash == cap.Hash && Vector3.Distance(r.Position, new Vector3(-6, 5165.023f, -2636)) < 1e-3f);
        // A corridor laid over the entrance overlaps it.
        var corridor = Dungeons.RoomOf("cave_new_corridor03")!;
        var over = new Dungeons.Placed(corridor.Hash, rooms[0].Position, rooms[0].Rotation);
        Assert.Contains(0, Dungeons.Overlaps(rooms, over));
    }

    [Fact]
    public void DoorsGoWhereRoomsMeetTheGamesWay()
    {
        // That Frost Cave: 29 joints, 13 where a door can stand (all its 7 saved doors are on those).
        var rooms = Cave();
        var joints = Dungeons.Joints(rooms);
        Assert.Equal(29, joints.Count);
        Assert.Equal(13, joints.Count(j => j.DoorAllowed));
        // Never where an end cap closes the opening.
        Assert.DoesNotContain(joints, j => j.DoorAllowed && rooms[j.Second].Room!.EndCap);
        var cave = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
        Assert.Contains(cave.Doors, d => d.Prefab == "cloth_hanging_door_double" && d.Type == "shrine");
        var shrine = joints.First(j => j.DoorAllowed && j.Type == "shrine");
        // The roll: the door type, then its chance (0.8 for a shrine curtain).
        Assert.Equal("cloth_hanging_door_double", Dungeons.RollDoor(cave, shrine, () => 0.5f));
        Assert.Null(Dungeons.RollDoor(cave, shrine, () => 0.9f));
        Assert.Null(Dungeons.RollDoor(cave, shrine with { DoorAllowed = false }, () => 0f));
    }

    [Fact]
    public void TheFirstRoomIsPlacedWhereTheGamePlacesIt()
    {
        // The game puts the entrance room's way in at the dungeon object, facing its way (PlaceStartRoom): the real
        // cave's first room, placed again from its dungeon object.
        var real = Cave()[0];
        var start = Dungeons.PlaceStart(real.Room!, CaveAt, Quaternion.Identity);
        Assert.Equal(real.Hash, start.Hash);
        Assert.True(Vector3.Distance(real.Position, start.Position) < 0.01f, start.Position.ToString());
        Assert.True(Same(real.Rotation, start.Rotation));
        // Turned: the way in turns with it. A room without openings stands at the object itself.
        var turned = Dungeons.PlaceStart(real.Room!, CaveAt, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        Assert.True(Vector3.Distance(CaveAt, turned.At(real.Room!.Openings.First(o => o.Entrance)).Position) < 0.01f);
        var bare = new Dungeons.Room("bare", 1, 1, new Vector3(4, 4, 4), false, false, false, 0, 1f, false, true, [], [], [], [], []);
        Assert.Equal(CaveAt, Dungeons.PlaceStart(bare, CaveAt, Quaternion.Identity).Position);
    }
}
