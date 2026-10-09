using System.Numerics;

namespace TerrainEditor.Editing;

// Dungeons made from a few settings and a seed (the same seed makes the same dungeon), in two ways:
// - from the game's own rooms (Rooms): an entrance, then rooms of the dungeon's kind joined at their
//   openings while they fit (the game's rules: Dungeons.Attach, no overlap, inside its space), then the
//   game's end cap on every open end; each room with what the game puts in it, and doors where rooms
//   meet. The game builds it from its room list, like any dungeon.
// - from building pieces (Pieces): levels of rooms joined by corridors on an 8 m grid, stairs between
//   levels, a boss hall and a vault on the last, chests in dead ends. Floors and roofs are black marble
//   slabs, the one piece the game never checks for support; everything else stands on them, so it holds
//   anywhere, 5000 m up as on the ground.
// Positions are metres from the dungeon's origin (its entrance; y up from the first floor).
public static class DungeonGen
{
	public enum Made { Rooms, Pieces }

	// What a biome's dungeon is made of and holds. Wall: a wall tile (W × H m, Bottom: its foot below its
	// position) and the post that closes its corners; Rooms: the kind of the game's dungeon of that biome.
	public sealed record Biome(string Name, string Wall, float W, float H, float Bottom, string Post, string Light, string[] Foes, string[] Bosses,
		string Chest, string[] Props, string Rooms);

