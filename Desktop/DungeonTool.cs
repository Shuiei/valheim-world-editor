using System.Numerics;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// The Dungeon tool: the rooms of a dungeon of the open area (Frost Cave, crypt...), edited the game's
// way. A room is added at a free opening of another (turned and moved so the two openings meet, see
// Dungeons.Attach), selected, deleted, and the open ends closed with end caps. Each change rewrites the
// dungeon object's room list as one step of the history (Commit), so undo, saving and Apply live work as
// for any other object. Everything here is in Unity's world space; the view hands in rays.
public sealed partial class DungeonTool
{
	// The dungeon being edited: its object's position (an edit makes it a new object of the scene, at
	// the same place).
	public Vector3? Current { get; private set; }

	// The room the next click adds (a name of the catalogue), and which of its openings joins.
	public string? Chosen { get; private set; }

	public int Join { get; private set; }

	// Rooms added with what the game makes in them (chests, creatures' spawners, torches, ice): rolled
	// from the room's place the way the game rolls them (Dungeons.Contents).
	public bool WithContents { get; set; } = true;

	// The room clicked last (an index into the current dungeon's list).
	public int? Selected { get; private set; }

	// What the pointer is over: a free opening (its index in FreeEnds) or a room.
	public int? HoverEnd { get; private set; }

	public int? HoverRoom { get; private set; }

	public event Action? Changed;

	public event Action<string>? Message;

	// The scene: set by the view when it opens an area.
	public WorldScene? Scene { get; set; }

	public IReadOnlyList<DungeonRooms.Dungeon> All => Scene == null ? Array.Empty<DungeonRooms.Dungeon>() : DungeonRooms.Of(Scene);

	public DungeonRooms.Dungeon? Dungeon => Current is { } at ? All.FirstOrDefault(d => Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f) : null;

	public List<Dungeons.OpenEnd> FreeEnds => Dungeon is { } d ? Dungeons.Openings(d.Rooms, freeOnly: true) : new();

	// ---- Names shown for the kinds of dungeon.
	public static string KindName(Dungeons.Kind k) => k.Name switch
	{
		"DG_Cave" => "Frost Cave",
		"DG_SunkenCrypt" => "Sunken Crypt",
		"DG_ForestCrypt" => "Burial Chambers",
		"DG_HalfBurried_ForestCrypt" => "Burial Chambers (half buried)",
		"DG_DvergrTown" => "Infested Mine",
		"DG_DvergrBoss" => "Infested Citadel",
		"DG_Hildir_Cave" => "Howling Cavern (Hildir)",
		"DG_Hildir_ForestCrypt" => "Smouldering Tomb (Hildir)",
		"DG_Hildir_PlainsFortress" => "Sealed Tower (Hildir)",
		"DG_GoblinCamp" => "Fuling village",
		"DG_MeadowsVillage" => "Meadows village",
		"DG_MeadowsFarm" => "Meadows farm",
		_ => Words().Replace(k.Name.Replace("DG_", "", StringComparison.Ordinal).Replace('_', ' '), " $1").Trim(),
	};

	[GeneratedRegex("(?<=[a-z])([A-Z])")]
	private static partial Regex Words();

	public static string Describe(DungeonRooms.Dungeon d) =>
		$"{KindName(d.Kind)}, {d.Rooms.Count} rooms, at {MathF.Round(d.Thing.Position.X) + 0f:0}, {MathF.Round(d.Thing.Position.Z) + 0f:0}";

	// ---- Choosing.
	public void Open(DungeonRooms.Dungeon? d)
	{
		Current = d?.Thing.Position;
		Selected = null;
		HoverEnd = HoverRoom = null;
		Changed?.Invoke();
	}

	public void Choose(string? room)
	{
		Chosen = room;
		Join = 0;
		Changed?.Invoke();
	}

	// The rooms the open dungeon can take, the game's way (its themes), by name.
	public IEnumerable<Dungeons.Room> Palette => Dungeon is { } d ? Dungeons.RoomsFor(d.Kind) : Enumerable.Empty<Dungeons.Room>();

	// ---- The pointer (a ray in world space).
	public void Hover(Vector3 origin, Vector3 dir)
	{
		int? end = null, room = null;
		if (Dungeon is { } d)
		{
			// A free opening near the ray (within 1.5 m of it), the nearest along it.
			float best = float.MaxValue;
			var ends = FreeEnds;
			for (int i = 0; i < ends.Count; i++)
			{
				float t = Vector3.Dot(ends[i].Position - origin, dir);
				if (t <= 0)
				{
					continue;
				}
				float miss = Vector3.Distance(origin + dir * t, ends[i].Position);
				if (miss < 1.5f && t < best)
				{
					best = t;
					end = i;
				}
			}
			if (end == null)
			{
				room = RoomAt(d.Rooms, origin, dir);
			}
		}
		if (end != HoverEnd || room != HoverRoom)
		{
			(HoverEnd, HoverRoom) = (end, room);
			Changed?.Invoke();
		}
	}

