using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// Generated dungeons (DungeonGen): the same settings make the same dungeon; every object is one of the
// game's; it holds in the sky (black marble slabs, nothing falls); every room can be reached, the
// boss's key without going through the boss's gate; doorways open between rooms, not into walls.
public class CoreDungeonGenTests
{
	public static IEnumerable<object[]> Samples()
	{
		int seed = 1;
		foreach (var style in DungeonKit.Styles.Select(s => s.Name))
		{
			foreach (var biome in new[] { "Black Forest", "Swamp", "Mistlands", "Ashlands", "Deep North", "Plains" })
			{
				yield return new object[] { style, biome, 1 + seed % 3, 1 + seed % 3, seed++ };
			}
		}
	}

	private static DungeonGen.Result Make(string style, string biome, int size, int levels, int seed) =>
		DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: style, Size: size, Levels: levels, Seed: seed));

	[Fact]
	public void TheSameSettingsMakeTheSameDungeon()
	{
		var a = Make("Crypt", "Swamp", 3, 2, 42);
		var b = Make("Crypt", "Swamp", 3, 2, 42);
		Assert.Equal(a.Items.Count, b.Items.Count);
		Assert.True(a.Items.Zip(b.Items).All(p => p.First.Prefab == p.Second.Prefab && p.First.Position == p.Second.Position));
		Assert.NotEqual(a.Items.Count, Make("Crypt", "Swamp", 3, 2, 43).Items.Count);
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void ItIsMadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed)
	{
		var r = Make(style, biome, size, levels, seed);
		var unknown = r.Items.Select(i => i.Prefab).Distinct().Where(n => PrefabCatalog.Details(StableHash.Of(n)) == null).ToList();
		Assert.Empty(unknown);
		var pieces = r.Items.Where(i => Stability.Known(i.Prefab)).Select(i => new Stability.Piece(i.Prefab, i.Position, i.Rotation)).ToList();
		var falls = Stability.Solve(pieces, (_, _) => -1000).Falls;
		// What hangs on goblin walls holds in game (they are not pieces: ground to what touches them),
		// but the check does not see those walls.
		bool goblinWalls = biome == "Plains";
		var real = falls.Where(i => !(goblinWalls && pieces[i].Prefab is "sign" or "piece_banner01" or "piece_banner02" or "piece_banner05" or "piece_banner06" or "piece_banner07"))
			.Select(i => $"{pieces[i].Prefab} at {pieces[i].Position}").ToList();
		Assert.Empty(real);
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void EveryRoomCanBeReachedAndTheKeyBeforeTheBoss(string style, string biome, int size, int levels, int seed)
	{
		var r = Make(style, biome, size, levels, seed);
		var map = r.Map!;
		int entrance = map.ToList().FindIndex(m => m.Name == "Entrance hall");
		Assert.True(entrance >= 0);
		HashSet<int> Reach(bool throughKeyGate)
		{
			var seen = new HashSet<int> { entrance };
			var queue = new Queue<int>(seen);
			while (queue.Count > 0)
			{
				int at = queue.Dequeue();
				foreach (var d in r.Doors!.Where(d => d.A == at || d.B == at))
				{
					if (d.Kind == "Key" && !throughKeyGate)
					{
						continue;
					}
					int other = d.A == at ? d.B : d.A;
					if (seen.Add(other))
					{
						queue.Enqueue(other);
					}
				}
			}
			return seen;
		}
		Assert.Equal(map.Count, Reach(true).Count);
		int key = map.ToList().FindIndex(m => m.Key);
		if (key >= 0)
		{
			Assert.Contains(key, Reach(false));
			Assert.DoesNotContain(map.ToList().FindIndex(m => m.Name == "Arena"), Reach(false));
		}
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void RoomsDoNotOverlapAndDoorwaysJoinTheirRooms(string style, string biome, int size, int levels, int seed)
	{
		var r = Make(style, biome, size, levels, seed);
		var map = r.Map!;
		for (int i = 0; i < map.Count; i++)
		{
			for (int j = i + 1; j < map.Count; j++)
			{
				var (a, b) = (map[i], map[j]);
				bool overlap = a.Level == b.Level && a.X0 < b.X1 - 0.01f && b.X0 < a.X1 - 0.01f && a.Z0 < b.Z1 - 0.01f && b.Z0 < a.Z1 - 0.01f;
				Assert.False(overlap && !(a.Corridor && b.Corridor), $"{a.Name} and {b.Name} overlap");
			}
		}
		foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
		{
			// On the edge of both rooms' boxes.
			foreach (var room in new[] { map[d.A], map[d.B] })
			{
				bool onEdge = (MathF.Abs(d.X - room.X0) < 0.01f || MathF.Abs(d.X - room.X1) < 0.01f) && d.Z > room.Z0 && d.Z < room.Z1
					|| (MathF.Abs(d.Z - room.Z0) < 0.01f || MathF.Abs(d.Z - room.Z1) < 0.01f) && d.X > room.X0 && d.X < room.X1;
				Assert.True(onEdge, $"a {d.Kind} doorway at {d.X}, {d.Z} is not on the edge of {room.Name}");
			}
		}
	}

	[Fact]
	public void ItGoesHigherThanWhatIsAlreadyUpThere()
	{
		var r = Make("Crypt", "Swamp", 2, 1, 4);
		var origin = new Vector3(100, 5050, 100);
		Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken(Array.Empty<(int, Vector3)>(), Array.Empty<(Vector3, int)>())));
		// A crypt's entrance nearby that nobody entered yet: its dungeon will come 5000 m above it.
		var crypt = (new Vector3(130, 40, 120), StableHash.Of("Crypt3"));
		var raised = DungeonGen.Clear(origin, r, DungeonGen.Taken(Array.Empty<(int, Vector3)>(), new[] { crypt }));
		Assert.True(raised.Y + r.Items.Min(i => i.Position.Y) - 6 >= 40 + 5250 - 0.01f, $"raised to {raised.Y}");
		// A cave already laid out: its dungeon object keeps the space of its rooms, not just its point.
		var cave = (StableHash.Of("DG_Cave"), new Vector3(150, 5050, 140));
		Assert.True(DungeonGen.Clear(origin, r, DungeonGen.Taken(new[] { cave }, Array.Empty<(Vector3, int)>())).Y > origin.Y);
		// Other locations take nothing.
		Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken(Array.Empty<(int, Vector3)>(), new[] { (new Vector3(130, 40, 120), StableHash.Of("StoneTower1")) })));
	}

	[Fact]
	public void TheKeyChestHoldsTheKey()
	{
		var r = Make("Crypt", "Swamp", 2, 2, 7);
		var chest = r.Items.Single(i => i.Data?.Any(d => d.Key == "items") == true);
		var items = InventoryData.Read(Convert.FromBase64String(chest.Data!.Single(d => d.Key == "items").Value));
		Assert.Contains(items.Items, i => i.Prefab == StableHash.Of("CryptKey"));
		Assert.Contains(chest.Data!, d => d.Key == "addedDefaultItems" && d.Value == "1");
		Assert.Contains(r.Items, i => i.Prefab == "sunken_crypt_gate");
	}

	[Theory]
	[InlineData("Meadows")]
	[InlineData("Black Forest")]
	[InlineData("Swamp")]
	[InlineData("Mountain")]
	[InlineData("Plains")]
	[InlineData("Mistlands")]
	[InlineData("Ashlands")]
	[InlineData("Deep North")]
	public void EveryBiomesOwnRoomsCanBeGenerated(string biome)
	{
		var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: biome, Size: 2, Seed: 5));
		Assert.True(r.Rooms!.Count > 3);
	}

	[Fact]
	public void TheGamesRoomsCanBeGeneratedToo()
	{
		var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3));
		Assert.NotNull(r.Kind);
		Assert.True(r.Rooms!.Count > 5);
		Assert.Empty(Dungeons.Openings(r.Rooms, freeOnly: true));
	}
}