	public static readonly Biome[] Biomes =
	{
		new("Meadows", "woodwall", 2, 2, 1, "wood_pole2", "CastleKit_groundtorch", new[] { "Spawner_Skeleton_Meadows", "Spawner_Skeleton" },
			new[] { "Spawner_Skeleton_rise" }, "TreasureChest_meadows", new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02" }, "DG_ForestCrypt"),
		new("Black Forest", "stone_wall_4x2", 4, 2, 1, "stone_pillar", "CastleKit_groundtorch_green",
			new[] { "Spawner_Greydwarf", "Spawner_Greydwarf_Shaman", "Spawner_Skeleton", "Spawner_Ghost" }, new[] { "Spawner_Troll" }, "TreasureChest_forestcrypt",
			new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Pickable_ForestCryptRemains03", "crypt_skeleton_chest" }, "DG_ForestCrypt"),
		new("Swamp", "stone_wall_4x2", 4, 2, 1, "stone_pillar", "CastleKit_groundtorch_green",
			new[] { "Spawner_Draugr", "Spawner_Draugr_Ranged", "Spawner_Blob", "Spawner_Skeleton_poison" }, new[] { "Spawner_Draugr_Elite", "Spawner_Wraith" }, "TreasureChest_sunkencrypt",
			new[] { "Pickable_ForestCryptRemains02", "lox_ribs", "Pickable_MeatPile" }, "DG_SunkenCrypt"),
		new("Mountain", "stone_wall_4x2", 4, 2, 1, "stone_pillar", "MountainKit_brazier",
			new[] { "Spawner_Ulv", "Spawner_Cultist", "Spawner_Bat", "Spawner_Skeleton_Mountains" }, new[] { "Spawner_StoneGolem", "Spawner_Fenring" }, "TreasureChest_mountaincave",
			new[] { "lox_ribs", "Pickable_MeatPile", "Pickable_MountainCaveCrystal" }, "DG_Cave"),
		new("Plains", "woodwall", 2, 2, 1, "wood_pole2", "CastleKit_groundtorch",
			new[] { "Spawner_Goblin", "Spawner_GoblinArcher", "Spawner_GoblinShaman" }, new[] { "Spawner_GoblinBrute" }, "TreasureChest_plains_stone",
			new[] { "lox_ribs", "Pickable_MeatPile" }, "DG_GoblinCamp"),
		new("Mistlands", "piece_dvergr_metal_wall_2x2", 2, 2, 1, "wood_pole2", "MountainKit_brazier_blue",
			new[] { "Spawner_Seeker", "Spawner_Tick", "Spawner_Seeker" }, new[] { "Spawner_SeekerBrute" }, "TreasureChest_dvergrtown",
			new[] { "Pickable_MountainCaveCrystal", "lox_ribs" }, "DG_DvergrTown"),
		new("Ashlands", "Piece_grausten_wall_4x2", 4, 2, 0, "stone_pillar", "CastleKit_brazier",
			new[] { "Spawner_Charred", "Spawner_Charred_Archer", "Spawner_Charred_Mage" }, new[] { "Spawner_CharredStone_Elite", "Spawner_FallenValkyrie" }, "TreasureChest_charredfortress",
			new[] { "lox_ribs", "Pickable_ForestCryptRemains03" }, "DG_MorkHalla"),
		new("Deep North", "stave_wall_2x2", 2, 2, 1, "wood_pole2", "MountainKit_brazier",
			new[] { "Spawner_JotunWarrior", "Spawner_JotunWitch", "Spawner_Frysling" }, new[] { "Spawner_JotunDualWield" }, "TreasureChest_deepnorth_village",
			new[] { "lox_ribs", "Pickable_MountainCaveCrystal" }, "DG_Cave"),
	};

	public static Biome BiomeOf(string name) => Biomes.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Biomes[1];

	// Size: cells a side (8 m each; rooms: about that many rooms); Levels: floors (pieces); Monsters and
	// Loot: 0 none, 1 usual, 2 twice as many; Foes: the spawners to use (none: the biome's); Kind: the
	// game dungeon's kind (rooms; none: the biome's).
	public sealed record Settings(Made Made = Made.Pieces, string Biome = "Black Forest", int Size = 7, int Levels = 2, float Monsters = 1, float Loot = 1,
		int Seed = 1, IReadOnlyList<string>? Foes = null, string? Kind = null);

	public sealed record Item(string Prefab, Vector3 Position, Quaternion Rotation);

	// Items: the objects to place (pieces, spawners, chests, doors, room contents); Rooms: the game rooms'
	// list (Made.Rooms) for a dungeon object of Kind at the origin; Arrival: where to stand on arriving.
	public sealed record Result(List<Item> Items, Vector3 Arrival, Dungeons.Kind? Kind, List<Dungeons.Placed>? Rooms, List<string> Notes);

	public static Result Make(Settings s, int worldSeed = 0) => s.Made == Made.Rooms ? MakeRooms(s, worldSeed) : MakePieces(s);

	private static Quaternion Yaw(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);

	// What stands on a floor goes 5 cm up: a piece whose middle of mass is right on a floor's surface gets
	// no support from it in game.
	private const float Lift = 0.05f;

	// ---- From the game's rooms.
	private static Result MakeRooms(Settings s, int worldSeed)
	{
		var notes = new List<string>();
		var biome = BiomeOf(s.Biome);
		var kind = Dungeons.Kinds.FirstOrDefault(k => k.Name == (s.Kind ?? biome.Rooms)) ?? Dungeons.KindOf(Save.StableHash.Of("DG_Cave"))!;
		var rnd = new Random(s.Seed);
		var all = Dungeons.RoomsFor(kind).ToList();
		var entrances = all.Where(r => r.Entrance && !r.EndCap).ToList();
		var normal = all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList();
		var caps = all.Where(r => r.EndCap && r.Openings.Length == 1).ToList();
		var rooms = new List<Dungeons.Placed>();
		var origin = Vector3.Zero;
		var first = entrances.Count > 0 ? entrances[rnd.Next(entrances.Count)] : normal[rnd.Next(normal.Count)];
		rooms.Add(new Dungeons.Placed(first.Hash, origin, Quaternion.Identity));
		int target = Math.Max(2, s.Size * 3);
		for (int tries = 0; rooms.Count < target && tries < target * 40; tries++)
		{
			var ends = Dungeons.Openings(rooms, freeOnly: true);
			if (ends.Count == 0)
			{
				break;
			}
			var end = ends[rnd.Next(ends.Count)];
			var fits = normal.Where(r => Dungeons.Matching(r, end.Type).Any()).ToList();
			if (fits.Count == 0)
			{
				continue;
			}
			// Weighted as the game weighs its rooms (Room.m_weight).
			float total = fits.Sum(r => r.Weight), roll = (float)rnd.NextDouble() * total;
			var room = fits.First(r => (roll -= r.Weight) <= 0 || r == fits[^1]);
			var mine = Dungeons.Matching(room, end.Type).ToList();
			var p = Dungeons.Attach(room, mine[rnd.Next(mine.Count)], end.Position, end.Rotation);
			if (Dungeons.Overlaps(rooms, p).Count == 0 && Dungeons.Inside(kind, origin, p))
			{
				rooms.Add(p);
			}
		}
		// End caps on every open end; the entrance's way in too (no location's tunnel joins it here).
		var open = Dungeons.Openings(rooms, freeOnly: true).Concat(Dungeons.Openings(rooms, freeOnly: false).Where(e => e.Entrance)).ToList();
		int left = 0;
		foreach (var end in open)
		{
			var fit = caps.Where(c => c.Openings[0].Type == end.Type).ToList();
			if (fit.Count == 0)
			{
				left++;
				continue;
			}
			var cap = fit[rnd.Next(fit.Count)];
			rooms.Add(Dungeons.Attach(cap, cap.Openings[0], end.Position, end.Rotation));
		}
		if (left > 0)
		{
			notes.Add($"{left} open end(s) have no end cap of their kind");
		}
		// What the game puts in the rooms, then doors where rooms meet.
		var items = new List<Item>();
		foreach (var r in rooms)
		{
			foreach (var m in Dungeons.Contents(r, kind, origin, worldSeed))
			{
				items.Add(new Item(m.Prefab, m.Position, m.Rotation));
			}
		}
		foreach (var j in Dungeons.Joints(rooms))
		{
			if (Dungeons.RollDoor(kind, j, () => (float)rnd.NextDouble()) is string door)
			{
				items.Add(new Item(door, j.Position, j.Rotation));
			}
		}
		// Monsters: the rooms' own spawners, fewer or more, or others picked.
		var spawners = items.Where(i => i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal)).ToList();
		foreach (var sp in spawners)
		{
			items.Remove(sp);
			int copies = (int)MathF.Floor(s.Monsters) + (rnd.NextDouble() < s.Monsters - MathF.Floor(s.Monsters) ? 1 : 0);
			for (int c = 0; c < copies; c++)
			{
				string prefab = s.Foes is { Count: > 0 } foes ? foes[rnd.Next(foes.Count)] : sp.Prefab;
				items.Add(sp with { Prefab = prefab, Position = sp.Position + (c == 0 ? Vector3.Zero : new Vector3((float)rnd.NextDouble() * 2 - 1, 0, (float)rnd.NextDouble() * 2 - 1)) });
			}
		}
		// Loot: the rooms' chests kept by chance (fewer), or more chests beside them.
		var chests = items.Where(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)).ToList();
		foreach (var ch in chests)
		{
			if (s.Loot < 1 && rnd.NextDouble() > s.Loot)
			{
				items.Remove(ch);
			}
			else if (s.Loot > 1 && rnd.NextDouble() < s.Loot - 1)
			{
				items.Add(ch with { Position = ch.Position + Vector3.Transform(new Vector3(1.2f, 0, 0), ch.Rotation) });
			}
		}
		// Arriving in the entrance room, near its middle (the floor is found where it is placed).
		var arrival = rooms[0].Position;
		notes.Add($"{Describe(kind)}: {rooms.Count} rooms, {items.Count} objects");
		return new Result(items, arrival, kind, rooms, notes);
	}

