using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

// What a generated dungeon is made of, as it is laid out: objects with their place and turn, and the
// object data some need (a chest's contents, a sign's text, a creature's stars). The same object twice
// at the same place (a slab shared by two levels, a wall seen from both sides) is kept once.
internal sealed class DungeonOut
{
	public readonly List<DungeonGen.Item> Items = new();
	private readonly HashSet<(string, int, int, int)> _seen = new();

	public static Quaternion Yaw(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);

	public static float YawOf((int X, int Z) d) => MathF.Atan2(d.X, d.Z) * 180f / MathF.PI;

	public void Add(string prefab, Vector3 p, float yaw, IReadOnlyList<(string Section, string Key, string Value)>? data = null) => Add(prefab, p, Yaw(yaw), data);

	public void Add(string prefab, Vector3 p, Quaternion q, IReadOnlyList<(string Section, string Key, string Value)>? data = null)
	{
		if (_seen.Add((prefab, (int)MathF.Round(p.X * 20), (int)MathF.Round(p.Y * 20), (int)MathF.Round(p.Z * 20))))
		{
			Items.Add(new DungeonGen.Item(prefab, p, q, data));
		}
	}
}

// The building: floor and roof slabs, walls, doorways, posts, beams, floor finish, stairs.
internal static class DungeonShell
{
	private static readonly (int X, int Z)[] Dirs = { (0, 1), (1, 0), (0, -1), (-1, 0) };

	public static void Build(DungeonPlan plan, Material mat, DungeonOut o, DungeonGen.Settings s)
	{
		foreach (var L in plan.Levels)
		{
			float F = DungeonPlan.FloorY(L.Index);
			Slabs(L, F, o);
			Walls(plan, L, F, mat, o);
			foreach (var d in plan.Doors.Where(d => d.A.Level == L.Index))
			{
				Doorway(d, F, mat, o);
			}
			if (mat.Floor != null && s.Decor >= 1)
			{
				foreach (var (t, sp) in L.Owner)
				{
					if (sp.Room is Room.StairTop or Room.StairBottom || L.Holes.Contains(DungeonPlan.CellOf(t)))
					{
						continue;
					}
					o.Add(mat.Floor, new Vector3(2 * t.Item1 + 1, F, 2 * t.Item2 + 1), 0);
				}
			}
			if (mat.Beam != null)
			{
				foreach (var sp in L.Spaces.Where(x => x.IsRoom && x.Room is not (Room.StairTop or Room.StairBottom)))
				{
					Beams(sp, F, mat, o);
				}
			}
			foreach (var top in L.Spaces.Where(x => x.Room == Room.StairTop))
			{
				Stairs(top, F, o);
			}
		}
	}

