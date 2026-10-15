using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Dungeons as the Dungeon tool edits them: the room list in a dungeon object, and the game's rules for joining rooms
/// at their openings, checked against a Frost Cave the game generated itself.
/// </summary>
public class DungeonsTests
{
    // The room list (roomData) of a Frost Cave of a real world (30 rooms, DG_Cave at 0, 5165.02, -2654),
    // laid out by the game's generator.
    private const string GeneratedCave = "HgAAAJd4m14AAMA1L2ihRQAgJcUAAACAAAC0QgAAAADXtLSx/v8/wS9goUUAwCTFAAAAgJMOIjcAAAAACRvPzAAAoDUvaKFFAAAkxQAAAIAAAIdDAAAAAA+Nnz/wYya1L3ChRQDgIsUAAAAAk6ImuQAAAAAPjZ8//v+PwS9woUUAACTFAAAAAPv/hkMAAAAAi7rYjhMAQMEveqFFAIAixQAAAAAMAIdDAAAAAHlwoYEAAMDBL1KhRQDAJMUAAACAAQA0QwAAAACZiJRnBQDAwS92oUUAQCPFAAAAAAEAh0MAAAAAY59FIu7/P0EvdqFFAIAixQAAAIADALRCAAAAAPBFMewKAMDBL3qhRQCAIsUAAACAMAC0QgAAAADIKp5/9/+/QS92oUUAgCLFAAAAgAQAtEIAAACAUupdjeb/f0EvaqFFAEAjxQAAAIAEALRCAAAAgIjTrNJhAEDBL2qhRQBAI8UAAACAGAA0QwAAAACI06zS3P8/QS9qoUUAACTFAAAAgAIANEMAAAAAQqG7uAcAwEAvaKFFAMAkxQAAAICTDiI3AAAAgF0FKu0AAMBAL2ihRQAAJMUAAACA3hVzNwAAAICzcESuIDMOuC92oUUAICLFAAAAgOz/s0IAAAAAhyon3v7/78EvdqFFAAAkxQAAAAA13RK5AAAAAIcqJ972/7/BL3ahRQBgJMUAAACADACHQwAAAAB2QLA77f8/wS92oUUAICLFAAAAgC8AtEIAAAAAXgUq7f//v8EvUqFFAGAkxQAAAIABAIdDAAAAAKwp/4AEAJDBL3ahRQBAI8UAAAAAAgA0QwAAAACsKf+A8f8/QS92oUUAICLFAAAAgAQAtEIAAAAAdkCwOwoA8MEvdqFFAIAixQAAAIDDgcU5AAAAAHZAsDv2/7/BL3ahRQAgIsUAAACAMQC0QgAAAACsKf+AHgDAwS92oUUA4CLFAAAAgAwAh0MAAAAAdkCwO/f/70EvdqFFAIAixQAAAAADADRDAAAAAIcqJ975/79BL3ahRQAgIsUAAACABAC0QgAAAICHKife9f+/QS92oUUA4CLFAAAAAAEAh0MAAAAArCn/gMj/v0AvdqFFAEAjxQAAAIBlUDY4AAAAgA==";

    private static readonly Vector3 CaveAt = new(0, 5165.023f, -2654);

    /// <summary>
    /// The rooms of the generated Frost Cave.
    /// </summary>
    private static List<Dungeons.Placed> Cave() => Dungeons.Read(Convert.FromBase64String(GeneratedCave));