	// The room whose box the ray meets first from outside, or the smallest box it starts in (the camera
	// inside a room sees the rooms around it through their walls only with a cut).
	public static int? RoomAt(IReadOnlyList<Dungeons.Placed> rooms, Vector3 origin, Vector3 dir)
	{
		int? hit = null;
		float best = float.MaxValue;
		for (int i = 0; i < rooms.Count; i++)
		{
			var (c, h, q) = Dungeons.Box(rooms[i]);
			if (h.X == 0 || h.Z == 0)
			{
				h = new Vector3(Math.Max(h.X, 0.5f), Math.Max(h.Y, 0.5f), Math.Max(h.Z, 0.5f));
			}
			var inv = Quaternion.Inverse(q);
			var o = Vector3.Transform(origin - c, inv);
			var d = Vector3.Transform(dir, inv);
			if (Picking.HitBox(o, d, -h, h) is float t)
			{
				// From inside a box (t = 0) the smaller box wins.
				float key = t > 0 ? t : -1f / (h.X * h.Y * h.Z);
				if (key < best)
				{
					best = key;
					hit = i;
				}
			}
		}
		return hit;
	}

	// Where the chosen room would go at the hovered opening; null when nothing would be added.
	public Dungeons.Placed? Preview
	{
		get
		{
			if (Chosen == null || HoverEnd is not int e || Dungeons.RoomOf(Chosen) is not { } room)
			{
				return null;
			}
			var ends = FreeEnds;
			if (e >= ends.Count)
			{
				return null;
			}
			var end = ends[e];
			var matching = Dungeons.Matching(room, end.Type).ToList();
			return matching.Count == 0 ? null : Dungeons.Attach(room, matching[Join % matching.Count], end.Position, end.Rotation);
		}
	}

	// Why the preview would not fit: overlapping rooms, outside the dungeon's box (the game's own checks
	// when it generates; a list made here is built anyway, so these are warnings).
	public string? Problem(Dungeons.Placed p, int skip = -1)
	{
		if (Dungeon is not { } d)
		{
			return null;
		}
		var over = Dungeons.Overlaps(d.Rooms, p, skip);
		if (over.Count > 0)
		{
			return $"Overlaps {string.Join(", ", over.Select(i => d.Rooms[i].Name).Distinct())}.";
		}
		return Dungeons.Inside(d.Kind, d.Thing.Position, p) ? null
			: $"Goes past the dungeon's space ({d.Kind.ZoneSize.X:0} × {d.Kind.ZoneSize.Z:0} m around its zone's middle): the game keeps its own rooms inside it.";
	}

	// R: the next of the chosen room's openings that can join the hovered one.
	public void Turn()
	{
		Join++;
		Changed?.Invoke();
	}

	// ---- Changes: each one a step of the history.
	public bool Click()
	{
		if (Preview is { } p && Dungeon is { } d)
		{
			string? problem = Problem(p);
			var rooms = d.Rooms.ToList();
			rooms.Add(p);
			var made = ContentsOf(d, new[] { p });
			string with = made.Count > 0 ? $" with {made.Count} object(s)" : "";
			Commit(d, rooms, $"Dungeon: added {p.Name}", problem == null ? $"Added {p.Name}{with}." : $"Added {p.Name}{with}. {problem}", add: made);
			Selected = rooms.Count - 1;
			return true;
		}
		Selected = HoverRoom;
		Changed?.Invoke();
		if (HoverRoom is int r && Dungeon is { } dd)
		{
			Message?.Invoke($"{dd.Rooms[r].Name}: Delete removes it.");
		}
		return HoverRoom != null;
	}

	public bool Delete()
	{
		if (Selected is not int i || Dungeon is not { } d || i >= d.Rooms.Count)
		{
			return false;
		}
		var rooms = d.Rooms.ToList();
		var gone = rooms[i];
		string name = gone.Name;
		rooms.RemoveAt(i);
		Selected = null;
		// What the game made in the room (chests, spawners, ice, torches: objects of their own) goes with
		// it, unless another room holds it too.
		var inside = Inside(gone, rooms);
		Commit(d, rooms, $"Dungeon: removed {name}", $"Removed {name}" + (inside.Count > 0 ? $" and the {inside.Count} object(s) in it" : "") +
			". Its neighbours' openings are open now: Close open ends caps them.", inside);
		return true;
	}