	// Black marble slabs (8 × 8 × 2 m), the floor and roof of every cell in use: the one piece the game
	// never asks for support, so all else can stand on them, high in the sky.
	private static void Slabs(DungeonPlan.Level L, float F, DungeonOut o)
	{
		var floors = new HashSet<(int, int)>();
		var roofs = new HashSet<(int, int)>();
		var high = new HashSet<(int, int)>();
		foreach (var (t, sp) in L.Owner)
		{
			var c = DungeonPlan.CellOf(t);
			if (!L.Holes.Contains(c))
			{
				floors.Add(c);
			}
			if (L.Open.Contains(c))
			{
				continue;
			}
			(sp.Tall ? high : roofs).Add(c);
		}
		foreach (var (ci, cj) in floors)
		{
			o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, F - 1, 8 * cj + 4), 0);
		}
		foreach (var (ci, cj) in roofs)
		{
			o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, F + 5, 8 * cj + 4), 0);
		}
		foreach (var (ci, cj) in high)
		{
			o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, F + 11, 8 * cj + 4), 0);
		}
	}

	// A wall's piece of plane: along the X axis (a plane x = Line) or the Z axis (z = Line), metres.
	private sealed record Edge(bool AlongZ, float Line, int Index, float Y0, float Y1);

	private static void Walls(DungeonPlan plan, DungeonPlan.Level L, float F, Material mat, DungeonOut o)
	{
		var doorEdges = new HashSet<((int, int), (int, int))>();
		foreach (var d in plan.Doors.Where(d => d.A.Level == L.Index))
		{
			foreach (var t in new[] { d.T1, d.T2 })
			{
				var n = (t.I + d.Dir.X, t.J + d.Dir.Z);
				doorEdges.Add((t, n));
				doorEdges.Add((n, t));
			}
		}
		var edges = new List<Edge>();
		foreach (var (t, sp) in L.Owner)
		{
			foreach (var dir in Dirs)
			{
				var n = (t.Item1 + dir.X, t.Item2 + dir.Z);
				var other = L.Owner.GetValueOrDefault(n);
				if (other == sp || sp.Corridor && other is { Corridor: true } || doorEdges.Contains((t, n)))
				{
					continue;
				}
				if (other != null && (n.Item1 < t.Item1 || n.Item2 < t.Item2))
				{
					continue; // the other side makes it
				}
				float y0 = F, y1 = F + Math.Max(DungeonPlan.HeightOf(sp), DungeonPlan.HeightOf(other));
				if (InWell(sp, t) || other != null && InWell(other, n))
				{
					y0 = F - 2; // down through the floor round a stairwell
				}
				bool alongZ = dir.X != 0;
				float line = alongZ ? 2 * Math.Max(t.Item1, n.Item1) : 2 * Math.Max(t.Item2, n.Item2);
				edges.Add(new Edge(alongZ, line, alongZ ? t.Item2 : t.Item1, y0, y1));
			}
		}
		var posts = new HashSet<(float, float, float, float)>();
		foreach (var g in edges.GroupBy(e => (e.AlongZ, e.Line, e.Y0, e.Y1)))
		{
			var idx = g.Select(e => e.Index).Distinct().OrderBy(i => i).ToList();
			for (int a = 0; a < idx.Count;)
			{
				int b = a;
				while (b + 1 < idx.Count && idx[b + 1] == idx[b] + 1)
				{
					b++;
				}
				float t0 = 2 * idx[a], t1 = 2 * idx[b] + 2;
				Fill(mat, o, g.Key.AlongZ, g.Key.Line, t0, t1, g.Key.Y0, g.Key.Y1);
				if (mat.Post != null)
				{
					posts.Add((g.Key.AlongZ ? g.Key.Line : t0, g.Key.AlongZ ? t0 : g.Key.Line, g.Key.Y0, g.Key.Y1));
					posts.Add((g.Key.AlongZ ? g.Key.Line : t1, g.Key.AlongZ ? t1 : g.Key.Line, g.Key.Y0, g.Key.Y1));
				}
				a = b + 1;
			}
		}
		foreach (var (x, z, y0, y1) in posts)
		{
			Stack(mat.Post!, o, x, z, y0, y1);
		}
	}

	// Whether tile t of space s is over its stairwell (no floor under it).
	private static bool InWell(DungeonPlan.Space s, (int, int) t) =>
		s.Room == Room.StairTop && DungeonPlanner.Depth((s.X0, s.Z0, s.W, s.H), s.Dir, t) >= 4;

	public static void Stack(Column c, DungeonOut o, float x, float z, float y0, float y1)
	{
		for (float y = y0; y < y1 - 0.3f; y += c.H)
		{
			o.Add(c.Prefab, new Vector3(x, y + c.Bottom, z), 0);
		}
	}

	// Fills a piece of wall (t0..t1 along the line, y0..y1) with the material's pieces, largest first.
	// Where no piece is low enough for what is left at the top, one rises past it, into the roof.
	public static void Fill(Material mat, DungeonOut o, bool alongZ, float line, float t0, float t1, float y0, float y1)
	{
		const float eps = 0.01f;
		if (t1 - t0 < 0.9f || y1 - y0 < 0.4f)
		{
			return;
		}
		var fits = mat.Walls.Where(p => p.W <= t1 - t0 + eps).ToList();
		if (fits.Count == 0)
		{
			return;
		}
		var low = fits.Where(p => p.H <= y1 - y0 + eps).ToList();
		var part = low.Count > 0 ? low.OrderByDescending(p => p.H).ThenByDescending(p => p.W).First() : fits.OrderBy(p => p.H).ThenByDescending(p => p.W).First();
		float tc = t0 + part.W / 2, y = y0 + part.Bottom;
		o.Add(part.Prefab, alongZ ? new Vector3(line, y, tc) : new Vector3(tc, y, line), alongZ ? 90 : 0);
		Fill(mat, o, alongZ, line, t0 + part.W, t1, y0, y0 + Math.Min(part.H, y1 - y0));
		Fill(mat, o, alongZ, line, t0, t1, y0 + part.H, y1);
	}

	// A doorway: 4 m of wall with its opening and what closes it.
	private static void Doorway(DungeonPlan.Doorway d, float F, Material mat, DungeonOut o)
	{
		var (cx, cz) = d.Center;
		bool alongZ = d.Dir.X != 0;
		float line = alongZ ? cx : cz, c = alongZ ? cz : cx, yaw = alongZ ? 90 : 0;
		float H = F + Math.Max(DungeonPlan.HeightOf(d.A), DungeonPlan.HeightOf(d.B));
		Vector3 At(float t, float y) => alongZ ? new Vector3(line, y, t) : new Vector3(t, y, line);
		void Frame(float doorTop)
		{
			Fill(mat, o, alongZ, line, c - 2, c - 1, F, H);
			Fill(mat, o, alongZ, line, c + 1, c + 2, F, H);
			Fill(mat, o, alongZ, line, c - 1, c + 1, F + doorTop, H);
		}
		switch (d.Kind)
		{
			case Door.Open:
				// A framed opening: jambs, and over it a lintel, or the material's arch, held by both.
				if (mat.Arch is { } arch && arch != "stone_arch")
				{
					Frame(2);
					o.Add(arch, At(c, F + (arch == "blackmarble_arch" ? 3 : 2)), yaw);
				}
				else
				{
					Frame(3);
				}
				break;
			case Door.Grand:
				o.Add("darkwood_gate", At(c - 1, F), yaw);
				o.Add("darkwood_gate", At(c + 1, F), yaw + 180);
				Fill(mat, o, alongZ, line, c - 2, c + 2, F + 4, H);
				break;
			case Door.Wood:
				Frame(3);
				o.Add("dungeon_forestcrypt_door", At(c, F), yaw);
				break;
			case Door.Gate:
				Frame(3);
				o.Add("dungeon_sunkencrypt_irongate", At(c, F), yaw);
				break;
			case Door.Key:
				Frame(3);
				o.Add("sunken_crypt_gate", At(c, F), yaw);
				break;
			case Door.Grate:
				Frame(3);
				o.Add("iron_grate", At(c, F + 1), yaw);
				break;
			case Door.Curtain:
				Frame(4);
				o.Add("cloth_hanging_door", At(c, F - 0.25f), yaw);
				break;
			case Door.Secret:
				if (mat.Name == "Stone")
				{
					// A cracked stretch of wall, to break through.
					o.Add("stone_wall_ruin_2", At(c - 1, F + 0.63f), alongZ ? -90 : 0);
				}
				else
				{
					o.Add("cloth_hanging_door_double", At(c, F - 0.25f), yaw);
				}
				Fill(mat, o, alongZ, line, c - 2, c + 2, F + 4.2f, H);
				break;
		}
	}

	// Beams under the roof across a room, every 4 m.
	private static void Beams(DungeonPlan.Space sp, float F, Material mat, DungeonOut o)
	{
		float top = F + DungeonPlan.HeightOf(sp) - mat.BeamTop;
		bool acrossX = sp.W <= sp.H; // beams span the narrower way
		float x0 = 2 * sp.X0, x1 = 2 * (sp.X0 + sp.W), z0 = 2 * sp.Z0, z1 = 2 * (sp.Z0 + sp.H);
		if (acrossX)
		{
			for (float z = z0 + 4; z < z1 - 1; z += 4)
			{
				for (float x = x0 + 2; x < x1 - 1; x += 4)
				{
					o.Add(mat.Beam!, new Vector3(x, top, z), 0);
				}
			}
		}
		else
		{
			for (float x = x0 + 4; x < x1 - 1; x += 4)
			{
				for (float z = z0 + 2; z < z1 - 1; z += 4)
				{
					o.Add(mat.Beam!, new Vector3(x, top, z), 90);
				}
			}
		}
	}

	// The stairs from a stairwell's first cell down to the middle of its last, against one side, on
	// stone stacks; a railing where the floor ends.
	private static void Stairs(DungeonPlan.Space top, float F, DungeonOut o)
	{
		var d = top.Dir;
		var a = (X: d.Z, Z: -d.X);
		float cx = top.CenterX, cz = top.CenterZ, half = top.Deep;
		Vector3 P(float u, float v, float y) => new(cx - d.X * half + a.X * u + d.X * v, y, cz - d.Z * half + a.Z * u + d.Z * v);
		float low = F - 6, yaw = DungeonOut.YawOf(d);
		for (int st = 0; st < 6; st++)
		{
			float v = 19 - 2 * st;
			o.Add("stone_stair", P(-2.5f, v, low + st), yaw);
			for (int b = 0; b < st; b++)
			{
				o.Add("stone_wall_2x1", P(-2.5f, v, low + b + 0.5f), yaw + 90);
			}
		}
		o.Add("stone_fence", P(-0.3f, 7.4f, F + 0.06f), yaw);
		o.Add("stone_fence", P(1.9f, 7.4f, F + 0.06f), yaw);
	}
}
