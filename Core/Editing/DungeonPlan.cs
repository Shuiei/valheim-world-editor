using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

// The layout of a generated dungeon: levels of rooms and corridors on a 2 m grid (tiles), and the
// doorways between them. Rooms are rectangles, entered in the middle of a side (Dir: the way in) and
// joined to the room they come from wall to wall or by a straight corridor, so the plan reads like
// a building: axes, wings, twins on both sides. Then loops: rooms far apart on the way that happen to
// lie close are joined, so there is more than one way round (Jaquays). Floor and roof slabs cover
// whole 8 m cells, so what needs a cell to itself (a stairwell, a hall two floors high) is aligned to
// cells and keeps them. Level k's floor is 6k m below the first.
internal sealed class DungeonPlan
{
	public sealed class Space
	{
		public int Id;
		public int Level;
		public Room Room;
		public string Name = "";
		public bool Corridor;
		public bool Tall;
		public bool Main;
		public bool KeyChest;
		public readonly HashSet<(int I, int J)> Tiles = new();
		public int X0, Z0, W, H; // box (tiles, world axes)
		public (int X, int Z) Dir; // the way in
		public readonly List<Doorway> Doors = new();
		public Space? Parent;
		public int Shift, Length; // how it was placed off its parent: shifted across, corridor tiles

		public bool IsRoom => !Corridor;

		public float CenterX => (X0 + W / 2f) * 2;

		public float CenterZ => (Z0 + H / 2f) * 2;

		// Tiles across (perpendicular to Dir) and deep (along Dir).
		public int Across => Dir.X != 0 ? H : W;

		public int Deep => Dir.X != 0 ? W : H;
	}

	// A 4 m doorway: A's tiles T1 and T2 along its side, B beyond (Dir from A to B).
	public sealed class Doorway
	{
		public Space A = null!, B = null!;
		public (int I, int J) T1, T2;
		public (int X, int Z) Dir;
		public Door Kind;
		public bool Loop;

		public Space Other(Space s) => s == A ? B : A;

		// The middle of the doorway (metres), on the wall line.
		public (float X, float Z) Center => (T1.I + T2.I + 1 + Dir.X, T1.J + T2.J + 1 + Dir.Z);
	}

	public sealed class Level
	{
		public int Index;
		public readonly Dictionary<(int, int), Space> Owner = new();
		public readonly HashSet<(int, int)> Holes = new(); // cells with no floor: a stairwell
		public readonly HashSet<(int, int)> Open = new(); // cells with no roof: under a stairwell
		public readonly List<Space> Spaces = new();
	}

	public readonly List<Level> Levels = new();
	public readonly List<Space> Spaces = new();
	public readonly List<Doorway> Doors = new();
	public readonly List<string> Notes = new();
	public Space Entrance = null!;
	public Space? Boss, Treasury;

	public static (int, int) CellOf((int I, int J) t) => (FloorDiv(t.I, 4), FloorDiv(t.J, 4));

	public static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

	public static float FloorY(int level) => -6f * level;

	// How high a space's walls go above its floor.
	public static float HeightOf(Space? s) => s == null ? 0 : s.Tall ? 10 : 4;

	public Space? At(int level, (int, int) tile) => Levels[level].Owner.GetValueOrDefault(tile);
}

internal sealed class DungeonPlanner
{
	private static readonly (int X, int Z)[] Dirs = { (0, 1), (1, 0), (0, -1), (-1, 0) };

	private readonly DungeonPlan _plan = new();
	private readonly DungeonGen.Settings _s;
	private readonly Style _style;
	private readonly Random _rnd;
	private readonly int _extent; // tiles from the origin a level may reach
	private readonly HashSet<Room> _used = new();
	private int _next;

	private DungeonPlanner(DungeonGen.Settings s, Style style, Random rnd)
	{
		_s = s;
		_style = style;
		_rnd = rnd;
		_extent = 14 + 6 * Math.Clamp(s.Size, 1, 4);
	}