    /// <summary>
    /// Whether two rotations are the same turn (either sign, nearly exactly).
    /// </summary>
    private static bool Same(Quaternion a, Quaternion b) => MathF.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b))) > 0.99999f;

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
    public void ARoomsContentsAreRolledTheGamesWay()
    {
        // What the game made in that Frost Cave, as its save holds it (all 235 found there, at these
        // places): the rooms' networked objects its random parts kept, rolled from each room's place.
        var expected = new Dictionary<string, int>
        {
            ["CastleKit_metal_groundtorch_unlit"] = 6,
            ["Ice_floor"] = 5,
            ["MountainKit_brazier"] = 17,
            ["MountainKit_wood_gate"] = 2,
            ["Pickable_ForestCryptRemains01"] = 4,
            ["Pickable_ForestCryptRemains02"] = 5,
            ["Pickable_ForestCryptRemains03"] = 2,
            ["Pickable_MeatPile"] = 24,
            ["Pickable_MountainCaveCrystal"] = 4,
            ["Pickable_MountainCaveRandom"] = 12,
            ["Spawner_Cultist"] = 5,
            ["Spawner_Ulv"] = 16,
            ["caverock_ice_pillar_wall"] = 2,
            ["caverock_ice_stalagmite"] = 16,
            ["caverock_ice_stalagtite"] = 60,
            ["cloth_hanging_door"] = 4,
            ["cloth_hanging_long"] = 6,
            ["fenrirhide_hanging"] = 6,
            ["hanging_hairstrands"] = 21,
            ["lox_ribs"] = 6,
            ["mountainkit_chair"] = 11,
            ["mountainkit_table"] = 1,
        };
        var kind = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
        var made = Cave().SelectMany(r => Dungeons.Contents(r, kind, CaveAt, 509094289)).ToList();
        Assert.Equal(235, made.Count);
        Assert.Equal(expected, made.GroupBy(m => m.Prefab).ToDictionary(g => g.Key, g => g.Count()));
        var first = Dungeons.Contents(Cave()[0], kind, CaveAt, 509094289)[0];
        Assert.Equal("caverock_ice_stalagtite", first.Prefab);
        Assert.Equal(new Vector3(3.01f, 5165.703f, -2642.79f), first.Position, new Vector3Comparer(1e-3f));
        // The roll leaves the game's random numbers as they were (the world generator shares them).
        ValheimGen.UnityEngine.Random.InitState(42);
        float a = ValheimGen.UnityEngine.Random.value;
        ValheimGen.UnityEngine.Random.InitState(42);
        Dungeons.Contents(Cave()[1], kind, CaveAt, 1);
        Assert.Equal(a, ValheimGen.UnityEngine.Random.value);
        // A room placed elsewhere rolls differently.
        var moved = Cave()[5] with { Position = Cave()[5].Position + new Vector3(24, 0, 0) };
        Assert.NotEqual(Dungeons.Contents(Cave()[5], kind, CaveAt, 1).Select(m => m.Prefab), Dungeons.Contents(moved, kind, CaveAt, 1).Select(m => m.Prefab));
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

    /// <summary>
    /// Positions equal when within this distance of each other.
    /// </summary>
    private sealed class Vector3Comparer(float tolerance) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

        public int GetHashCode(Vector3 v) => 0;
    }

    [Fact]
    public void TheDungeonsOfASaveAreFoundWithTheirRoomCounts()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        Assert.Empty(Dungeons.In(world));
        var at = new Vector3(640, 30, 640);
        List<Dungeons.Placed> rooms =
        [
            new(StableHash.Of("cave_new_entrance02"), at + new Vector3(0, 5000, 0), Quaternion.Identity),
            new(StableHash.Of("cave_new_endcap02"), at + new Vector3(0, 5000, 8), Quaternion.Identity),
        ];
        world.MergeLive([(10, 10)],
        [
            (4242, 1, MadeObjects.Dungeon("DG_Cave", at, rooms)),
            // A room list too short to hold its count: no rooms read, the dungeon still found.
            (4242, 2, MadeObjects.Dungeon("DG_Cave", at + new Vector3(10, 0, 0), [1, 2])),
            // Not a dungeon.
            (4242, 3, MadeObjects.Zdo("Beech1", at + new Vector3(20, 0, 0))),
        ], _ => false, _ => false);
        var found = Dungeons.In(world).OrderBy(f => f.Position.X).ToList();
        Assert.Equal(2, found.Count);
        Assert.All(found, f => Assert.Equal("DG_Cave", f.Kind.Name));
        Assert.Equal(at, found[0].Position);
        Assert.Equal(2, found[0].Rooms);
        Assert.Equal(0, found[1].Rooms);
    }

    [Fact]
    public void ADungeonIsReadInItsFilesWorldVersion()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        // Objects read from now on are of version 39: each carries its sector after its flags.
        world.Chunks[0].WorldVersion = 39;
        var at = new Vector3(640, 30, 640);
        List<Dungeons.Placed> rooms =
        [
            new(StableHash.Of("cave_new_entrance02"), at + new Vector3(0, 5000, 0), Quaternion.Identity),
            new(StableHash.Of("cave_new_endcap02"), at + new Vector3(0, 5000, 8), Quaternion.Identity),
        ];
        byte[] current = MadeObjects.Dungeon("DG_Cave", at, rooms);
        world.MergeLive([(10, 10)], [(4242, 1, [.. current[..2], 0, 0, 0, 0, .. current[2..]])], _ => false, _ => false);
        var found = Assert.Single(Dungeons.In(world));
        Assert.Equal(2, found.Rooms);
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