	public static string Describe(Dungeons.Kind k) => k.Name.Replace("DG_", "", StringComparison.Ordinal);

	// ---- From building pieces.
	private const float Cell = 8f, LevelHeight = 6f;

	// A level's cells (8 m) by room; corridors are all one group, so they meet without walls. Doors: where
	// a room meets a corridor or another room, an iron gate (mobs don't open doors, so each room's foes
	// wait behind theirs). Holes: the stairwell over the stairs from the level below, two cells long so
	// nobody climbing hits the floor's edge; it sits in a gated landing room.
	private const int Corridor = -1;

	private sealed class Level
	{
		public readonly Dictionary<(int, int), int> Group = new();
		public readonly HashSet<((int, int), (int, int))> Doors = new();
		public readonly List<(int X0, int Z0, int W, int H, string Kind)> Rooms = new();
		public readonly HashSet<(int, int)> Holes = new();
		public readonly HashSet<(int, int)> StairCells = new();
	}

	private static Result MakePieces(Settings s)
	{
		var biome = BiomeOf(s.Biome);
		int size = Math.Clamp(s.Size, 3, 15), levels = Math.Clamp(s.Levels, 1, 5);
		var rnd = new Random(s.Seed);
		var items = new List<Item>();
		var notes = new List<string>();
		var lv = Enumerable.Range(0, levels).Select(_ => new Level()).ToList();
		int next = 0;
		(int, int)? forced = null;
		var stairs = new List<(int Level, int I, int J)>();
		int roomsPerLevel = Math.Max(2, size * size / 12);
		for (int k = 0; k < levels; k++)
		{
			var L = lv[k];
			bool Free(int x0, int z0, int w, int h) =>
				x0 >= 0 && z0 >= 0 && x0 + w <= size && z0 + h <= size &&
				!L.Rooms.Any(r => x0 < r.X0 + r.W + 1 && r.X0 < x0 + w + 1 && z0 < r.Z0 + r.H + 1 && r.Z0 < z0 + h + 1);
			void AddRoom(int x0, int z0, int w, int h, string kind)
			{
				int id = next++;
				L.Rooms.Add((x0, z0, w, h, kind));
				for (int i = x0; i < x0 + w; i++)
				{
					for (int j = z0; j < z0 + h; j++)
					{
						L.Group[(i, j)] = id;
					}
				}
			}
			if (k == 0)
			{
				AddRoom(Math.Max(0, size / 2 - 1), 0, Math.Min(3, size), Math.Min(2, size), "entrance");
			}
			else if (forced is var (fi, fj))
			{
				// Over the stairwell (fj - 1, fj) and the cell the stairs come up to.
				AddRoom(fi, fj - 1, 1, 3, "landing");
			}
			if (size >= 5)
			{
				string big = k == levels - 1 ? "boss" : "hall";
				for (int t = 0; t < 200; t++)
				{
					int w = 3 + rnd.Next(2), h = 3;
					int x0 = rnd.Next(Math.Max(1, size - w + 1)), z0 = rnd.Next(size / 2, Math.Max(size / 2 + 1, size - h + 1));
					if (Free(x0, z0, w, h))
					{
						AddRoom(x0, z0, w, h, big);
						break;
					}
				}
			}
			for (int t = 0, n = 0; t < 400 && n < roomsPerLevel; t++)
			{
				int w = 1 + rnd.Next(3), h = 1 + rnd.Next(3);
				int x0 = rnd.Next(Math.Max(1, size - w + 1)), z0 = rnd.Next(Math.Max(1, size - h + 1));
				if (Free(x0, z0, w, h))
				{
					AddRoom(x0, z0, w, h, "room");
					n++;
				}
			}
			var joined = new List<int> { 0 };
			var order = Enumerable.Range(1, L.Rooms.Count - 1).ToList();
			while (order.Count > 0)
			{
				var best = order.SelectMany(a => joined.Select(b => (a, b))).MinBy(p => Dist(L.Rooms[p.a], L.Rooms[p.b]));
				Carve(L, Center(L.Rooms[best.a]), Center(L.Rooms[best.b]), size, rnd);
				joined.Add(best.a);
				order.Remove(best.a);
			}
			if (L.Rooms.Count > 4)
			{
				Carve(L, Center(L.Rooms[1]), Center(L.Rooms[^1]), size, rnd);
			}
			forced = null;
			if (k < levels - 1)
			{
				var spots = L.Group.Keys.Where(c => L.Group.TryGetValue((c.Item1, c.Item2 - 1), out int g) && g == L.Group[c] && c.Item2 + 1 < size
					&& L.Rooms.Any(r => r.Kind is "room" or "hall" && In(r, c))).OrderBy(c => c).ToList();
				if (spots.Count > 0)
				{
					var (si, sj) = spots[rnd.Next(spots.Count)];
					stairs.Add((k, si, sj));
					forced = (si, sj);
					lv[k + 1].Holes.Add((si, sj));
					lv[k + 1].Holes.Add((si, sj - 1));
					L.StairCells.Add((si, sj));
					L.StairCells.Add((si, sj - 1));
				}
				else
				{
					notes.Add($"no place for stairs down from level {k + 1}: the levels below are left out");
					levels = k + 1;
					lv = lv.Take(levels).ToList();
					break;
				}
			}
		}
		var last = lv[^1];
		var vault = last.Rooms.Where(r => r.Kind == "room").OrderByDescending(r => r.W * r.H).FirstOrDefault();
		if (vault != default && last.Rooms.Count > 2)
		{
			last.Rooms[last.Rooms.IndexOf(vault)] = vault with { Kind = "vault" };
		}

		void Add(string prefab, float x, float y, float z, float yaw = 0) => items.Add(new Item(prefab, new Vector3(x, y, z), Yaw(yaw)));
		float CX(int i) => (i - size / 2) * Cell;
		float CZ(int j) => j * Cell;
		bool thin = biome.Post != "stone_pillar";
		for (int k = 0; k < levels; k++)
		{
			var L = lv[k];
			float F = k * LevelHeight;
			foreach (var c in L.Group.Keys)
			{
				if (!L.Holes.Contains(c))
				{
					Add("blackmarble_floor_large", CX(c.Item1), F - 1, CZ(c.Item2));
				}
				bool above = k + 1 < levels && (lv[k + 1].Group.ContainsKey(c) || lv[k + 1].Holes.Contains(c));
				if (!above)
				{
					Add("blackmarble_floor_large", CX(c.Item1), F + 5, CZ(c.Item2));
				}
			}
			var corners = new HashSet<(int, int)>();
			foreach (var (c, g) in L.Group)
			{
				foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
				{
					var n = (c.Item1 + di, c.Item2 + dj);
					bool inside = L.Group.TryGetValue(n, out int other);
					if (inside && other == g || inside && (n.Item1 < c.Item1 || n.Item2 < c.Item2))
					{
						continue;
					}
					bool gate = inside && (L.Doors.Contains((c, n)) || L.Doors.Contains((n, c)));
					float ex = CX(c.Item1) + 4 * di, ez = CZ(c.Item2) + 4 * dj, yaw = di != 0 ? 90 : 0;
					void Along(string p, float t, float y) => Add(p, di != 0 ? ex : ex + t, y, di != 0 ? ez + t : ez, yaw);
					if (gate)
					{
						// An iron gate (2 m wide, 3 m high, in its own frame) in a stone surround.
						foreach (float y in new[] { F + 0.5f, F + 1.5f, F + 2.5f })
						{
							Along("stone_wall_2x1", -3, y);
							Along("stone_wall_2x1", 3, y);
							Along("stone_wall_1x1", -1.5f, y);
							Along("stone_wall_1x1", 1.5f, y);
						}
						foreach (float t in new[] { -3f, -1f, 1f, 3f })
						{
							Along("stone_wall_2x1", t, F + 3.5f);
						}
						Along("dungeon_sunkencrypt_irongate", 0, F);
					}
					else
					{
						// Wall tiles over the 8 m edge, 4 m high.
						for (float t = -4 + biome.W / 2; t < 4; t += biome.W)
						{
							for (float y = 0; y < 4 - 1e-3f; y += biome.H)
							{
								Along(biome.Wall, t, F + y + biome.Bottom);
							}
						}
					}
					corners.Add(di != 0 ? (2 * c.Item1 + di, 2 * c.Item2 - 1) : (2 * c.Item1 - 1, 2 * c.Item2 + dj));
					corners.Add(di != 0 ? (2 * c.Item1 + di, 2 * c.Item2 + 1) : (2 * c.Item1 + 1, 2 * c.Item2 + dj));
				}
			}
			foreach (var (ci, cj) in corners)
			{
				Add(biome.Post, (ci - 2 * (size / 2)) * 4, F + 1, cj * 4);
				Add(biome.Post, (ci - 2 * (size / 2)) * 4, F + 3, cj * 4);
			}
			// The stairwell's sides through the floor's thickness, where no floor slab closes them; they
			// stand on the wall below there (where the room below goes on, its roof slab closes it).
			foreach (var (hi, hj) in L.Holes)
			{
				foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
				{
					var n = (hi + di, hj + dj);
					var below = lv[k - 1].Group;
					if (L.Holes.Contains(n) || L.Group.ContainsKey(n) || below.TryGetValue(n, out int g) && g == below[(hi, hj)])
					{
						continue;
					}
					for (float t = -4 + biome.W / 2; t < 4; t += biome.W)
					{
						for (float y = -2; y < -1e-3f; y += biome.H)
						{
							Add(biome.Wall, CX(hi) + (di != 0 ? 4 * di : t), F + y + biome.Bottom, CZ(hj) + (dj != 0 ? 4 * dj : t), di != 0 ? 90 : 0);
						}
					}
				}
			}
			Furnish(L, biome, s, F, Add, CX, CZ, rnd, k == 0, size);
		}
		foreach (var (k, si, sj) in stairs)
		{
			float F = k * LevelHeight, x = CX(si) - 2.5f, z0 = CZ(sj) - 8;
			for (int st = 0; st < 6; st++)
			{
				float zc = z0 + 2 * st + 1;
				Add("stone_stair", x, F + st, zc, 180);
				for (int b = 0; b < st; b++)
				{
					Add("stone_wall_2x1", x, F + b + 0.5f, zc, 90);
				}
			}
		}
		_ = thin;
		notes.Add($"{biome.Name}: {levels} level(s), {lv.Sum(l => l.Rooms.Count)} rooms, {items.Count} objects");
		return new Result(items, new Vector3(0, Lift, 0), null, null, notes);
	}

