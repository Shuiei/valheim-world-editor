using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

// What makes a generated dungeon's rooms what they are: their furniture and set pieces (colonnades,
// thrones on a dais, pews before an altar, sarcophagi, cells behind bars...), their lights, what hangs
// on their walls and lies on their floors, their decay, who guards them and what they hold. Each room
// is dressed in its own frame: u across (right of the way in), v deep (0 its middle, the way in behind).
internal sealed class DungeonDresser
{
	// What stands on a floor goes 5 cm up: a piece whose middle of mass is right on a floor's surface
	// gets no support from it in game.
	private const float Lift = 0.05f;

	private readonly DungeonPlan _plan;
	private readonly Material _mat;
	private readonly Biome _b;
	private readonly Style _style;
	private readonly DungeonGen.Settings _s;
	private readonly Random _rnd;
	private readonly DungeonOut _o;
	private readonly float _decay;
	private readonly Foe[] _foes;
	private readonly Foe? _boss;
	private readonly string _name;

	private DungeonDresser(DungeonPlan plan, Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
	{
		_plan = plan;
		_mat = mat;
		_b = b;
		_style = style;
		_s = s;
		_rnd = rnd;
		_o = o;
		_name = name;
		_decay = Math.Clamp(s.Decay ?? style.Decay, 0, 1);
		_foes = s.Foes is { Count: > 0 } chosen ? chosen.Select(FoeOf).ToArray() : b.Foes;
		_boss = s.Boss == null ? b.Boss : s.Boss.Length == 0 ? null : FoeOf(s.Boss);
	}

	public static void Dress(DungeonPlan plan, Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
	{
		var d = new DungeonDresser(plan, mat, b, style, s, rnd, o, name);
		foreach (var sp in plan.Spaces)
		{
			d.Furnish(new Frame(sp, mat, plan));
		}
	}

	// A foe by its spawner's or its creature's name.
	public static Foe FoeOf(string name)
	{
		foreach (var f in Biomes.SelectMany(b => b.Foes.Concat(b.Elites).Append(b.Boss)))
		{
			if (f.Spawner == name || f.Creature == name)
			{
				return f;
			}
		}
		return name.StartsWith("Spawner_", StringComparison.Ordinal) ? new Foe(name, name["Spawner_".Length..]) : new Foe(name, name);
	}

	// ---- A room's frame.
	private sealed class Frame
	{
		public readonly DungeonPlan.Space S;
		public readonly float F, Top, H, Cx, Cz, HalfA, HalfD;
		public readonly (int X, int Z) D, A;
		public readonly List<(float X, float Z, float R)> Taken = new();
		public readonly List<(float X, float Z, (int X, int Z) In, DungeonPlan.Doorway Door)> Mouths = new();

		public Frame(DungeonPlan.Space s, Material mat, DungeonPlan plan)
		{
			S = s;
			F = DungeonPlan.FloorY(s.Level);
			Top = F + (mat.Floor != null && s.IsRoom && s.Room is not (Room.StairTop or Room.StairBottom) ? mat.FloorTop : 0);
			H = DungeonPlan.HeightOf(s);
			Cx = s.CenterX;
			Cz = s.CenterZ;
			D = s.Dir;
			A = (D.Z, -D.X);
			HalfA = s.Across - mat.Thick / 2;
			HalfD = s.Deep - mat.Thick / 2;
			foreach (var d in s.Doors)
			{
				var (x, z) = d.Center;
				var inward = d.A == s ? (-d.Dir.X, -d.Dir.Z) : d.Dir;
				Mouths.Add((x, z, inward, d));
				Taken.Add((x + inward.Item1 * 1.6f, z + inward.Item2 * 1.6f, 2.2f));
			}
		}

		public float Area => 4 * HalfA * HalfD;

		public Vector3 W(float u, float v, float y) => new(Cx + A.X * u + D.X * v, y, Cz + A.Z * u + D.Z * v);

		public (float U, float V) Local(float x, float z) => ((x - Cx) * A.X + (z - Cz) * A.Z, (x - Cx) * D.X + (z - Cz) * D.Z);

		public float Yaw(float local) => DungeonOut.YawOf(D) + local;

		public bool Free(float u, float v, float r)
		{
			if (Math.Abs(u) > HalfA - r + 0.15f || Math.Abs(v) > HalfD - r + 0.15f)
			{
				return false;
			}
			var p = W(u, v, 0);
			return Taken.All(t => (t.X - p.X) * (t.X - p.X) + (t.Z - p.Z) * (t.Z - p.Z) >= (t.R + r) * (t.R + r));
		}

		public void Take(float u, float v, float r)
		{
			var p = W(u, v, 0);
			Taken.Add((p.X, p.Z, r));
		}

		// Whether a point on a wall is in front of a doorway.
		public bool NearDoor(float u, float v, float clear)
		{
			var p = W(u, v, 0);
			return Mouths.Any(m => (m.X - p.X) * (m.X - p.X) + (m.Z - p.Z) * (m.Z - p.Z) < clear * clear);
		}

		// Spots along the walls (on their inner face), every step metres, facing in (local yaw), not by
		// doorways.
		public List<(float U, float V, float Yaw)> WallSpots(float step, float clear = 3f)
		{
			var spots = new List<(float, float, float)>();
			for (float u = -HalfA + step / 2; u <= HalfA - step / 2 + 0.01f; u += step)
			{
				spots.Add((u, -HalfD, 0));
				spots.Add((u, HalfD, 180));
			}
			for (float v = -HalfD + step / 2; v <= HalfD - step / 2 + 0.01f; v += step)
			{
				spots.Add((-HalfA, v, 90));
				spots.Add((HalfA, v, -90));
			}
			return spots.Where(s => !NearDoor(s.Item1, s.Item2, clear)).ToList();
		}
	}

	// Moves a wall spot in by depth metres.
	private static (float U, float V) In((float U, float V, float Yaw) s, float depth) =>
		(s.U + depth * MathF.Sin(s.Yaw * MathF.PI / 180), s.V + depth * MathF.Cos(s.Yaw * MathF.PI / 180));

	private float R(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

	private T One<T>(IReadOnlyList<T> list) => list[_rnd.Next(list.Count)];

	private bool Chance(double p) => _rnd.NextDouble() < p;

	private int Count(float n) => n <= 0 ? 0 : (int)MathF.Floor(n) + (_rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);

	// ---- Placing.

	// Places an object at (u, v) of a room, y above its floor (its foot, from the model), turned yaw
	// (local; 0 faces the far end), keeping r metres around it for itself unless forced.
	private bool Put(Frame f, string prefab, float u, float v, float yaw, float r, float y = 0, bool force = false,
		IReadOnlyList<(string, string, string)>? data = null)
	{
		if (!force && !f.Free(u, v, r))
		{
			return false;
		}
		if (r > 0)
		{
			f.Take(u, v, r);
		}
		_o.Add(prefab, f.W(u, v, f.Top + Lift + y + DungeonProps.Foot(prefab)), f.Yaw(yaw + DungeonProps.Turn(prefab)), data);
		return true;
	}

	// Tries a few spots near (u, v).
	private bool PutNear(Frame f, string prefab, float u, float v, float yaw, float r, float spread = 1.5f, float y = 0)
	{
		for (int i = 0; i < 8; i++)
		{
			if (Put(f, prefab, u + (i == 0 ? 0 : R(-spread, spread)), v + (i == 0 ? 0 : R(-spread, spread)), yaw, r, y))
			{
				return true;
			}
		}
		return false;
	}

	// Somewhere free in the room.
	private (float U, float V)? Spot(Frame f, float r, float margin = 0.8f)
	{
		for (int i = 0; i < 30; i++)
		{
			float u = R(-f.HalfA + margin + r, f.HalfA - margin - r), v = R(-f.HalfD + margin + r, f.HalfD - margin - r);
			if (f.Free(u, v, r))
			{
				return (u, v);
			}
		}
		return null;
	}

	private void Column(Frame f, float u, float v)
	{
		var p = f.W(u, v, 0);
		float y = f.F, top = f.F + f.H;
		while (y < top - 0.3f)
		{
			var c = _mat.Column.FirstOrDefault(c => c.H <= top - y + 0.01f) ?? _mat.Column[^1];
			_o.Add(c.Prefab, new Vector3(p.X, y + c.Bottom, p.Z), 0);
			y += c.H;
		}
		f.Take(u, v, 0.8f);
	}

	// A wall of the material inside a room: across (u0..u1 at depth v) or along (v0..v1 at u).
	private void InnerWall(Frame f, float u0, float v0, float u1, float v1, float y0, float y1)
	{
		var a = f.W(u0, v0, 0);
		var b = f.W(u1, v1, 0);
		bool alongZ = MathF.Abs(a.X - b.X) < 0.01f;
		float line = alongZ ? a.X : a.Z, t0 = alongZ ? Math.Min(a.Z, b.Z) : Math.Min(a.X, b.X), t1 = alongZ ? Math.Max(a.Z, b.Z) : Math.Max(a.X, b.X);
		DungeonShell.Fill(_mat, _o, alongZ, line, t0, t1, f.F + y0, f.F + y1);
	}

	// ---- Light.
	private string Torch => _decay > 0 && _b.Torch.StartsWith("CastleKit_groundtorch", StringComparison.Ordinal) && Chance(_decay * 0.5) ? "CastleKit_groundtorch_unlit" : _b.Torch;

	private void Brazier(Frame f, float u, float v) => Put(f, _b.Brazier, u, v, 0, 0.6f, force: false);

	private void Lights(Frame f, bool important = false)
	{
		if (_s.Light <= 0 && !important && !Chance(0.3))
		{
			return;
		}
		float step = _s.Light >= 2 ? 5 : _s.Light == 1 ? 8 : 12;
		var spots = f.WallSpots(step, 2.5f);
		foreach (var s in spots)
		{
			var (u, v) = In(s, 0.7f);
			Put(f, Torch, u, v, s.Yaw, 0.4f);
		}
	}

	// ---- What hangs on walls.
	private void Hangings(Frame f, int count, string[]? choice = null)
	{
		var options = choice ?? _b.Hanging;
		var spots = f.WallSpots(3.2f, 3f).OrderBy(_ => _rnd.Next()).Take(count).ToList();
		foreach (var s in spots)
		{
			Hang(f, One(options), s);
		}
	}

	private void Hang(Frame f, string prefab, (float U, float V, float Yaw) s)
	{
		var h = DungeonProps.Hanging(prefab, f.H);
		if (h == null)
		{
			return;
		}
		var (u, v) = In(s, h.Value.Inward);
		_o.Add(prefab, f.W(u, v, f.Top + h.Value.Y), f.Yaw(s.Yaw + h.Value.Turn));
		f.Take(u, v, 0.3f);
	}

	// A runner rug down the middle, from v0 to v1.
	private void Runner(Frame f, float v0, float v1, float u = 0)
	{
		const float len = 4.36f;
		int n = (int)MathF.Floor((v1 - v0) / len);
		if (n < 2)
		{
			Put(f, One(new[] { "rug_wolf", "rug_fur", "rug_deer" }), u, (v0 + v1) / 2, 0, 0, force: true);
			return;
		}
		float start = (v0 + v1) / 2 - n * len / 2 + len / 2;
		for (int i = 0; i < n; i++)
		{
			string piece = i == 0 ? "Morkhalla_Rug_end1" : i == n - 1 ? "Morkhalla_Rug_end2" : "Morkhalla_Rug_middle";
			_o.Add(piece, f.W(u, start + i * len, f.Top + 0.06f), f.Yaw(i == n - 1 ? 180 : 0));
		}
	}

	// ---- What lies about.
	private void Clutter(Frame f, float perArea, string[]? choice = null)
	{
		var options = choice ?? _b.Clutter;
		int n = Count(f.Area * perArea * (0.5f + 0.5f * _s.Decor));
		var walls = f.WallSpots(1.3f, 2.5f).OrderBy(_ => _rnd.Next()).ToList();
		for (int i = 0; i < n; i++)
		{
			if (i < walls.Count && Chance(0.7))
			{
				var (u, v) = In(walls[i], R(0.5f, 1.1f));
				Put(f, One(options), u, v, R(0, 360), 0.45f);
			}
			else if (Spot(f, 0.5f) is var (u, v))
			{
				Put(f, One(options), u, v, R(0, 360), 0.45f);
			}
		}
	}

	private void Decay(Frame f)
	{
		if (_decay <= 0)
		{
			return;
		}
		int rubble = Count(f.Area / 90 * _decay * 2);
		var corners = new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }.OrderBy(_ => _rnd.Next()).ToList();
		for (int i = 0; i < rubble; i++)
		{
			var (cu, cv) = corners[i % 4];
			PutNear(f, One(new[] { "Morkhalla_Rubble2", "Morkhalla_Rubble1" }), cu * (f.HalfA - 2.2f), cv * (f.HalfD - 2.2f), R(0, 360), 1.8f, 0.6f);
		}
		int debris = Count(f.Area / 40 * _decay * 2);
		for (int i = 0; i < debris; i++)
		{
			if (Spot(f, 0.6f) is var (u, v))
			{
				Put(f, One(new[] { "stone_wall_2x1_ruin", "stone_wall_1x1_ruin", "piece_pot1_cracked", "piece_pot3_cracked" }), u, v, R(0, 360), 0.6f);
			}
		}
		// A fallen pillar now and then.
		if (_mat.Column[0].Prefab == "stone_pillar" && f.Area > 60 && Chance(_decay * 0.6) && Spot(f, 1.3f) is var (pu, pv))
		{
			float yaw = R(0, 360);
			var q = DungeonOut.Yaw(f.Yaw(yaw)) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
			_o.Add("stone_pillar", f.W(pu, pv, f.Top + 0.55f), q);
			f.Take(pu, pv, 1.3f);
		}
	}

	// ---- Who guards it.
	private void Spawn(Frame f, Foe foe, float u, float v, float yaw, int level = 1, bool once = false)
	{
		bool creature = once || !_s.Respawn;
		var data = creature && level > 1 ? new[] { ("ints", "level", level.ToString(System.Globalization.CultureInfo.InvariantCulture)) } : null;
		Put(f, creature ? foe.Creature : foe.Spawner, u, v, yaw, 0.6f, data: data);
	}

	private void Foes(Frame f, float perArea, Foe[]? pool = null, int level = 1)
	{
		int n = Count(f.Area * perArea / 75f * _s.Monsters);
		pool ??= _foes;
		for (int i = 0; i < n && pool.Length > 0; i++)
		{
			// Away from the way in, facing it.
			if (Spot(f, 0.7f, 1.2f) is var (u, v))
			{
				Spawn(f, One(pool), u, v, 180 + R(-40, 40), level);
			}
		}
	}

	private void Elite(Frame f, float u, float v, float yaw)
	{
		if (_s.Monsters > 0 && _b.Elites.Length > 0)
		{
			Spawn(f, One(_b.Elites), u, v, yaw, _s.Monsters >= 2 ? 2 : 1);
		}
	}

	// ---- What it holds.
	private void Chest(Frame f, float u, float v, float yaw, bool force = false)
	{
		if (force || _s.Loot > 0)
		{
			Put(f, _b.Chest, u, v, yaw, 0.8f);
		}
	}

	private void ChestAgainstWall(Frame f)
	{
		foreach (var s in f.WallSpots(2, 3f).OrderBy(_ => _rnd.Next()))
		{
			var (u, v) = In(s, 0.6f);
			if (f.Free(u, v, 0.8f))
			{
				Chest(f, u, v, s.Yaw);
				return;
			}
		}
	}

	// A chest holding the boss's key (the crypt key opens the arena's gate) and some loot. Its contents
	// are written in, and the game told it already has them, or it would fill it with its own.
	private void KeyChest(Frame f)
	{
		var items = new List<(string, int)> { ("CryptKey", 1) };
		foreach (var (item, max) in _b.Loot.OrderBy(_ => _rnd.Next()).Take(3))
		{
			items.Add((item, Math.Max(1, (int)(max * R(0.4f, 1)))));
		}
		var data = new[]
		{
			("ints", "addedDefaultItems", "1"),
			("bytes", "items", Convert.ToBase64String(DungeonProps.Inventory(items))),
		};
		foreach (var s in f.WallSpots(2, 3f).OrderBy(s => Math.Abs(s.V - f.HalfD)))
		{
			var (u, v) = In(s, 0.6f);
			if (Put(f, _b.Chest, u, v, s.Yaw, 0.8f, data: data))
			{
				Brazier(f, u + 1.6f, v);
				return;
			}
		}
		Put(f, _b.Chest, 0, 0, 0, 0.8f, force: true, data: data);
	}

	private void Pickables(Frame f, float n)
	{
		for (int i = 0; i < Count(n * (0.5f + 0.5f * _s.Loot)); i++)
		{
			if (Spot(f, 0.5f) is var (u, v))
			{
				Put(f, _b.Pickable, u, v, R(0, 360), 0.5f);
			}
		}
	}

	// ---- The rooms.
	private void Furnish(Frame f)
	{
		if (f.S.Corridor)
		{
			Corridor(f);
			return;
		}
		switch (f.S.Room)
		{
			case DungeonKit.Room.Entrance: Entrance(f); break;
			case DungeonKit.Room.Hall: Hall(f); break;
			case DungeonKit.Room.Hub: Hub(f); break;
			case DungeonKit.Room.Gallery: Gallery(f); break;
			case DungeonKit.Room.Burial: Burial(f); break;
			case DungeonKit.Room.Ossuary: Ossuary(f); break;
			case DungeonKit.Room.Chapel: Chapel(f); break;
			case DungeonKit.Room.Shrine: Shrine(f); break;
			case DungeonKit.Room.Tomb: Tomb(f); break;
			case DungeonKit.Room.Throne: Throne(f); break;
			case DungeonKit.Room.Barracks: Barracks(f); break;
			case DungeonKit.Room.Armory: Armory(f); break;
			case DungeonKit.Room.Feast: Feast(f); break;
			case DungeonKit.Room.Kitchen: Kitchen(f); break;
			case DungeonKit.Room.Store: Store(f); break;
			case DungeonKit.Room.Forge: Forge(f); break;
			case DungeonKit.Room.Library: Library(f); break;
			case DungeonKit.Room.Cells: Cells(f); break;
			case DungeonKit.Room.Torture: Torture(f); break;
			case DungeonKit.Room.Guard: Guard(f); break;
			case DungeonKit.Room.Treasury: Treasury(f); break;
			case DungeonKit.Room.Boss: Boss(f); break;
			case DungeonKit.Room.Antechamber: Antechamber(f); break;
			case DungeonKit.Room.Den: Den(f); break;
			case DungeonKit.Room.Totem: Totem(f); break;
			case DungeonKit.Room.Mine: Mine(f); break;
			case DungeonKit.Room.Workshop: Workshop(f); break;
			case DungeonKit.Room.Quarters: Quarters(f); break;
			case DungeonKit.Room.Secret: Secret(f); break;
			case DungeonKit.Room.StairTop or DungeonKit.Room.StairBottom: Stairwell(f); break;
		}
		if (f.S.KeyChest)
		{
			KeyChest(f);
			Elite(f, 0, f.HalfD * 0.3f, 180);
		}
	}

	private string Banner => One(new[] { "piece_banner01", "piece_banner02", "piece_banner03", "piece_banner04", "piece_banner05", "piece_banner06", "piece_banner07" });

	// The wall hangings that suit the style: banners in keeps and temples, the biome's own elsewhere.
	private string[] Drapes => _style.Name switch
	{
		"Fortress" or "Temple" => new[] { "piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner07", "cloth_hanging_door" },
		_ => _b.Hanging,
	};

	private void Corridor(Frame f)
	{
		// Torches every few metres, on alternate sides; a few things lying about; now and then a foe.
		float step = _s.Light >= 2 ? 6 : _s.Light == 1 ? 9 : 16;
		bool left = Chance(0.5);
		for (float v = -f.HalfD + step / 2; v < f.HalfD; v += step)
		{
			float u = (left ? -1 : 1) * (f.HalfA - 0.5f);
			left = !left;
			if (!f.NearDoor(u, v, 2.5f))
			{
				Put(f, Torch, u, v, left ? -90 : 90, 0.35f);
			}
		}
		Clutter(f, 0.02f);
		if (_decay > 0.3f && Chance(_decay * 0.3) && Spot(f, 0.8f) is var (ru, rv))
		{
			Put(f, "Morkhalla_Rubble2", ru, rv, R(0, 360), 0.9f);
		}
		Foes(f, 0.25f);
	}

	private void Stairwell(Frame f)
	{
		// The stairs keep the side at u -3.5..-1.5; torches on the other.
		f.Take(-2.5f, 0, 2f);
		f.Take(-2.5f, f.HalfD - 4, 2f);
		f.Take(-2.5f, -f.HalfD + 4, 2f);
		if (f.S.Room == DungeonKit.Room.StairTop)
		{
			// Only its first cell has a floor.
			Put(f, Torch, f.HalfA - 0.6f, -f.HalfD + 1, -90, 0.4f);
			Put(f, Torch, -f.HalfA + 0.6f, -f.HalfD + 1, 90, 0.4f);
		}
		else
		{
			Put(f, Torch, f.HalfA - 0.6f, f.HalfD - 1, -90, 0.4f);
			Put(f, Torch, f.HalfA - 0.6f, -f.HalfD + 6, -90, 0.4f);
			Clutter(f, 0.02f);
		}
	}

	private void Entrance(Frame f)
	{
		// The portal stands before the back wall; the dungeon's name on a sign beside it.
		f.Take(0, -f.HalfD + 1.2f, 1.9f);
		_o.Add("sign", f.W(2.6f, -f.HalfD + 0.04f, f.Top + 2.1f), f.Yaw(DungeonProps.Turn("sign")),
			new[] { ("strings", "text", _name) });
		Runner(f, -f.HalfD + 2.5f, f.HalfD - 1);
		Brazier(f, -2.8f, f.HalfD - 1.3f);
		Brazier(f, 2.8f, f.HalfD - 1.3f);
		foreach (int side in new[] { -1, 1 })
		{
			Put(f, _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : "piece_bench01", side * (f.HalfA - 0.6f), 0, side < 0 ? 90 : -90, 1.3f);
		}
		Hangings(f, 2, Drapes);
		Lights(f, important: true);
		Clutter(f, 0.01f);
	}

	private void Hall(Frame f)
	{
		bool cols = f.HalfA >= 5;
		if (cols)
		{
			for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
			{
				Column(f, -(f.HalfA - 2.2f), v);
				Column(f, f.HalfA - 2.2f, v);
			}
		}
		Feature(f, f.HalfD - 2.2f);
		Runner(f, -f.HalfD + 1.5f, f.HalfD - 4.2f);
		// Between the columns, braziers and banners.
		for (float v = -f.HalfD + 5; v <= f.HalfD - 5; v += 8)
		{
			Brazier(f, -(f.HalfA - 1.2f), v);
			Brazier(f, f.HalfA - 1.2f, v);
		}
		Hangings(f, (int)(f.HalfD / 3), Drapes);
		if (f.S.Tall)
		{
			foreach (int side in new[] { -1, 1 })
			{
				_o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
			}
		}
		Lights(f);
		Foes(f, 1.1f);
		Clutter(f, 0.012f);
		Decay(f);
	}

	// The set piece at the far end of a hall: an altar, a throne, a statue, a table.
	private void Feature(Frame f, float v)
	{
		switch (_style.Name)
		{
			case "Fortress" or "Goblin warren":
				Dais(f, v);
				break;
			case "Dvergr hold":
				Put(f, "dvergrprops_table", 0, v - 0.6f, 0, 1.2f);
				Put(f, "dvergrprops_chair", 0, v + 0.3f, 180, 0.4f);
				Put(f, "dvergrprops_lantern_standing", 0.5f, v - 0.6f, 0, 0, y: 0.78f, force: true);
				break;
			default:
				Put(f, f.S.Tall ? "StatueEvil" : One(new[] { "Ashlands_Altar", "offeraltar_FrozenKing_bossroom" }), 0, v, 180, 1.6f);
				Brazier(f, -2.2f, v - 0.6f);
				Brazier(f, 2.2f, v - 0.6f);
				break;
		}
	}

	private string ThronePiece => _mat.Name switch
	{
		"Marble" or "Grausten" => "piece_blackmarble_throne",
		"Goblin" => "piece_bone_throne",
		"Stave" => "piece_moose_throne",
		_ => _style.Name == "Crypt" ? "piece_bone_throne" : One(new[] { "piece_throne01", "piece_throne02" }),
	};

	// A dais of stone 1 m high across the far end, steps in front, the throne on it. It stands on the
	// marble, through any wooden floor (stone on wood would not hold).
	private void Dais(Frame f, float v)
	{
		float back = Math.Min(f.HalfD - 0.6f, v + 1.2f);
		for (float u = -2; u <= 2.01f; u += 2)
		{
			for (float dv = 0; dv < 4; dv += 2)
			{
				_o.Add("stone_floor_2x2", f.W(u, back - 1 - dv, f.F + 0.5f + Lift), f.Yaw(0));
			}
		}
		for (float u = -2; u <= 2.01f; u += 2)
		{
			_o.Add("stone_stair", f.W(u, back - 5, f.F + Lift), f.Yaw(180));
		}
		f.Take(0, back - 2.5f, 3.6f);
		_o.Add(ThronePiece, f.W(0, back - 1.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(ThronePiece)));
		Brazier(f, -3.6f, back - 1.5f);
		Brazier(f, 3.6f, back - 1.5f);
	}

	private void Hub(Frame f)
	{
		switch (_rnd.Next(4))
		{
			case 0:
				Put(f, One(_decay > 0.4f ? new[] { "StatueThor_broken_top", "StatueFreya_broken_left" } : new[] { "StatueThor", "StatueFreya", "StatueDeer", "StatueSeed" }), 0, 0, 180, 1.4f);
				break;
			case 1:
				for (int i = 0; i < 4; i++)
				{
					float a = i * MathF.PI / 2 + MathF.PI / 4;
					Brazier(f, 1.6f * MathF.Cos(a), 1.6f * MathF.Sin(a));
				}
				Put(f, "Morkhalla_giant_railing_deco", 0, 0, 0, 0.8f, y: 0.5f);
				break;
			case 2:
				Column(f, 0, 0);
				break;
			default:
				Put(f, "blackmarble_altar_crystal", 0, 0, 0, 1f, y: 0.9f);
				break;
		}
		if (f.HalfA >= 5 && f.HalfD >= 5)
		{
			foreach (var (u, v) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
			{
				if (!f.NearDoor(u, v, 3))
				{
					Column(f, u, v);
				}
			}
		}
		Hangings(f, 2, Drapes);
		Lights(f);
		Foes(f, 0.6f);
		Clutter(f, 0.015f);
		Decay(f);
	}

	private void Gallery(Frame f)
	{
		// The dead in niches along both walls, pilasters between.
		var remains = _b.Clutter.Where(c => c.Contains("Remains", StringComparison.Ordinal) || c == "Skull1" || c == "LargeBone").ToArray();
		if (remains.Length == 0)
		{
			remains = new[] { "Skull1", "LargeBone" };
		}
		for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
		{
			foreach (int side in new[] { -1, 1 })
			{
				float u = side * (f.HalfA - 0.6f);
				if (f.NearDoor(u, v, 2.6f))
				{
					continue;
				}
				if (((int)((v + f.HalfD) / 2) % 3) == 0)
				{
					Column(f, side * (f.HalfA - 0.5f), v);
				}
				else if (Chance(0.7))
				{
					Put(f, One(remains), u, v, side < 0 ? 90 : -90, 0.5f);
				}
			}
		}
		Lights(f);
		Foes(f, 0.8f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Burial(Frame f)
	{
		// Rows of sarcophagi each side of an aisle.
		float uRow = Math.Max(2.2f, f.HalfA / 2 + 0.3f);
		for (float v = -f.HalfD + 2.2f; v <= f.HalfD - 1.6f; v += 2.4f)
		{
			foreach (int side in new[] { -1, 1 })
			{
				Put(f, "crypt_skeleton_chest", side * uRow, v, side < 0 ? 0 : 180, 1.2f);
			}
		}
		foreach (var (cu, cv) in new[] { (-1, 1), (1, 1) })
		{
			Put(f, "skull_pile", cu * (f.HalfA - 0.7f), cv * (f.HalfD - 0.7f), R(0, 360), 0.6f);
		}
		Lights(f);
		Hangings(f, 1);
		// The dead do not rest here.
		var sleepers = _b.Name == "Swamp" ? new[] { new Foe("Spawner_Draugr", "Draugr_sleeping"), new Foe("Spawner_Draugr_Ranged", "Draugr_Ranged_sleeping") } : null;
		Foes(f, 1.1f, sleepers);
		Clutter(f, 0.02f);
		Decay(f);
	}

	private void Ossuary(Frame f)
	{
		foreach (var s in f.WallSpots(1.25f, 2.6f))
		{
			var (u, v) = In(s, 0.55f);
			Put(f, Chance(0.5) ? "skull_pile" : "bone_stack", u, v, s.Yaw + R(-20, 20), 0.5f);
		}
		if (_s.Monsters > 0)
		{
			// A heap of bones that keeps raising the dead until broken.
			Put(f, _b.Name == "Swamp" ? "Spawner_DraugrPile" : _b.Name == "Black Forest" || _b.Name == "Meadows" ? "BonePileSpawner" : "BonePileSpawner_swamp", 0, R(-1, 1), R(0, 360), 1.3f);
		}
		Clutter(f, 0.04f, new[] { "Skull1", "LargeBone", "LargeBone_half01", "Skull1" });
		Lights(f);
		Foes(f, 0.4f);
		Decay(f);
	}

	private void Chapel(Frame f)
	{
		float far = f.HalfD - 1.6f;
		Put(f, One(new[] { "Ashlands_Altar", "offeraltar_FrozenKing_bossroom", "blackmarble_altar_crystal" }) is var altar && altar == "blackmarble_altar_crystal" ? "Ashlands_Altar" : altar, 0, far, 180, 1.4f);
		var statues = _decay > 0.4f ? new[] { "StatueFreya_broken_left", "StatueThor_broken_top" } : new[] { "StatueFreya", "StatueThor" };
		Put(f, statues[0], -2.6f, far + 0.3f, 180, 0.6f);
		Put(f, statues[1], 2.6f, far + 0.3f, 180, 0.6f);
		Brazier(f, -1.6f, far - 1.8f);
		Brazier(f, 1.6f, far - 1.8f);
		Pickables(f, 0.5f);
		// Pews either side of the aisle, facing the altar.
		string bench = _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : _mat.Name == "Stave" ? "prop_piece_bench_runed" : "piece_bench01";
		for (float v = -f.HalfD + 3; v <= far - 3.5f; v += 1.7f)
		{
			foreach (int side in new[] { -1, 1 })
			{
				for (float u = 2.6f; u <= f.HalfA - 1.2f; u += 2.6f)
				{
					Put(f, bench, side * u, v, 0, 0.7f);
				}
			}
		}
		Runner(f, -f.HalfD + 1.2f, far - 1.4f);
		if (f.S.Tall)
		{
			foreach (int side in new[] { -1, 1 })
			{
				_o.Add("cloth_hanging_long", f.W(side * 2.2f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
			}
		}
		Hangings(f, 2, Drapes);
		Lights(f, important: true);
		Foes(f, 0.7f);
		Clutter(f, 0.008f);
		Decay(f);
	}

	private void Shrine(Frame f)
	{
		Put(f, f.S.Tall ? "StatueEvil" : One(new[] { "StatueSeed", "blackmarble_altar_crystal", "StatueDeer" }), 0, 0, 180, 1.3f, y: 0);
		float r = Math.Min(f.HalfA, f.HalfD) - 1.6f;
		for (int i = 0; i < 6; i++)
		{
			float a = i * MathF.PI / 3;
			if (i % 2 == 0)
			{
				Brazier(f, r * MathF.Cos(a), r * MathF.Sin(a));
			}
			else
			{
				Put(f, "skull_pile", r * MathF.Cos(a), r * MathF.Sin(a), R(0, 360), 0.6f);
			}
		}
		Pickables(f, 0.6f);
		Hangings(f, 2);
		Foes(f, 0.9f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Tomb(Frame f)
	{
		Put(f, "crypt_skeleton_chest", 0, 0.5f, 90, 1.6f);
		Put(f, "MountainGraveStone01", 0, 2.6f, 180, 0.6f);
		foreach (var (u, v) in new[] { (-2.3f, -1.6f), (2.3f, -1.6f), (-2.3f, 2.6f), (2.3f, 2.6f) })
		{
			Column(f, u, v);
		}
		Brazier(f, -f.HalfA + 1, -f.HalfD + 1);
		Brazier(f, f.HalfA - 1, -f.HalfD + 1);
		for (int i = 0; i < Count(1 + _s.Loot); i++)
		{
			PutNear(f, "treasure_stack", R(-1, 1), -1.2f, R(0, 360), 0.3f, 0.8f);
		}
		Hangings(f, 2);
		Elite(f, 0, -0.8f, 180);
		Clutter(f, 0.015f);
		Decay(f);
	}

	private void Throne(Frame f)
	{
		Dais(f, f.HalfD - 2);
		if (f.HalfA >= 5)
		{
			for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
			{
				Column(f, -(f.HalfA - 2.4f), v);
				Column(f, f.HalfA - 2.4f, v);
			}
		}
		Runner(f, -f.HalfD + 1.2f, f.HalfD - 6.2f);
		for (float v = -f.HalfD + 4; v <= f.HalfD - 7; v += 4)
		{
			Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), v, 90, 0.6f);
			Put(f, "ArmorStand_Male", f.HalfA - 0.8f, v, -90, 0.6f);
		}
		Hangings(f, 4, new[] { "piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06" });
		Trophies(f, 2);
		Lights(f, important: true);
		Elite(f, -1.8f, f.HalfD - 6.5f, 180);
		Elite(f, 1.8f, f.HalfD - 6.5f, 180);
		Foes(f, 0.6f);
		Decay(f);
	}

	private void Trophies(Frame f, int n)
	{
		var options = new[] { "prop_itemstand_TrophyDraugrElite", "prop_itemstand_TrophyGoblinBrute", "prop_itemstand_TrophyGoblinShaman", "prop_itemstand_TrophyGreydwarf",
			"prop_itemstand_TrophyGreydwarfBrute", "prop_itemstand_TrophySeekerBrute" };
		foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(n))
		{
			var (u, v) = In(s, 0.06f);
			_o.Add(One(options), f.W(u, v, f.Top + 2.1f), f.Yaw(s.Yaw));
		}
	}

	private string Bed => _mat.Name switch { "Goblin" => "goblin_bed", "Grausten" => "prop_ashwood_bed", _ => "dvergrprops_bed" };

	private void Barracks(Frame f)
	{
		foreach (int side in new[] { -1, 1 })
		{
			for (float v = -f.HalfD + 1.6f; v <= f.HalfD - 1; v += 1.9f)
			{
				float u = side * (f.HalfA - 1.15f);
				if (f.NearDoor(u, v, 2.4f))
				{
					continue;
				}
				if (Put(f, Bed, u, v, side < 0 ? 90 : -90, 0.75f) && Chance(0.5))
				{
					Put(f, "dvergrprops_crate", side * (f.HalfA - 2.7f), v, 0, 0.5f);
				}
			}
		}
		Put(f, "dvergrprops_table", 0, 0, 0, 1.1f);
		foreach (var (u, v) in new[] { (-1.2f, 0f), (1.2f, 0f), (0f, -1f) })
		{
			Put(f, "dvergrprops_stool", u, v, 0, 0.3f);
		}
		Put(f, "prop_Tankard", 0.3f, 0.1f, R(0, 360), 0, y: 0.78f, force: true);
		Put(f, "ArmorStand_Male", 0, f.HalfD - 0.7f, 180, 0.6f);
		Hangings(f, 1, Drapes);
		Lights(f);
		Foes(f, 1.5f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Armory(Frame f)
	{
		foreach (var s in f.WallSpots(1.6f, 2.8f))
		{
			var (u, v) = In(s, 0.6f);
			Put(f, Chance(0.6) ? "ArmorStand_Male" : "ArmorStand_Female", u, v, s.Yaw, 0.7f);
		}
		Put(f, "piece_table", 0, 0, 0, 1.3f);
		Put(f, "prop_piece_workbench_ext3", 0, 0, 0, 0, y: 0.82f, force: true);
		Trophies(f, 2);
		Chest(f, 0, -1.6f, 180);
		Lights(f);
		Foes(f, 0.6f);
		Decay(f);
	}

	private string FeastFood => _b.Name switch { "Ashlands" => "prop_FeastAshlands", "Deep North" => "PropFeastDeepNorth", _ => "prop_FeastMeadows" };

	private void Feast(Frame f)
	{
		// Long tables end to end down the middle, benches each side, the hearth at the end.
		float v0 = -f.HalfD + 3, v1 = f.HalfD - 3.6f;
		for (float v = v0 + 3.3f; v + 3.3f <= v1 + 0.01f; v += 6.8f)
		{
			_o.Add("piece_table_oak", f.W(0, v, f.Top + Lift), f.Yaw(90));
			for (float dv = -3f; dv <= 3.01f; dv += 1.5f)
			{
				f.Take(0, v + dv, 0.8f);
			}
			foreach (float dv in new[] { -2f, 0f, 2f })
			{
				_o.Add(Chance(0.7) ? FeastFood : "prop_Tankard", f.W(R(-0.3f, 0.3f), v + dv, f.Top + 0.82f + Lift), f.Yaw(R(0, 360)));
			}
			foreach (int side in new[] { -1, 1 })
			{
				for (float dv = -2.6f; dv <= 2.61f; dv += 2.6f)
				{
					Put(f, "piece_bench01", side * 1.5f, v + dv, side < 0 ? 90 : -90, 0.45f);
				}
			}
		}
		Put(f, "piece_chair03", 0, v1 + 0.6f, 180, 0.4f);
		Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.4f);
		Hangings(f, 3, Drapes);
		Lights(f);
		Foes(f, 1.2f);
		Clutter(f, 0.008f);
		Decay(f);
	}

	private void Kitchen(Frame f)
	{
		Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.6f);
		Put(f, "prop_piece_cauldron", 0, f.HalfD - 3.4f, 0, 0.6f);
		Put(f, "prop_preptable", -(f.HalfA - 0.5f), 0, 90, 1.3f);
		Put(f, "prop_cauldron_ext6_rollingpins", -(f.HalfA - 0.5f), 0.6f, 90, 0, y: 1.2f, force: true);
		Put(f, "prop_cauldron_ext3_butchertable", f.HalfA - 1.2f, 0, -90, 0.8f);
		Put(f, "prop_cauldron_ext5_mortarandpestle", f.HalfA - 0.6f, -2.4f, -90, 0.8f);
		Put(f, "prop_piece_cookingstation", -2.4f, f.HalfD - 1.4f, 180, 0.8f);
		foreach (var s in f.WallSpots(1.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(5))
		{
			var (u, v) = In(s, 0.6f);
			Put(f, One(new[] { "barrell", "CargoCrate", "prop_wood_stack" }), u, v, R(0, 360), 0.6f);
		}
		Lights(f);
		Foes(f, 0.5f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Store(Frame f)
	{
		var stock = _mat.Name == "Dvergr" ? new[] { "dvergrprops_barrel", "dvergrprops_crate", "dvergrprops_crate_long" } : new[] { "barrell", "CargoCrate", "barrell", "prop_wood_stack" };
		foreach (var s in f.WallSpots(1.2f, 2.6f))
		{
			var (u, v) = In(s, 0.6f);
			if (Chance(0.75))
			{
				Put(f, One(stock), u, v, s.Yaw + R(-15, 15), 0.55f);
			}
		}
		foreach (var s in f.WallSpots(2.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(2))
		{
			var (u, v) = In(s, 0.12f);
			Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.5f);
		}
		Pickables(f, 1);
		if (Chance(0.5))
		{
			ChestAgainstWall(f);
		}
		Lights(f);
		Foes(f, 0.3f);
		Decay(f);
	}

	private void Forge(Frame f)
	{
		Put(f, "prop_forge_ext2", 0, 0.6f, 0, 0.6f);
		Put(f, "prop_forge_ext5", 1.4f, 0.6f, 0, 0.6f);
		Brazier(f, -1.4f, 0.6f);
		Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
		Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 1.6f, 90, 0.7f);
		Put(f, "prop_piece_workbench_ext4", f.HalfA - 0.4f, 1.6f, -90, 0.7f);
		Put(f, "prop_wood_stack", -(f.HalfA - 1.4f), -(f.HalfD - 1.4f), R(0, 360), 1.2f);
		if (_s.Loot > 0)
		{
			Put(f, _b.Bars, f.HalfA - 1.2f, -(f.HalfD - 1.2f), R(0, 360), 0.5f);
		}
		Lights(f);
		Foes(f, 0.5f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Library(Frame f)
	{
		foreach (var s in f.WallSpots(2, 2.8f))
		{
			var (u, v) = In(s, 0.12f);
			Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
		}
		Put(f, One(new[] { "rug_wolf", "rug_fur", "rug_deer" }), 0, 0, 0, 0, force: true);
		Put(f, "dvergrprops_table", 0, 0, 0, 1.2f);
		Put(f, "dvergrprops_chair", 0, -1.1f, 0, 0.4f);
		Put(f, "dvergrprops_chair", 0, 1.1f, 180, 0.4f);
		Put(f, "dvergrprops_lantern_standing", 0.5f, 0, 0, 0, y: 0.78f, force: true);
		Chest(f, 0, f.HalfD - 2.2f, 180);
		Lights(f);
		Foes(f, 0.3f);
		Decay(f);
	}

	// A block of cells either side of an aisle 4 m wide, each 4 m along it behind a grate.
	private void Cells(Frame f)
	{
		float aisle = 2;
		int n = (int)MathF.Floor((2 * f.HalfD) / 4);
		float v0 = -n * 2f;
		for (int i = 0; i < n; i++)
		{
			float a = v0 + 4 * i, b = a + 4;
			foreach (int side in new[] { -1, 1 })
			{
				float front = side * aisle, back = side * f.HalfA;
				if (i > 0)
				{
					InnerWall(f, Math.Min(front, back), a, Math.Max(front, back), a, 0, 4);
				}
				InnerWall(f, front, a, front, a + 1, 0, 4);
				InnerWall(f, front, b - 1, front, b, 0, 4);
				InnerWall(f, front, a + 1, front, b - 1, 3, 4);
				_o.Add("iron_grate", f.W(front, a + 2, f.F + 1), f.Yaw(90));
				float mid = (front + back) / 2;
				Put(f, Chance(0.5) ? "goblin_strawpile" : "rug_straw", mid, (a + b) / 2, R(0, 360), 0, force: true);
				Hang(f, "Morkhalla_WallChain1", (back, (a + b) / 2, side < 0 ? 90 : -90));
				if (Chance(0.6))
				{
					_o.Add(One(new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1" }), f.W(mid + side * 0.8f, a + R(1, 3), f.Top + Lift), f.Yaw(R(0, 360)));
				}
				else if (Chance(0.3) && _s.Loot > 0)
				{
					_o.Add(_b.Pickable, f.W(mid, a + 2, f.Top + Lift), f.Yaw(R(0, 360)));
				}
				else if (Chance(0.4) && _s.Monsters > 0)
				{
					var p = f.W(mid, a + 2, f.Top + Lift);
					var foe = One(_foes);
					_o.Add(_s.Respawn ? foe.Spawner : foe.Creature, p, f.Yaw(side < 0 ? 90 : -90));
				}
			}
		}
		// The aisle's walls are the cells': only the aisle is free.
		f.Take(-(f.HalfA + aisle) / 2 - 0.5f, 0, 0);
		for (float v = v0 + 2; v < -v0; v += 8)
		{
			Put(f, Torch, aisle - 0.4f, v, -90, 0.3f);
		}
		Foes(f, 0.3f, _b.Elites.Length > 0 ? _b.Elites : null);
	}

	private void Torture(Frame f)
	{
		Put(f, "prop_cauldron_ext3_butchertable", 0, 0, R(0, 360), 1.1f);
		Brazier(f, 1.8f, 1.2f);
		Put(f, "prop_piece_workbench_ext3", -(f.HalfA - 0.6f), 0, 90, 1f);
		foreach (var s in f.WallSpots(2.4f, 3).OrderBy(_ => _rnd.Next()).Take(4))
		{
			Hang(f, "Morkhalla_WallChain1", s);
		}
		if (f.S.Tall)
		{
			_o.Add("dvergrprops_hooknchain", f.W(-1.5f, -1.5f, f.Top + f.H - 2.5f), f.Yaw(0));
		}
		Clutter(f, 0.04f, new[] { "Skull1", "LargeBone", "Pickable_ForestCryptRemains01", "skull_pile" });
		Lights(f);
		Foes(f, 0.7f);
		Decay(f);
	}

	private void Guard(Frame f)
	{
		Put(f, "dvergrprops_table", 0, 0, 90, 1f);
		Put(f, "dvergrprops_stool", -1, 0, 90, 0.3f);
		Put(f, "dvergrprops_stool", 1, 0, -90, 0.3f);
		Put(f, "prop_Tankard", 0, 0.2f, 0, 0, y: 0.78f, force: true);
		foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(2))
		{
			var (u, v) = In(s, 0.5f);
			Put(f, Chance(0.5) ? "ArmorStand_Male" : "barrell", u, v, s.Yaw, 0.6f);
		}
		Lights(f, important: true);
		Elite(f, 0, f.HalfD - 1.6f, 180);
		Foes(f, 1f);
		Decay(f);
	}

	private void Treasury(Frame f)
	{
		float loot = Math.Max(0.5f, _s.Loot);
		for (int i = 0; i < Count(1 + loot); i++)
		{
			PutNear(f, "treasure_pile", R(-1.5f, 1.5f), R(-0.5f, 1.5f), R(0, 360), 1.2f, 1.5f);
		}
		for (int i = 0; i < Count(3 * loot); i++)
		{
			PutNear(f, "treasure_stack", R(-2, 2), R(-1, 2), R(0, 360), 0.3f, 2f);
		}
		for (int i = 0; i < Count(2 * loot); i++)
		{
			PutNear(f, _b.Bars, R(-2.5f, 2.5f), f.HalfD - 1.2f, R(-10, 10), 0.5f, 1f);
		}
		foreach (var s in f.WallSpots(2.5f, 3).Where(s => s.Yaw != 0).OrderBy(_ => _rnd.Next()).Take(Count(1.5f + loot)))
		{
			var (u, v) = In(s, 0.6f);
			Chest(f, u, v, s.Yaw, force: true);
		}
		foreach (var (u, v) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
		{
			Brazier(f, u * (f.HalfA - 1), v * (f.HalfD - 1));
		}
		Pickables(f, 2);
	}

	private void Boss(Frame f)
	{
		// A ring of columns, braziers between, the master in the middle.
		float r = Math.Min(f.HalfA, f.HalfD) - 3.5f;
		for (int i = 0; i < 8; i++)
		{
			float a = i * MathF.PI / 4 + MathF.PI / 8;
			float u = r * MathF.Cos(a), v = r * MathF.Sin(a);
			if (!f.NearDoor(u, v, 3.5f))
			{
				Column(f, u, v);
			}
		}
		for (int i = 0; i < 4; i++)
		{
			float a = i * MathF.PI / 2 + MathF.PI / 4;
			Brazier(f, (r - 2.2f) * MathF.Cos(a), (r - 2.2f) * MathF.Sin(a));
		}
		if (f.S.Tall)
		{
			Put(f, "StatueEvil", 0, f.HalfD - 1.6f, 180, 1.6f);
			foreach (int side in new[] { -1, 1 })
			{
				_o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3.4f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
			}
			_o.Add("Morkhalla_StatueSword_hanging", f.W(0, f.HalfD - 5, f.Top + f.H - 2.7f), f.Yaw(0));
		}
		else
		{
			Put(f, One(new[] { "Ashlands_Altar", "offeraltar_FrozenKing_bossroom" }), 0, f.HalfD - 1.6f, 180, 1.5f);
		}
		// A great rug in the middle, the boss upon it; a giant's sword before the altar; banners.
		foreach (var (cu, cv, yaw) in new[] { (-2.18f, -2.18f, 0f), (2.18f, -2.18f, 90f), (2.18f, 2.18f, 180f), (-2.18f, 2.18f, 270f) })
		{
			_o.Add("Morkhalla_Rug_corner", f.W(cu, cv, f.Top + 0.06f), f.Yaw(yaw));
		}
		if (f.HalfA >= 10)
		{
			_o.Add("Morkhalla_StatueSword", f.W(0, f.HalfD - 4.4f, f.Top + 0.3f), f.Yaw(R(-8, 8)));
			f.Take(-3, f.HalfD - 4.4f, 1.2f);
			f.Take(3, f.HalfD - 4.4f, 1.2f);
		}
		Hangings(f, 4, Drapes);
		for (int i = 0; i < 4; i++)
		{
			float a = i * MathF.PI / 2;
			PutNear(f, "skull_pile", (r + 1.4f) * MathF.Cos(a), (r + 1.4f) * MathF.Sin(a), R(0, 360), 0.6f, 1f);
		}
		f.Take(0, 0, 1.5f);
		if (_boss != null)
		{
			Spawn(f, _boss, 0, 0.5f, 180, Math.Clamp(1 + (int)MathF.Round(_s.Monsters), 1, 3), once: true);
		}
		if (_s.Monsters > 0)
		{
			for (int i = 0; i < Count(2 * _s.Monsters); i++)
			{
				if (Spot(f, 0.7f, 2f) is var (u, v))
				{
					Spawn(f, One(_foes), u, v, 180, 1, once: true);
				}
			}
		}
		Clutter(f, 0.012f, new[] { "Skull1", "LargeBone", "lox_ribs", "skull_pile" });
		Lights(f, important: true);
	}

	private void Antechamber(Frame f)
	{
		// The way to the boss: braziers either side of its gate, guards, a warning.
		foreach (var m in f.Mouths.Where(m => m.Door.Kind is Door.Key or Door.Grand))
		{
			var (u, v) = f.Local(m.X, m.Z);
			var side = (U: m.In.X * f.A.X + m.In.Z * f.A.Z, V: m.In.X * f.D.X + m.In.Z * f.D.Z);
			var along = (U: side.V, V: -side.U);
			Brazier(f, u + side.U * 1.2f + along.U * 3.2f, v + side.V * 1.2f + along.V * 3.2f);
			Brazier(f, u + side.U * 1.2f - along.U * 3.2f, v + side.V * 1.2f - along.V * 3.2f);
			if (m.Door.Kind == Door.Key)
			{
				float yaw = MathF.Atan2(side.U, side.V) * 180 / MathF.PI;
				_o.Add("sign", f.W(u + along.U * 2.6f - side.U * 0.04f, v + along.V * 2.6f - side.V * 0.04f, f.Top + 2.1f), f.Yaw(yaw + DungeonProps.Turn("sign")),
					new[] { ("strings", "text", "Sealed. The key lies within these halls.") });
			}
		}
		Hangings(f, 2, Drapes);
		Clutter(f, 0.02f, new[] { "Skull1", "skull_pile", "LargeBone" });
		Elite(f, -1.6f, 0, 180);
		Elite(f, 1.6f, 0, 180);
		Lights(f, important: true);
	}

	private void Den(Frame f)
	{
		foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(4))
		{
			var (u, v) = In(s, 1.1f);
			Put(f, Chance(0.5) ? "goblin_strawpile" : "goblin_bed", u, v, s.Yaw, 1.1f);
		}
		Put(f, "goblin_trashpile", R(-1, 1), R(-1, 1), R(0, 360), 1.6f);
		Put(f, "goblin_totempole", 0, f.HalfD - 0.6f, 180, 0.6f);
		Put(f, "goblin_banner", -(f.HalfA - 0.6f), f.HalfD - 1.2f, 90, 0.6f);
		Brazier(f, R(-2, 2), -1.5f);
		Lights(f);
		Foes(f, 1.5f);
		Clutter(f, 0.025f);
		Decay(f);
	}

	private void Totem(Frame f)
	{
		Brazier(f, 0, 0);
		for (int i = 0; i < 5; i++)
		{
			float a = i * MathF.PI * 2 / 5;
			Put(f, "goblin_totempole", 3.2f * MathF.Cos(a), 3.2f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.6f);
		}
		foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(3))
		{
			var (u, v) = In(s, 0.5f);
			Put(f, "skull_pile", u, v, R(0, 360), 0.6f);
		}
		Hangings(f, 2);
		Foes(f, 1f, _foes.Where(x => x.Creature.Contains("Shaman", StringComparison.Ordinal)).DefaultIfEmpty(_foes[0]).ToArray());
		Foes(f, 0.6f);
		Clutter(f, 0.02f);
		Decay(f);
	}

	private void Mine(Frame f)
	{
		// Timber frames across the gallery, every 6 m.
		for (float v = -f.HalfD + 3; v <= f.HalfD - 2; v += 6)
		{
			_o.Add("dvergrtown_wood_support", f.W(0, v, f.F), f.Yaw(0));
			f.Take(-(f.HalfA - 0.4f), v, 0.6f);
			f.Take(f.HalfA - 0.4f, v, 0.6f);
		}
		foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(6))
		{
			var (u, v) = In(s, 0.7f);
			Put(f, One(new[] { "dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe", "dvergrprops_crate_long" }), u, v, s.Yaw + R(-20, 20), 0.6f);
		}
		Pickables(f, 1.5f);
		Lights(f);
		Foes(f, 0.8f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Workshop(Frame f)
	{
		Put(f, "dvergrprops_table", -1.4f, 0, 90, 1.1f);
		Put(f, "dvergrprops_table", 1.4f, 0, 90, 1.1f);
		Put(f, "dvergrprops_stool", -1.4f, -1.2f, 0, 0.3f);
		Put(f, "dvergrprops_stool", 1.4f, 1.2f, 180, 0.3f);
		Put(f, "dvergrprops_lantern_standing", -1.4f, 0.3f, 0, 0, y: 0.78f, force: true);
		Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 2, 90, 0.7f);
		Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
		foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(3))
		{
			var (u, v) = In(s, 0.12f);
			Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
		}
		Clutter(f, 0.02f, new[] { "dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe" });
		Lights(f);
		Foes(f, 0.6f);
		Decay(f);
	}

	private void Quarters(Frame f)
	{
		Put(f, Bed, -(f.HalfA - 1.2f), f.HalfD - 1.6f, 90, 1f);
		Put(f, Bed, f.HalfA - 1.2f, f.HalfD - 1.6f, -90, 1f);
		Put(f, "prop_chest_warderobe", 0, f.HalfD - 0.6f, 180, 0.9f);
		Put(f, One(new[] { "rug_wolf", "rug_fur", "rug_deer" }), 0, -0.5f, 0, 0, force: true);
		Put(f, "dvergrprops_table", 0, -0.5f, 0, 1.1f);
		Put(f, "dvergrprops_chair", -1.1f, -0.5f, 90, 0.4f);
		Put(f, "dvergrprops_chair", 1.1f, -0.5f, -90, 0.4f);
		Trophies(f, 1);
		if (Chance(0.5))
		{
			ChestAgainstWall(f);
		}
		Lights(f);
		Foes(f, 0.6f);
		Clutter(f, 0.01f);
		Decay(f);
	}

	private void Secret(Frame f)
	{
		Chest(f, -1.4f, f.HalfD - 1, 180, force: true);
		Chest(f, 1.4f, f.HalfD - 1, 180, force: true);
		Put(f, "treasure_pile", 0, 0.4f, R(0, 360), 1.1f);
		Put(f, _b.Bars, -2, -1, R(0, 360), 0.5f);
		Put(f, "Pickable_ForestCryptRemains01", 1.6f, -1.2f, R(0, 360), 0.5f);
		Pickables(f, 1.5f);
		Brazier(f, 2.4f, 1.2f);
	}
}