	public static DungeonPlan Make(DungeonGen.Settings s, Style style, Random rnd)
	{
		var p = new DungeonPlanner(s, style, rnd);
		p.Build();
		return p._plan;
	}

	private static (int X, int Z) Right((int X, int Z) d) => (d.Z, -d.X);

	private static (int, int) Add((int I, int J) t, (int X, int Z) d, int n = 1) => (t.I + d.X * n, t.J + d.Z * n);

	private int Even(int min, int max) => min + 2 * _rnd.Next((max - min) / 2 + 1);

	private T Pick<T>(IReadOnlyList<(T Item, float Weight)> items)
	{
		float total = items.Sum(i => i.Weight), roll = (float)_rnd.NextDouble() * total;
		foreach (var (item, w) in items)
		{
			if ((roll -= w) <= 0)
			{
				return item;
			}
		}
		return items[^1].Item;
	}

	private void Build()
	{
		int levels = Math.Clamp(_s.Levels, 1, 4);
		int perLevel = new[] { 5, 8, 12, 17 }[Math.Clamp(_s.Size, 1, 4) - 1];
		for (int k = 0; k < levels; k++)
		{
			_plan.Levels.Add(new DungeonPlan.Level { Index = k });
		}
		DungeonPlan.Space? start = null;
		for (int k = 0; k < levels; k++)
		{
			bool last = k == levels - 1;
			var L = _plan.Levels[k];
			if (k == 0)
			{
				var spec = SpecOf(Room.Entrance);
				int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
				start = NewSpace(L, Room.Entrance, (0, 1), Rect(-w / 2, 0, w, d));
				start.Main = true;
				_plan.Entrance = start;
			}
			// The way through: the style's main rooms in turn, then down, or to the boss.
			int mainCount = Math.Max(2, (int)MathF.Round(perLevel * 0.4f));
			var cur = start!;
			for (int i = 0; i < mainCount; i++)
			{
				var room = _style.Main[(i + k) % _style.Main.Length];
				if (SpecOf(room).Unique && _used.Contains(room))
				{
					room = Room.Hub;
				}
				var n = Attach(L, cur, room, main: true) ?? Attach(L, AnyParent(L, main: true), room, main: true);
				if (n != null)
				{
					n.Main = true;
					cur = n;
				}
			}
			if (!last)
			{
				var top = AttachStairs(L, cur) ?? L.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).Select(s => AttachStairs(L, s)).FirstOrDefault(s => s != null);
				if (top == null)
				{
					_plan.Notes.Add($"no room for stairs down from level {k + 1}; the levels below are left out");
					levels = k + 1;
					_plan.Levels.RemoveRange(levels, _plan.Levels.Count - levels);
					last = true;
				}
				else
				{
					start = _plan.Levels[k + 1].Spaces[0];
				}
			}
			if (last)
			{
				var ante = Attach(L, cur, Room.Antechamber, main: true) ?? Attach(L, AnyParent(L, main: true), Room.Antechamber, main: true);
				var boss = ante == null ? null : Attach(L, ante, Room.Boss, main: true, forward: true) ?? Attach(L, ante, Room.Boss, main: true);
				if (boss == null)
				{
					// Not enough room ahead: anywhere it fits.
					foreach (var p in L.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).ToList())
					{
						ante = Attach(L, p, Room.Antechamber, main: true);
						boss = ante == null ? null : Attach(L, ante, Room.Boss, main: true);
						if (boss != null)
						{
							break;
						}
					}
				}
				if (boss != null)
				{
					_plan.Boss = boss;
					_plan.Treasury = Attach(L, boss, Room.Treasury, main: true, forward: true) ?? Attach(L, boss, Room.Treasury, main: true);
				}
				else
				{
					_plan.Notes.Add("no room for the boss arena");
				}
			}
			// Rooms off the way: wings, some with a twin across, some leading further.
			int sides = perLevel - mainCount;
			for (int i = 0, tries = 0; i < sides && tries < sides * 6; tries++)
			{
				var room = Pick(_style.Side);
				if (SpecOf(room).Unique && _used.Contains(room))
				{
					continue;
				}
				var parent = SideParent(L);
				if (parent == null)
				{
					break;
				}
				var n = Attach(L, parent, room, main: false);
				if (n == null)
				{
					continue;
				}
				i++;
				if (_rnd.NextDouble() < _style.Symmetry && Twin(L, n) != null)
				{
					i++;
				}
			}
			AddLoops(L);
		}
		// The boss's key, in a chest far from the boss's door; a hidden cache or two.
		if (_s.BossKey && _plan.Boss != null)
		{
			var dist = Distances(_plan.Boss);
			var keyRoom = _plan.Spaces.Where(s => s.IsRoom && !s.Main && s.Room is not (Room.Secret or Room.StairTop or Room.StairBottom or Room.Treasury) && dist.ContainsKey(s))
				.OrderByDescending(s => dist[s] + _rnd.NextDouble()).FirstOrDefault()
				?? _plan.Spaces.Where(s => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Antechamber or Room.StairTop or Room.StairBottom)).OrderByDescending(s => dist.GetValueOrDefault(s)).FirstOrDefault();
			if (keyRoom != null)
			{
				keyRoom.KeyChest = true;
			}
		}
		if (_s.Secrets)
		{
			int want = 1 + (_plan.Spaces.Count(s => s.IsRoom) > 20 ? 1 : 0);
			for (int t = 0, made = 0; made < want && t < 30; t++)
			{
				var L = _plan.Levels[_rnd.Next(_plan.Levels.Count)];
				var parent = L.Spaces.Where(s => s.IsRoom && CanParent(s) && !s.Main).OrderBy(_ => _rnd.Next()).FirstOrDefault();
				if (parent != null && Attach(L, parent, Room.Secret, main: false, door: Door.Secret) != null)
				{
					made++;
				}
			}
		}
		AssignDoors();
	}

	private static float Dist2(DungeonPlan.Space a, DungeonPlan.Space b) =>
		(a.CenterX - b.CenterX) * (a.CenterX - b.CenterX) + (a.CenterZ - b.CenterZ) * (a.CenterZ - b.CenterZ);

	private static (int X0, int Z0, int W, int H) Rect(int x0, int z0, int w, int h) => (x0, z0, w, h);

	private DungeonPlan.Space NewSpace(DungeonPlan.Level L, Room room, (int, int) dir, (int X0, int Z0, int W, int H) box, IEnumerable<(int, int)>? tiles = null)
	{
		var s = new DungeonPlan.Space
		{
			Id = _next++, Level = L.Index, Room = room, Name = SpecOf(room).Name, Dir = dir, X0 = box.X0, Z0 = box.Z0, W = box.W, H = box.H,
		};
		foreach (var t in tiles ?? Box(box))
		{
			s.Tiles.Add(t);
			L.Owner[t] = s;
		}
		L.Spaces.Add(s);
		_plan.Spaces.Add(s);
		_used.Add(room);
		return s;
	}

	private static IEnumerable<(int, int)> Box((int X0, int Z0, int W, int H) b)
	{
		for (int i = b.X0; i < b.X0 + b.W; i++)
		{
			for (int j = b.Z0; j < b.Z0 + b.H; j++)
			{
				yield return (i, j);
			}
		}
	}

	private DungeonPlan.Space NewCorridor(DungeonPlan.Level L, List<(int, int)> tiles, (int, int) dir)
	{
		int x0 = tiles.Min(t => t.Item1), z0 = tiles.Min(t => t.Item2);
		var c = NewSpace(L, Room.Hub, dir, (x0, z0, tiles.Max(t => t.Item1) - x0 + 1, tiles.Max(t => t.Item2) - z0 + 1), tiles);
		c.Corridor = true;
		c.Name = "Corridor";
		return c;
	}

	private DungeonPlan.Doorway Connect(DungeonPlan.Space a, DungeonPlan.Space b, (int, int) t1, (int, int) t2, (int X, int Z) dir, Door? kind = null, bool loop = false)
	{
		var d = new DungeonPlan.Doorway { A = a, B = b, T1 = t1, T2 = t2, Dir = dir, Kind = kind ?? Door.Open, Loop = loop };
		a.Doors.Add(d);
		b.Doors.Add(d);
		_plan.Doors.Add(d);
		return d;
	}

	// Rooms that can lead on to others.
	private static bool CanParent(DungeonPlan.Space s) => s.IsRoom && s.Room is not (Room.StairTop or Room.Boss or Room.Treasury or Room.Secret);

	// The arena leads only to its treasury.
	private static bool CanParent(DungeonPlan.Space s, Room child) => CanParent(s) || s.Room == Room.Boss && child == Room.Treasury;

	private DungeonPlan.Space AnyParent(DungeonPlan.Level L, bool main) =>
		L.Spaces.Where(s => CanParent(s) && (!main || s.Main)).OrderBy(_ => _rnd.Next()).FirstOrDefault() ?? L.Spaces[0];

	private DungeonPlan.Space? SideParent(DungeonPlan.Level L)
	{
		var options = L.Spaces.Where(s => CanParent(s) && s.Room != Room.Antechamber)
			.Select(s => (s, s.Room switch { Room.Hub => 3f, Room.Hall or Room.Gallery or Room.Mine => 2.5f, Room.StairBottom => 0.5f, _ => s.Main ? 1.5f : 0.6f }))
			.ToList();
		return options.Count == 0 ? null : Pick(options);
	}

	// The tiles along a side of a space: those whose neighbour in dir is outside it.
	private static List<(int, int)> SideTiles(DungeonPlan.Space s, (int X, int Z) dir)
	{
		var r = Right(dir);
		return s.Tiles.Where(t => !s.Tiles.Contains(Add(t, dir))).OrderBy(t => t.Item1 * r.X + t.Item2 * r.Z).ToList();
	}

	// Whether a doorway may open at tiles t1, t2 on side dir of s.
	private static bool SlotAllowed(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
	{
		if (s.Room == Room.Entrance && dir == (-s.Dir.X, -s.Dir.Z))
		{
			return false; // the entrance's back wall: the portal stands before it
		}
		if (s.Room == Room.StairBottom)
		{
			// The foot of the stairs: forward, or the sides of its last 2 m rows.
			if (dir == s.Dir)
			{
				return true;
			}
			if (dir == (-s.Dir.X, -s.Dir.Z))
			{
				return false;
			}
			int Depth((int I, int J) t) => (t.I - s.X0) * s.Dir.X + (t.J - s.Z0) * s.Dir.Z + (s.Dir.X < 0 ? s.W - 1 : 0) + (s.Dir.Z < 0 ? s.H - 1 : 0);
			return Depth(t1) >= s.Deep - 2 && Depth(t2) >= s.Deep - 2;
		}
		if (s.Room == Room.Cells)
		{
			// A cell block opens only at the ends of its aisle.
			if (dir != s.Dir && dir != (-s.Dir.X, -s.Dir.Z))
			{
				return false;
			}
			int Across((int I, int J) t) => s.Dir.Z != 0 ? t.I - s.X0 : t.J - s.Z0;
			int mid = s.Across / 2;
			return Math.Min(Across(t1), Across(t2)) == mid - 1;
		}
		// Not in a corner, not on another doorway's tiles or right next to them.
		foreach (var d in s.Doors)
		{
			var mine = d.A == s ? new[] { d.T1, d.T2 } : new[] { Add(d.T1, d.Dir), Add(d.T2, d.Dir) };
			var side = d.A == s ? d.Dir : (-d.Dir.X, -d.Dir.Z);
			if (side == dir && mine.Any(m => Math.Abs(m.Item1 - t1.I) + Math.Abs(m.Item2 - t1.J) <= 2 || Math.Abs(m.Item1 - t2.I) + Math.Abs(m.Item2 - t2.J) <= 2))
			{
				return false;
			}
		}
		return true;
	}

	private bool Free(DungeonPlan.Level L, (int I, int J) t) => !L.Owner.ContainsKey(t) && Math.Abs(t.I) <= _extent && t.J >= -_extent && t.J <= 2 * _extent;

	// What a placement would make: the room's tiles, the corridor's, where they meet.
	private sealed record Placement((int X0, int Z0, int W, int H) Box, List<(int, int)> Corridor, (int, int) T1, (int, int) T2, (int X, int Z) Dir, int Shift, int Length, bool Tall);

	// Tries to place a room of w × d tiles (across, deep) off side dir of parent, at slot t1/t2, with a
	// corridor of len tiles and the room shifted across by shift tiles from centred.
	private Placement? TryPlace(DungeonPlan.Level L, DungeonPlan.Space parent, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int shift,
		bool aligned, bool tall)
	{
		var a = (X: t2.I - t1.I, Z: t2.J - t1.J); // across, t1 to t2
		var corridor = new List<(int, int)>();
		for (int t = 1; t <= len; t++)
		{
			corridor.Add(Add(t1, dir, t));
			corridor.Add(Add(t2, dir, t));
		}
		if (corridor.Any(c => !Free(L, c)))
		{
			return null;
		}
		var row0 = Add(t1, dir, len + 1);
		int off = w / 2 - 1 + shift;
		var first = (row0.Item1 - a.X * off, row0.Item2 - a.Z * off);
		var far = (first.Item1 + a.X * (w - 1) + dir.X * (d - 1), first.Item2 + a.Z * (w - 1) + dir.Z * (d - 1));
		var box = (Math.Min(first.Item1, far.Item1), Math.Min(first.Item2, far.Item2), Math.Abs(far.Item1 - first.Item1) + 1, Math.Abs(far.Item2 - first.Item2) + 1);
		if (off < 0 || off > w - 2)
		{
			return null;
		}
		if (aligned && (box.Item1 % 4 != 0 || box.Item2 % 4 != 0 || box.Item3 % 4 != 0 || box.Item4 % 4 != 0))
		{
			return null;
		}
		foreach (var t in Box(box))
		{
			if (!Free(L, t))
			{
				return null;
			}
		}
		if (tall && L.Index > 0)
		{
			// Two floors high: nothing on the level above in its cells.
			var above = _plan.Levels[L.Index - 1];
			var cells = Box(box).Select(DungeonPlan.CellOf).ToHashSet();
			if (above.Owner.Keys.Any(t => cells.Contains(DungeonPlan.CellOf(t))))
			{
				return null;
			}
		}
		return new Placement(box, corridor, t1, t2, dir, shift, len, tall);
	}

	private DungeonPlan.Space? Attach(DungeonPlan.Level L, DungeonPlan.Space parent, Room room, bool main, bool forward = false, Door? door = null)
	{
		if (!CanParent(parent, room))
		{
			return null;
		}
		var spec = SpecOf(room);
		bool tallWanted = spec.Tall && _rnd.NextDouble() < _style.TallChance;
		Placement? best = null;
		float bestScore = float.MinValue;
		for (int attempt = 0; attempt < 48; attempt++)
		{
			bool tall = tallWanted && attempt < 32;
			var dirs = Dirs.Where(dd => dd != (-parent.Dir.X, -parent.Dir.Z) || parent.Room == Room.Hub).ToList();
			var dir = forward ? parent.Dir : Pick(dirs.Select(dd => (dd, dd == parent.Dir ? (main ? 5f : 0.7f) : (main ? 1f : 3f))).ToList());
			var side = SideTiles(parent, dir);
			if (side.Count < 2)
			{
				continue;
			}
			// The slot: in the middle of the side most of the time.
			int mid = side.Count / 2 - 1, at = _rnd.NextDouble() < 0.7 ? mid : _rnd.Next(side.Count - 1);
			var (t1, t2) = (side[at], side[at + 1]);
			if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(parent, t1, t2, dir))
			{
				continue;
			}
			int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
			if (tall)
			{
				w = Math.Max(8, (w + 3) / 4 * 4);
				d = Math.Max(8, (d + 3) / 4 * 4);
			}
			bool wall = _rnd.NextDouble() < 0.25 && _style.CorridorMin <= 1 && parent.Room != Room.StairBottom;
			int len = wall ? 0 : _rnd.Next(_style.CorridorMin, _style.CorridorMax + 1);
			int shift = _rnd.NextDouble() < 0.75 || room == Room.Cells ? 0 : _rnd.Next(-(w / 2 - 1), w / 2);
			Placement? p = null;
			if (tall)
			{
				// Aligned to cells: try nearby shifts and lengths.
				for (int dl = 0; dl < 4 && p == null; dl++)
				{
					for (int sh = -(w / 2 - 1); sh <= w / 2 - 1 && p == null; sh++)
					{
						p = TryPlace(L, parent, t1, t2, dir, w, d, len + dl, sh, aligned: true, tall: true);
					}
				}
			}
			else
			{
				p = TryPlace(L, parent, t1, t2, dir, w, d, len, shift, aligned: false, tall: false);
			}
			if (p == null)
			{
				continue;
			}
			float cx = (p.Box.X0 + p.Box.W / 2f) * 2, cz = (p.Box.Z0 + p.Box.H / 2f) * 2;
			float score = (float)_rnd.NextDouble() * 2 - 0.004f * (cx * cx + (cz - 40) * (cz - 40)) / 10 + (p.Shift == 0 ? 1 : 0) + (dir == parent.Dir && main ? 2 : 0)
				+ (tall ? 3 : 0) - p.Length * 0.15f;
			if (score > bestScore)
			{
				best = p;
				bestScore = score;
			}
		}
		if (best == null)
		{
			return null;
		}
		var n = NewSpace(L, room, best.Dir, best.Box);
		n.Parent = parent;
		n.Tall = best.Tall;
		Join(L, parent, n, best, door);
		return n;
	}

	private void Join(DungeonPlan.Level L, DungeonPlan.Space parent, DungeonPlan.Space n, Placement p, Door? door)
	{
		n.Shift = p.Shift;
		n.Length = p.Length;
		if (p.Length == 0)
		{
			Connect(parent, n, p.T1, p.T2, p.Dir, door);
			return;
		}
		var c = NewCorridor(L, p.Corridor, p.Dir);
		c.Parent = parent;
		Connect(parent, c, p.T1, p.T2, p.Dir, door == Door.Secret ? Door.Secret : null);
		Connect(c, n, Add(p.T1, p.Dir, p.Length), Add(p.T2, p.Dir, p.Length), p.Dir, door == Door.Secret ? Door.Open : door);
	}

	// The same room on the other side of its parent, mirrored across the parent's middle.
	private DungeonPlan.Space? Twin(DungeonPlan.Level L, DungeonPlan.Space n)
	{
		var parent = n.Parent;
		if (parent == null || n.Shift != 0 || n.Tall || n.Dir == parent.Dir || n.Dir == (-parent.Dir.X, -parent.Dir.Z))
		{
			return null;
		}
		var first = parent.Doors.FirstOrDefault(d => d.A == parent && (d.B == n || d.B.Corridor && d.B.Doors.Any(x => x.B == n)));
		if (first == null)
		{
			return null;
		}
		(int, int) Mirror((int I, int J) t) => n.Dir.X != 0 ? (2 * parent.X0 + parent.W - 1 - t.I, t.J) : (t.I, 2 * parent.Z0 + parent.H - 1 - t.J);
		var dir = (-n.Dir.X, -n.Dir.Z);
		var side = SideTiles(parent, dir);
		var m1 = Mirror(first.T1);
		var m2 = Mirror(first.T2);
		if (!side.Contains(m1) || !side.Contains(m2))
		{
			return null;
		}
		var (o1, o2) = side.IndexOf(m1) < side.IndexOf(m2) ? (m1, m2) : (m2, m1);
		if (!SlotAllowed(parent, o1, o2, dir))
		{
			return null;
		}
		var p = TryPlace(L, parent, o1, o2, dir, n.Across, n.Deep, n.Length, 0, aligned: false, tall: false);
		if (p == null)
		{
			return null;
		}
		var twin = NewSpace(L, n.Room, dir, p.Box);
		twin.Parent = parent;
		Join(L, parent, twin, p, null);
		return twin;
	}

	// Stairs down: an aligned 1 × 3 cells room (the first cell to stand in, then the stairwell), and on
	// the level below the 1 × 2 cells under the stairwell.
	private DungeonPlan.Space? AttachStairs(DungeonPlan.Level L, DungeonPlan.Space parent)
	{
		if (!CanParent(parent))
		{
			return null;
		}
		var below = _plan.Levels[L.Index + 1];
		for (int attempt = 0; attempt < 40; attempt++)
		{
			var dir = attempt < 12 ? parent.Dir : Dirs[_rnd.Next(4)];
			if (dir == (-parent.Dir.X, -parent.Dir.Z) && parent.Room != Room.Hub)
			{
				continue;
			}
			var side = SideTiles(parent, dir);
			for (int at = 0; at + 1 < side.Count; at++)
			{
				var (t1, t2) = (side[at], side[at + 1]);
				if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(parent, t1, t2, dir))
				{
					continue;
				}
				for (int n = 0; n < 3 * (_style.CorridorMax + 4); n++)
				{
					int len = Math.Max(1, _style.CorridorMin) + n / 3, shift = n % 3 - 1;
					var p = TryPlace(L, parent, t1, t2, dir, 4, 12, len, shift, aligned: true, tall: false);
					if (p == null)
					{
						continue;
					}
					var deep = Box(p.Box).Where(t => Depth(p.Box, dir, t) >= 4).ToList();
					if (deep.Any(t => below.Owner.ContainsKey(t)))
					{
						continue;
					}
					var top = NewSpace(L, Room.StairTop, dir, p.Box);
					top.Parent = parent;
					top.Main = true;
					Join(L, parent, top, p, null);
					foreach (var t in deep)
					{
						L.Holes.Add(DungeonPlan.CellOf(t));
						below.Open.Add(DungeonPlan.CellOf(t));
					}
					int x0 = deep.Min(t => t.Item1), z0 = deep.Min(t => t.Item2);
					var foot = NewSpace(below, Room.StairBottom, dir, (x0, z0, deep.Max(t => t.Item1) - x0 + 1, deep.Max(t => t.Item2) - z0 + 1), deep);
					foot.Parent = top;
					foot.Main = true;
					// The stairs join them (no doorway: the stairwell).
					return top;
				}
			}
		}
		return null;
	}

	// How far tile t is from the back of a box entered along dir (tiles).
	public static int Depth((int X0, int Z0, int W, int H) box, (int X, int Z) dir, (int I, int J) t) =>
		dir.X > 0 ? t.I - box.X0 : dir.X < 0 ? box.X0 + box.W - 1 - t.I : dir.Z > 0 ? t.J - box.Z0 : box.Z0 + box.H - 1 - t.J;

	private Dictionary<DungeonPlan.Space, int> Distances(DungeonPlan.Space from)
	{
		var dist = new Dictionary<DungeonPlan.Space, int> { [from] = 0 };
		var queue = new Queue<DungeonPlan.Space>();
		queue.Enqueue(from);
		while (queue.Count > 0)
		{
			var s = queue.Dequeue();
			var next = s.Doors.Select(d => d.Other(s)).ToList();
			// The stairs join a stairwell's two rooms.
			next.AddRange(_plan.Spaces.Where(o => o.Parent == s && o.Room == Room.StairBottom || s.Room == Room.StairBottom && s.Parent == o));
			foreach (var o in next)
			{
				if (!dist.ContainsKey(o))
				{
					dist[o] = dist[s] + (o.Corridor ? 0 : 1);
					queue.Enqueue(o);
				}
			}
		}
		return dist;
	}

	private static bool Loopable(DungeonPlan.Space s) => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Secret or Room.StairTop or Room.StairBottom);

	// Joins rooms that are far apart on the way but close in space: through a shared wall, or by a
	// short straight corridor.
	private void AddLoops(DungeonPlan.Level L)
	{
		int want = (int)MathF.Round(_s.Loops * L.Spaces.Count(s => s.IsRoom) / 5f);
		for (int made = 0, tries = 0; made < want && tries < want * 3 + 3; tries++)
		{
			var options = new List<(float Score, Action Make)>();
			foreach (var a in L.Spaces.Where(Loopable))
			{
				var dist = Distances(a);
				foreach (var dir in Dirs)
				{
					var side = SideTiles(a, dir);
					for (int at = 0; at + 1 < side.Count; at++)
					{
						var (t1, t2) = (side[at], side[at + 1]);
						if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(a, t1, t2, dir))
						{
							continue;
						}
						for (int len = 0; len <= 6; len++)
						{
							var n1 = Add(t1, dir, len + 1);
							var n2 = Add(t2, dir, len + 1);
							var b = L.Owner.GetValueOrDefault(n1);
							if (b != null && b == L.Owner.GetValueOrDefault(n2) && b != a && Loopable(b))
							{
								int d = dist.GetValueOrDefault(b, 99);
								var path = Enumerable.Range(1, len).SelectMany(t => new[] { Add(t1, dir, t), Add(t2, dir, t) }).ToList();
								if (d >= 3 && path.All(t => Free(L, t)) && SlotAllowed(b, n1, n2, (-dir.X, -dir.Z)))
								{
									var (aa, bb, tt1, tt2, dd, ll, pp) = (a, b, t1, t2, dir, len, path);
									void Join()
									{
										if (ll == 0)
										{
											Connect(aa, bb, tt1, tt2, dd, loop: true);
										}
										else
										{
											var c = NewCorridor(L, pp, dd);
											Connect(aa, c, tt1, tt2, dd, loop: true);
											Connect(c, bb, Add(tt1, dd, ll), Add(tt2, dd, ll), dd, loop: true);
										}
									}
									options.Add((d - len * 0.3f + (float)_rnd.NextDouble(), Join));
								}
								break;
							}
							if (b != null || !Free(L, n1) || !Free(L, n2))
							{
								break;
							}
						}
					}
				}
			}
			if (options.Count == 0)
			{
				break;
			}
			options.OrderByDescending(o => o.Score).First().Make();
			made++;
		}
	}

	private void AssignDoors()
	{
		foreach (var d in _plan.Doors)
		{
			var rooms = new[] { d.A, d.B };
			if (d.Kind == Door.Secret)
			{
				continue;
			}
			if (rooms.Any(r => r.Room == Room.Boss) && rooms.Any(r => r.Room == Room.Antechamber || r.Corridor && r.Parent?.Room == Room.Antechamber))
			{
				d.Kind = _s.BossKey ? Door.Key : Door.Grand;
				continue;
			}
			if (rooms.Any(r => r.Room == Room.Treasury))
			{
				d.Kind = Door.Gate;
				continue;
			}
			if (rooms.Any(r => r.Room == Room.Secret))
			{
				d.Kind = Door.Open;
				continue;
			}
			if (rooms.Any(r => r.Room == Room.Cells) && _rnd.NextDouble() < 0.6)
			{
				d.Kind = Door.Gate;
				continue;
			}
			bool grand = rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel) && rooms.All(r => !r.Corridor || true);
			var kind = Pick(_style.Doors);
			if (grand && _rnd.NextDouble() < 0.35)
			{
				kind = Door.Grand;
			}
			if (kind == Door.Grand && !rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel or Room.Hub or Room.Boss))
			{
				kind = Door.Wood;
			}
			d.Kind = kind;
		}
		// Doors on the way between levels, so what lives below stays below.
		foreach (var top in _plan.Spaces.Where(s => s.Room == Room.StairTop))
		{
			foreach (var d in top.Doors.Where(d => d.Kind is Door.Open or Door.Curtain))
			{
				d.Kind = Door.Gate;
			}
		}
	}
}
