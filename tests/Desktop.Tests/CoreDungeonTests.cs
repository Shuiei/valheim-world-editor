using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// Dungeons as the Dungeon tool edits them: the room list in a dungeon object, and the game's rules for
// joining rooms at their openings, checked against a Frost Cave the game generated itself.
public class CoreDungeonTests
{
	// The room list (roomData) of a Frost Cave of a real world (30 rooms, DG_Cave at 0, 5165.02, -2654),
	// laid out by the game's generator.
	private const string GeneratedCave = "HgAAAJd4m14AAMA1L2ihRQAgJcUAAACAAAC0QgAAAADXtLSx/v8/wS9goUUAwCTFAAAAgJMOIjcAAAAACRvPzAAAoDUvaKFFAAAkxQAAAIAAAIdDAAAAAA+Nnz/wYya1L3ChRQDgIsUAAAAAk6ImuQAAAAAPjZ8//v+PwS9woUUAACTFAAAAAPv/hkMAAAAAi7rYjhMAQMEveqFFAIAixQAAAAAMAIdDAAAAAHlwoYEAAMDBL1KhRQDAJMUAAACAAQA0QwAAAACZiJRnBQDAwS92oUUAQCPFAAAAAAEAh0MAAAAAY59FIu7/P0EvdqFFAIAixQAAAIADALRCAAAAAPBFMewKAMDBL3qhRQCAIsUAAACAMAC0QgAAAADIKp5/9/+/QS92oUUAgCLFAAAAgAQAtEIAAACAUupdjeb/f0EvaqFFAEAjxQAAAIAEALRCAAAAgIjTrNJhAEDBL2qhRQBAI8UAAACAGAA0QwAAAACI06zS3P8/QS9qoUUAACTFAAAAgAIANEMAAAAAQqG7uAcAwEAvaKFFAMAkxQAAAICTDiI3AAAAgF0FKu0AAMBAL2ihRQAAJMUAAACA3hVzNwAAAICzcESuIDMOuC92oUUAICLFAAAAgOz/s0IAAAAAhyon3v7/78EvdqFFAAAkxQAAAAA13RK5AAAAAIcqJ972/7/BL3ahRQBgJMUAAACADACHQwAAAAB2QLA77f8/wS92oUUAICLFAAAAgC8AtEIAAAAAXgUq7f//v8EvUqFFAGAkxQAAAIABAIdDAAAAAKwp/4AEAJDBL3ahRQBAI8UAAAAAAgA0QwAAAACsKf+A8f8/QS92oUUAICLFAAAAgAQAtEIAAAAAdkCwOwoA8MEvdqFFAIAixQAAAIDDgcU5AAAAAHZAsDv2/7/BL3ahRQAgIsUAAACAMQC0QgAAAACsKf+AHgDAwS92oUUA4CLFAAAAgAwAh0MAAAAAdkCwO/f/70EvdqFFAIAixQAAAAADADRDAAAAAIcqJ975/79BL3ahRQAgIsUAAACABAC0QgAAAICHKife9f+/QS92oUUA4CLFAAAAAAEAh0MAAAAArCn/gMj/v0AvdqFFAEAjxQAAAIBlUDY4AAAAgA==";

	private static readonly Vector3 CaveAt = new(0, 5165.023f, -2654);

	private static List<Dungeons.Placed> Cave() => Dungeons.Read(Convert.FromBase64String(GeneratedCave));

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
		Quaternion q = Dungeons.FromEuler(new Vector3(x, y, z));
		Assert.True(Same(q, Dungeons.FromEuler(Dungeons.ToEuler(q))));
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
		Assert.Throws<InvalidDataException>(() => Dungeons.Read(new byte[] { 200, 0, 0, 0, 1, 2 }));
	}

	private sealed class Vector3Comparer(float tolerance) : IEqualityComparer<Vector3>
	{
		public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

		public int GetHashCode(Vector3 v) => 0;
	}
}
