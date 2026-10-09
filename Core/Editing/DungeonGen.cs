using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

// Dungeons made from settings and a seed (the same settings make the same dungeon), in two ways:
// - from the game's own rooms (Rooms): an entrance, then rooms of the dungeon's kind joined at their
//   openings while they fit (the game's rules: Dungeons.Attach, no overlap, inside its space), then the
//   game's end cap on every open end; each room with what the game puts in it, and doors where rooms
//   meet. The game builds it from its room list, like any dungeon.
// - from building pieces (Pieces): a style (crypt, catacombs, temple, fortress, prison, Dvergr hold,
//   goblin warren, ruins) gives the rooms and how they are laid out (DungeonPlanner), the walls are the
//   biome's or those picked (DungeonShell), and each room is furnished for what it is, lit, guarded and
//   stocked (DungeonDresser): an entrance with the dungeon's name, halls, side rooms, loops, stairs
//   down, an antechamber, the boss's arena behind a gate whose key lies in a chest somewhere, the
//   treasury beyond, a hidden cache or two. Floors and roofs are black marble slabs, the one piece the
//   game never checks for support; everything else stands on them, so it holds anywhere, 5000 m up as
//   on the ground.
// Positions are metres from the dungeon's origin (its entrance; y up from the first floor).
public static class DungeonGen
{
	public enum Made { Rooms, Pieces }

	// Style: a DungeonKit style (none: the biome's); Walls: a material (none: the biome's); Size: 1 small
	// to 4 huge; Levels: 1 to 4; Monsters: 0 none, 1 usual, up to 3; Respawn: spawners (they come back)
	// or the creatures once; Foes: spawners or creatures to use (none: the biome's); Boss: its spawner or
	// creature ("": none, null: the biome's); Loot: 0 none to 2 rich; Light: 0 dark, 1 dim, 2 bright;
	// Decor: 0 bare, 1 usual, 2 rich; Decay: 0 kept to 1 ruined (null: the style's); Loops: how many ways
	// round (0 none, 1 usual, 2 many); BossKey: the arena locked, its key in a chest; Secrets: hidden
	// caches; Name: on the entrance's sign (null: one is made up); Kind: the game dungeon's (rooms).
	public sealed record Settings(Made Made = Made.Pieces, string Biome = "Black Forest", string? Style = null, string? Walls = null, int Size = 2, int Levels = 2,
		float Monsters = 1, bool Respawn = true, IReadOnlyList<string>? Foes = null, string? Boss = null, float Loot = 1, int Light = 1, int Decor = 1,
		float? Decay = null, float Loops = 1, bool BossKey = true, bool Secrets = true, int Seed = 1, string? Name = null, string? Kind = null);

	// An object to place; Data: values of its object data (section, key, value as text; see ZdoData.Set).
	public sealed record Item(string Prefab, Vector3 Position, Quaternion Rotation, IReadOnlyList<(string Section, string Key, string Value)>? Data = null);

	// A room of the plan (metres, its level's floor at Y), for a map.
	public sealed record PlanRoom(string Name, int Level, float Y, float X0, float Z0, float X1, float Z1, bool Corridor, bool Main, bool Key);

	// A way between two rooms of the map (A, B: their indices): a doorway, or Kind "Stairs".
	public sealed record PlanDoor(int Level, float X, float Z, string Kind, int A = -1, int B = -1);

	// Items: the objects to place (pieces, spawners, chests, doors, room contents); Rooms: the game rooms'
	// list (Made.Rooms) for a dungeon object of Kind at the origin; Arrival: where to stand on arriving
	// (a portal there faces the way in); Name: the dungeon's; Map: its rooms and doors (pieces).
	public sealed record Result(List<Item> Items, Vector3 Arrival, Dungeons.Kind? Kind, List<Dungeons.Placed>? Rooms, List<string> Notes, string Name = "",
		IReadOnlyList<PlanRoom>? Map = null, IReadOnlyList<PlanDoor>? Doors = null);

	public static Result Make(Settings s, int worldSeed = 0) => s.Made == Made.Rooms ? MakeRooms(s, worldSeed) : MakePieces(s);