	// Where corridors leave a room from: its middle; a landing's, the cell the stairs come up to.
	private static (int, int) Center((int X0, int Z0, int W, int H, string Kind) r) =>
		r.Kind == "landing" ? (r.X0, r.Z0 + 2) : (r.X0 + r.W / 2, r.Z0 + r.H / 2);

	private static int Dist((int X0, int Z0, int W, int H, string Kind) a, (int X0, int Z0, int W, int H, string Kind) b)
	{
		var (ax, az) = Center(a);
		var (bx, bz) = Center(b);
		return Math.Abs(ax - bx) + Math.Abs(az - bz);
	}

	private static bool In((int X0, int Z0, int W, int H, string Kind) r, (int, int) c) => c.Item1 >= r.X0 && c.Item1 < r.X0 + r.W && c.Item2 >= r.Z0 && c.Item2 < r.Z0 + r.H;

	// A corridor from a to b: the shortest way over the grid, around stairwells, as straight as it can
	// go. Cells no room holds become corridor; a gate stands wherever the way passes from one room (or
	// corridor) to another.
	private static void Carve(Level L, (int, int) a, (int, int) b, int size, Random rnd)
	{
		var from = new Dictionary<(int, int), (int, int)> { [a] = a };
		var queue = new Queue<(int, int)>();
		queue.Enqueue(a);
		var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
		while (queue.Count > 0 && !from.ContainsKey(b))
		{
			var c = queue.Dequeue();
			var (px, pz) = from[c];
			var dir = (c.Item1 - px, c.Item2 - pz);
			// Keep going the same way first, then the others in a random order.
			foreach (var d in steps.OrderBy(d => d == dir ? -1 : rnd.Next(4)))
			{
				var n = (c.Item1 + d.Item1, c.Item2 + d.Item2);
				if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= size || n.Item2 >= size || L.Holes.Contains(n) || from.ContainsKey(n))
				{
					continue;
				}
				from[n] = c;
				queue.Enqueue(n);
			}
		}
		if (!from.ContainsKey(b))
		{
			return;
		}
		var path = new List<(int, int)>();
		for (var c = b; c != a; c = from[c])
		{
			path.Add(c);
		}
		path.Add(a);
		path.Reverse();
		for (int i = 0; i < path.Count; i++)
		{
			L.Group.TryAdd(path[i], Corridor);
			if (i > 0 && L.Group[path[i]] != L.Group[path[i - 1]])
			{
				L.Doors.Add((path[i - 1], path[i]));
			}
		}
	}

	private static void Furnish(Level L, Biome biome, Settings s, float F, Action<string, float, float, float, float> add, Func<int, float> cx, Func<int, float> cz,
		Random rnd, bool first, int size)
	{
		var ways = new Dictionary<int, int>();
		foreach (var (a, b) in L.Doors)
		{
			ways[L.Group[a]] = ways.GetValueOrDefault(L.Group[a]) + 1;
			ways[L.Group[b]] = ways.GetValueOrDefault(L.Group[b]) + 1;
		}
		var foes = s.Foes is { Count: > 0 } f ? f : biome.Foes;
		float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
		int Count(float n) => (int)MathF.Floor(n) + (rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);
		(int, int) CellOf(float x, float z) => ((int)MathF.Round(x / Cell) + size / 2, (int)MathF.Round(z / Cell));
		foreach (var r in L.Rooms)
		{
			float x0 = cx(r.X0) - 4, z0 = cz(r.Z0) - 4, x1 = cx(r.X0 + r.W - 1) + 4, z1 = cz(r.Z0 + r.H - 1) + 4;
			void Put(string p, float x, float z, float yaw = -1)
			{
				var c = CellOf(x, z);
				if (!L.StairCells.Contains(c) && !L.Holes.Contains(c))
				{
					add(p, x, F + Lift, z, yaw < 0 ? R(0, 360) : yaw);
				}
			}
			foreach (var (x, z) in new[] { (x0 + 1.5f, z0 + 1.5f), (x1 - 1.5f, z1 - 1.5f), (x0 + 1.5f, z1 - 1.5f), (x1 - 1.5f, z0 + 1.5f) })
			{
				if (r.W * r.H > 1 || rnd.Next(2) == 0)
				{
					Put(biome.Light, x, z, 0);
				}
			}
			int area = r.W * r.H;
			float mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
			switch (r.Kind)
			{
				case "entrance":
					break;
				case "boss":
					foreach (int i in Enumerable.Range(0, Math.Max(1, Count(1.5f * s.Monsters))))
					{
						Put(biome.Bosses[i % biome.Bosses.Length], mx + (i - 0.5f) * 4, mz, 180);
					}
					for (int i = 0; i < Count(3 * s.Monsters); i++)
					{
						Put(foes[rnd.Next(foes.Count)], R(x0 + 2, x1 - 2), R(z0 + 2, z1 - 2));
					}
					for (float x = x0 + 8; x < x1 - 1; x += 8)
					{
						foreach (float z in new[] { mz - 4, mz + 4 })
						{
							add("stone_pillar", x, F + 1, z, 0);
							add("stone_pillar", x, F + 3, z, 0);
						}
					}
					break;
				case "vault":
					for (int i = 0; i < Math.Max(1, Count(Math.Min(4, 1 + area) * s.Loot)); i++)
					{
						Put(biome.Chest, R(x0 + 2, x1 - 2), R(z0 + 2, z1 - 2));
					}
					for (int i = 0; i < Count(s.Monsters); i++)
					{
						Put(biome.Bosses[rnd.Next(biome.Bosses.Length)], mx, mz);
					}
					break;
				default:
					for (int i = 0; i < Count((area / 2f + (r.Kind == "hall" ? 2 : 0)) * s.Monsters); i++)
					{
						Put(foes[rnd.Next(foes.Count)], R(x0 + 2, x1 - 2), R(z0 + 2, z1 - 2));
					}
					for (int i = 0; i < 1 + area; i++)
					{
						Put(biome.Props[rnd.Next(biome.Props.Length)], R(x0 + 1.5f, x1 - 1.5f), R(z0 + 1.5f, z1 - 1.5f));
					}
					if (!first && ways.GetValueOrDefault(L.Group[(r.X0, r.Z0)]) <= 1 && rnd.NextDouble() < s.Loot)
					{
						Put(biome.Chest, R(x0 + 2, x1 - 2), R(z0 + 2, z1 - 2));
					}
					break;
			}
		}
	}
}