	// The scene's objects (not the dungeon itself) the room holds: within its box, and nearer its middle
	// than any other room's whose box holds them too (rooms meet at their walls; an end cap, flat, gets
	// 1.5 m of depth each side).
	public List<int> Inside(Dungeons.Placed room, IReadOnlyList<Dungeons.Placed> others)
	{
		var found = new List<int>();
		if (Scene == null)
		{
			return found;
		}
		static float? In(Dungeons.Placed r, Vector3 p)
		{
			var (c, h, q) = Dungeons.Box(r);
			h = Vector3.Max(h, new Vector3(1.5f));
			var l = Vector3.Transform(p - c, Quaternion.Inverse(q));
			return MathF.Abs(l.X) <= h.X + 0.05f && MathF.Abs(l.Y) <= h.Y + 0.05f && MathF.Abs(l.Z) <= h.Z + 0.05f ? Vector3.Distance(p, c) : null;
		}
		lock (Scene.Things)
		{
			for (int i = 0; i < Scene.Things.Count; i++)
			{
				var t = Scene.Things[i];
				if (t.Gone || Dungeons.KindOf(t.Prefab) != null || In(room, t.Position) is not float mine)
				{
					continue;
				}
				if (!others.Any(o => In(o, t.Position) is float theirs && theirs < mine))
				{
					found.Add(i);
				}
			}
		}
		return found;
	}

	// An end cap on every free opening: of the open dungeon's rooms, one whose only opening has that
	// type (the game's end caps, highest EndCapPrio first, as DungeonGenerator.PlaceEndCaps prefers).
	public int CloseOpenEnds()
	{
		if (Dungeon is not { } d)
		{
			return 0;
		}
		var caps = Dungeons.RoomsFor(d.Kind).Where(r => r.EndCap && r.Openings.Length == 1).OrderByDescending(r => r.EndCapPrio).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
		var rooms = d.Rooms.ToList();
		int added = 0, left = 0, before = rooms.Count;
		foreach (var end in Dungeons.Openings(d.Rooms, freeOnly: true))
		{
			var cap = caps.FirstOrDefault(c => c.Openings[0].Type == end.Type && !c.Openings[0].Entrance);
			if (cap == null)
			{
				left++;
				continue;
			}
			rooms.Add(Dungeons.Attach(cap, cap.Openings[0], end.Position, end.Rotation));
			added++;
		}
		if (added > 0)
		{
			Commit(d, rooms, $"Dungeon: closed {added} open end(s)", $"Closed {added} open end(s) with end caps." + (left > 0 ? $" {left} have no end cap of their type." : ""),
				add: ContentsOf(d, rooms.Skip(before)));
		}
		else
		{
			Message?.Invoke(left > 0 ? $"{left} open end(s) have no end cap of their type." : "No open end to close.");
		}
		return added;
	}

	// The objects the game makes with these rooms of the dungeon (none when WithContents is off).
	private List<(NewObject, bool)> ContentsOf(DungeonRooms.Dungeon d, IEnumerable<Dungeons.Placed> rooms)
	{
		var list = new List<(NewObject, bool)>();
		if (!WithContents || Scene == null)
		{
			return list;
		}
		foreach (var r in rooms)
		{
			foreach (var m in Dungeons.Contents(r, d.Kind, d.Thing.Position, Scene.World.Seed))
			{
				int prefab = StableHash.Of(m.Prefab);
				list.Add((new NewObject(0, prefab, m.Position, Dungeons.ToEuler(m.Rotation), 0), TerrainEditor.Terrain.PieceCatalog.Get(prefab)?.Tool != null));
			}
		}
		return list;
	}

	private void Commit(DungeonRooms.Dungeon d, List<Dungeons.Placed> rooms, string label, string message, IReadOnlyCollection<int>? alsoRemove = null,
		IReadOnlyList<(NewObject, bool)>? add = null)
	{
		if (Scene?.Session is not { } s)
		{
			return;
		}
		byte[]? bytes = ObjectData.Bytes(Scene.World, s.Edits, d.Thing.Id);
		if (bytes == null)
		{
			Message?.Invoke("The dungeon's data could not be read.");
			return;
		}
		var z = ZdoData.Parse(Dungeons.WithRooms(bytes, rooms));
		var adds = new List<(NewObject, bool)> { (new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), false) };
		adds.AddRange(add ?? Array.Empty<(NewObject, bool)>());
		s.Commit(label, null, new[] { d.Index }.Concat(alsoRemove ?? Array.Empty<int>()).ToArray(), adds);
		HoverEnd = null;
		Message?.Invoke(message + " Ctrl+Z undoes; Save writes it.");
		Changed?.Invoke();
	}
}