	// The game's dungeon of a biome, for Made.Rooms.
	private static string RoomsOf(string biome) => biome switch
	{
		"Meadows" or "Black Forest" => "DG_ForestCrypt",
		"Swamp" => "DG_SunkenCrypt",
		"Plains" => "DG_GoblinCamp",
		"Mistlands" => "DG_DvergrTown",
		"Ashlands" => "DG_MorkHalla",
		_ => "DG_Cave",
	};

	// A name for a dungeon of this style (null: the biome's).
	public static string NameFor(int seed, string? style = null, string? biome = null)
	{
		var r = new Random(seed * 7919 + 13);
		var starts = NameStarts(style ?? BiomeOf(biome).Style);
		return $"{starts[r.Next(starts.Length)]} of {NameEnds[r.Next(NameEnds.Length)]}";
	}

	// ---- From the game's rooms.
	private static Result MakeRooms(Settings s, int worldSeed)
	{
		var notes = new List<string>();
		var kind = Dungeons.Kinds.FirstOrDefault(k => k.Name == (s.Kind ?? RoomsOf(s.Biome))) ?? Dungeons.KindOf(Save.StableHash.Of("DG_Cave"))!;
		var rnd = new Random(s.Seed);
		var all = Dungeons.RoomsFor(kind).ToList();
		var entrances = all.Where(r => r.Entrance && !r.EndCap).ToList();
		var normal = all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList();
		var caps = all.Where(r => r.EndCap && r.Openings.Length == 1).ToList();
		var rooms = new List<Dungeons.Placed>();
		var origin = Vector3.Zero;
		var first = entrances.Count > 0 ? entrances[rnd.Next(entrances.Count)] : normal[rnd.Next(normal.Count)];
		rooms.Add(new Dungeons.Placed(first.Hash, origin, Quaternion.Identity));
		int target = 2 + 6 * Math.Clamp(s.Size, 1, 4);
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
	private static Result MakePieces(Settings s)
	{
		var rnd = new Random(s.Seed);
		var biome = BiomeOf(s.Biome);
		var style = StyleOf(s.Style ?? biome.Style);
		var mat = MaterialOf(s.Walls ?? biome.Material);
		var name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, style.Name) : s.Name!;
		var plan = DungeonPlanner.Make(s, style, rnd);
		var o = new DungeonOut();
		DungeonShell.Build(plan, mat, o, s);
		DungeonDresser.Dress(plan, mat, biome, style, s, rnd, o, name);
		var e = plan.Entrance;
		var arrival = new System.Numerics.Vector3(e.CenterX, 0.05f, 2 * e.Z0 + 1.7f);
		var map = plan.Spaces.Select(sp => new PlanRoom(sp.Name, sp.Level, DungeonPlan.FloorY(sp.Level), 2 * sp.X0, 2 * sp.Z0, 2 * (sp.X0 + sp.W), 2 * (sp.Z0 + sp.H),
			sp.Corridor, sp.Main, sp.KeyChest)).ToList();
		var index = plan.Spaces.Select((sp, i) => (sp, i)).ToDictionary(p => p.sp, p => p.i);
		var doors = plan.Doors.Select(d => new PlanDoor(d.A.Level, d.Center.X, d.Center.Z, d.Kind.ToString(), index[d.A], index[d.B])).ToList();
		foreach (var foot in plan.Spaces.Where(sp => sp.Room == Room.StairBottom && sp.Parent != null))
		{
			doors.Add(new PlanDoor(foot.Level, foot.CenterX, foot.CenterZ, "Stairs", index[foot.Parent!], index[foot]));
		}
		var notes = new List<string>(plan.Notes);
		var key = plan.Spaces.FirstOrDefault(sp => sp.KeyChest);
		notes.Insert(0, $"{name}: {style.Name.ToLowerInvariant()} of {mat.Name.ToLowerInvariant()}, {plan.Levels.Count} level(s), {plan.Spaces.Count(sp => sp.IsRoom)} rooms, "
			+ $"{o.Items.Count} objects" + (key != null ? $"; the key is in the {key.Name.ToLowerInvariant()} on level {key.Level + 1}" : ""));
		return new Result(o.Items, arrival, null, null, notes, name, map, doors);
	}
}
