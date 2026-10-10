using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Editing;

// The game's structural support (WearNTear.UpdateSupport), worked out for a set of building pieces, so
// the editor shows what would hold and what would fall in game. Each piece has its material's support:
// full on the ground, else the best it gets from the pieces it rests on (their colliders that its own
// boxes, grown by 0.3 m, touch), less the material's loss for the distance between their centres of
// mass and the angle (horizontal loses more than vertical); two supports on opposite sides average.
// Repeated until nothing changes; pieces below their material's minimum break, then the rest is worked
// out again (the collapse spreading). The pieces' colliders, materials and centres come from the game
// (piece-support.json, tools/asset-export/scan_piece_support.py). Not counted: other objects (rocks,
// trees) the game would also take as full support, and mesh colliders' exact shapes (their boxes).
public static class Stability
{
	// A collider of a piece, in its own frame: middle, half size, rotation; Aabb: a non-box collider (its
	// axis-aligned bounds count); Counts: on a layer the game's support check looks at.
	private sealed record Box(Vector3 C, Vector3 H, Quaternion Q, bool Aabb, bool Counts);

	// Free: the game never checks its support (WearNTear.m_noSupportWear off): it stays up, at its
	// material's full support, wherever it is.
	private sealed record Data(int Material, bool Supports, Vector3 Com, Box[] Boxes, bool Free = false);

	private static readonly Lazy<Dictionary<string, Data>> Pieces = new(Load);

	// The layers WearNTear's overlap test sees: Default, piece, terrain, static_solid, Default_small.
	private static readonly HashSet<int> SupportLayers = new() { 0, 10, 11, 15, 20 };

	private static Dictionary<string, Data> Load()
	{
		using Stream s = typeof(Stability).Assembly.GetManifestResourceStream("TerrainEditor.piece-support.json")
			?? throw new InvalidOperationException("piece-support.json is not embedded");
		using JsonDocument doc = JsonDocument.Parse(s);
		var result = new Dictionary<string, Data>();
		foreach (JsonProperty p in doc.RootElement.EnumerateObject())
		{
			var c = p.Value.GetProperty("c");
			var boxes = p.Value.GetProperty("b").EnumerateArray().Select(b =>
			{
				float F(int i) => b[i].GetSingle();
				return new Box(new Vector3(F(0), F(1), F(2)), new Vector3(F(3), F(4), F(5)), new Quaternion(F(6), F(7), F(8), F(9)), b[10].GetInt32() != 0, SupportLayers.Contains(b[11].GetInt32()));
			}).ToArray();
			result[p.Name] = new Data(p.Value.GetProperty("m").GetInt32(), p.Value.GetProperty("s").GetInt32() != 0,
				new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle()), boxes, p.Value.TryGetProperty("f", out var f) && f.GetInt32() != 0);
		}
		return result;
	}

	// Whether the game gives this piece structural support at all (walls, floors, beams: yes; a
	// workbench, a chest: no, they rest on what is under them without counting).
	public static bool Known(string prefab) => Pieces.Value.ContainsKey(prefab);

	// A piece's material (WearNTear: 0 wood, 1 stone, 2 iron… -1 unknown) and its colliders as boxes in
	// its own frame (middle, half size, rotation): its rough shape, for pictures without its model.
	public static (int Material, List<(Vector3 C, Vector3 H, Quaternion Q)> Boxes) Shape(string prefab) =>
		Pieces.Value.TryGetValue(prefab, out var d) ? (d.Material, d.Boxes.Select(b => (b.C, b.H, b.Q)).ToList()) : (-1, new());

	// WearNTear.GetMaterialProperties: maximum and minimum support, losses per metre.
	public static (float Max, float Min, float Horizontal, float Vertical) Material(int m) => m switch
	{
		0 => (100f, 10f, 0.2f, 0.125f),          // wood
		1 => (1000f, 100f, 1f, 0.125f),          // stone
		2 => (1500f, 20f, 1f / 13f, 1f / 13f),   // iron
		3 => (140f, 10f, 1f / 6f, 0.1f),         // hardwood (core wood)
		4 => (1500f, 100f, 0.5f, 0.125f),        // marble (black marble)
		5 => (2000f, 100f, 1f / 3f, 0.1f),       // ashstone (grausten)
		6 => (5000f, 100f, 0.25f, 1f / 15f),     // ancient
		7 => (1000f, 100f, 1f / 3f, 0.125f),     // ice
		8 => (200f, 10f, 0.2f, 1f / 13f),        // timberwood
		_ => (0f, 0f, 0f, 0f),
	};

	public sealed record Piece(string Prefab, Vector3 Position, Quaternion Rotation, float Scale = 1f);

	// Per piece: its support, its colour value as the game shows it in build mode (-1: full, on the
	// ground, blue; else 0 red, about to break, to 1 green), and whether it breaks; Free: pieces the
	// game does not give support to (not counted). Broken pieces are given in the order they fall.
	public sealed record Result(float[] Support, float[] Colour, bool[] Breaks, bool[] Free, List<int> Falls)
	{
		public int Breaking => Falls.Count;
	}

	// An oriented box in the world.
	private readonly record struct Obb(Vector3 C, Vector3 X, Vector3 Y, Vector3 Z, Vector3 H)
	{
		public IEnumerable<Vector3> Corners()
		{
			for (int i = 0; i < 8; i++)
			{
				yield return C + X * ((i & 1) != 0 ? H.X : -H.X) + Y * ((i & 2) != 0 ? H.Y : -H.Y) + Z * ((i & 4) != 0 ? H.Z : -H.Z);
			}
		}

		public float Radius => H.Length();

		// The point of the box nearest to p (Collider.ClosestPoint).
		public Vector3 Closest(Vector3 p)
		{
			var d = p - C;
			return C + X * Math.Clamp(Vector3.Dot(d, X), -H.X, H.X) + Y * Math.Clamp(Vector3.Dot(d, Y), -H.Y, H.Y) + Z * Math.Clamp(Vector3.Dot(d, Z), -H.Z, H.Z);
		}
	}

	private static Obb World(Box b, Piece p, float grow)
	{
		float s = p.Scale > 0 ? p.Scale : 1f;
		var q = p.Rotation * b.Q;
		var c = p.Position + Vector3.Transform(b.C * s, p.Rotation);
		var obb = new Obb(c, Vector3.Transform(Vector3.UnitX, q), Vector3.Transform(Vector3.UnitY, q), Vector3.Transform(Vector3.UnitZ, q), b.H * s);
		if (b.Aabb)
		{
			// Collider.bounds: the turned box's axis-aligned bounds.
			Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
			foreach (var corner in obb.Corners())
			{
				lo = Vector3.Min(lo, corner);
				hi = Vector3.Max(hi, corner);
			}
			obb = new Obb((lo + hi) / 2, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, (hi - lo) / 2);
		}
		return obb with { H = obb.H + new Vector3(grow) };
	}

	// Separating axis test of two oriented boxes.
	private static bool Overlap(in Obb a, in Obb b)
	{
		var d = b.C - a.C;
		if (d.Length() > a.Radius + b.Radius)
		{
			return false;
		}
		Span<Vector3> axes = stackalloc Vector3[15];
		axes[0] = a.X; axes[1] = a.Y; axes[2] = a.Z; axes[3] = b.X; axes[4] = b.Y; axes[5] = b.Z;
		int n = 6;
		foreach (var u in new[] { a.X, a.Y, a.Z })
		{
			foreach (var v in new[] { b.X, b.Y, b.Z })
			{
				var c = Vector3.Cross(u, v);
				if (c.LengthSquared() > 1e-8f)
				{
					axes[n++] = Vector3.Normalize(c);
				}
			}
		}
		for (int i = 0; i < n; i++)
		{
			var ax = axes[i];
			float ra = a.H.X * MathF.Abs(Vector3.Dot(a.X, ax)) + a.H.Y * MathF.Abs(Vector3.Dot(a.Y, ax)) + a.H.Z * MathF.Abs(Vector3.Dot(a.Z, ax));
			float rb = b.H.X * MathF.Abs(Vector3.Dot(b.X, ax)) + b.H.Y * MathF.Abs(Vector3.Dot(b.Y, ax)) + b.H.Z * MathF.Abs(Vector3.Dot(b.Z, ax));
			if (MathF.Abs(Vector3.Dot(d, ax)) > ra + rb)
			{
				return false;
			}
		}
		return true;
	}

	// groundAt: the ground's height at a world x, z.
	public static Result Solve(IReadOnlyList<Piece> pieces, Func<float, float, float> groundAt)
	{
		int n = pieces.Count;
		var data = pieces.Select(p => Pieces.Value.GetValueOrDefault(p.Prefab)).ToArray();
		// Each piece's grown boxes (what it looks with) and its colliders that others see.
		var grown = new Obb[n][];
		var seen = new Obb[n][];
		var com = new Vector3[n];
		var grounded = new bool[n];
		for (int i = 0; i < n; i++)
		{
			var d = data[i];
			if (d == null)
			{
				grown[i] = seen[i] = Array.Empty<Obb>();
				continue;
			}
			grown[i] = d.Boxes.Select(b => World(b, pieces[i], 0.15f)).ToArray();
			seen[i] = d.Boxes.Where(b => b.Counts).Select(b => World(b, pieces[i], 0f)).ToArray();
			com[i] = pieces[i].Position + Vector3.Transform(d.Com, pieces[i].Rotation);
			// Touching the terrain: a grown box reaching the ground anywhere under it.
			grounded[i] = d.Free || grown[i].Any(g => g.Corners().Append(g.C).Any(c => c.Y <= groundAt(c.X, c.Z)));
		}
		// Who touches whom (the boxes do not move): for each piece, the other pieces' colliders its own
		// grown boxes reach.
		var touches = new List<(int J, Obb Collider)>[n];
		for (int i = 0; i < n; i++)
		{
			touches[i] = new();
			if (data[i] == null)
			{
				continue;
			}
			for (int j = 0; j < n; j++)
			{
				if (j == i || data[j] is not { Supports: true })
				{
					continue;
				}
				foreach (var col in seen[j])
				{
					if (grown[i].Any(g => Overlap(g, col)))
					{
						touches[i].Add((j, col));
					}
				}
			}
		}
		var support = new float[n];
		var alive = new bool[n];
		for (int i = 0; i < n; i++)
		{
			alive[i] = data[i] != null;
			support[i] = data[i] != null ? Material(data[i]!.Material).Max : 0;
		}
		var falls = new List<int>();
		while (true)
		{
			// Settle (from full support down, as new pieces start in game).
			for (int round = 0; round < 400; round++)
			{
				float change = 0;
				var next = (float[])support.Clone();
				for (int i = 0; i < n; i++)
				{
					if (!alive[i])
					{
						continue;
					}
					var (max, _, hLoss, vLoss) = Material(data[i]!.Material);
					if (grounded[i])
					{
						next[i] = max;
						continue;
					}
					float best = 0;
					var points = new List<(Vector3 P, float V)>();
					foreach (var (j, col) in touches[i])
					{
						if (!alive[j])
						{
							continue;
						}
						float s2 = support[j];
						float dist = Vector3.Distance(com[i], com[j]) + 0.1f, toPos = Vector3.Distance(com[i], pieces[j].Position) + 0.1f;
						if (toPos < dist)
						{
							dist = toPos;
						}
						best = MathF.Max(best, s2 - hLoss * dist * s2);
						var sp = col.Closest(com[i]);
						if (sp.Y < com[i].Y + 0.05f)
						{
							var dir = sp - com[i];
							if (dir.LengthSquared() > 1e-10f)
							{
								dir = Vector3.Normalize(dir);
								if (dir.Y < 0)
								{
									float t = MathF.Acos(1f - MathF.Abs(dir.Y)) / (MathF.PI / 2f);
									float loss = hLoss + (vLoss - hLoss) * Math.Clamp(t, 0, 1);
									best = MathF.Max(best, s2 - loss * dist * s2);
								}
							}
							points.Add((sp, s2 - vLoss * dist * s2));
						}
					}
					// Held from two sides (at least 100° apart seen from above): the average of the two.
					for (int a = 0; a < points.Count - 1; a++)
					{
						var va = points[a].P - com[i];
						va.Y = 0;
						for (int b = a + 1; b < points.Count; b++)
						{
							float avg = (points[a].V + points[b].V) * 0.5f;
							if (avg <= best)
							{
								continue;
							}
							var vb = points[b].P - com[i];
							vb.Y = 0;
							if (va.LengthSquared() > 1e-10f && vb.LengthSquared() > 1e-10f
								&& MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(va), Vector3.Normalize(vb)), -1, 1)) * 180 / MathF.PI >= 100f)
							{
								best = avg;
							}
						}
					}
					next[i] = MathF.Min(best, max);
					change = MathF.Max(change, MathF.Abs(next[i] - support[i]));
				}
				support = next;
				if (change < 1e-3f)
				{
					break;
				}
			}
			// What breaks first: the pieces below their minimum; then the rest again without them.
			var broken = Enumerable.Range(0, n).Where(i => alive[i] && support[i] < Material(data[i]!.Material).Min).ToList();
			if (broken.Count == 0)
			{
				break;
			}
			foreach (int i in broken)
			{
				alive[i] = false;
				falls.Add(i);
			}
		}
		var colour = new float[n];
		var breaks = new bool[n];
		var free = new bool[n];
		foreach (int i in falls)
		{
			breaks[i] = true;
		}
		for (int i = 0; i < n; i++)
		{
			free[i] = data[i] == null;
			if (data[i] == null)
			{
				continue;
			}
			var (max, min, _, _) = Material(data[i]!.Material);
			// WearNTear.GetSupportColorValue.
			colour[i] = breaks[i] ? 0 : support[i] >= max ? -1 : Math.Clamp((support[i] - min) / (max * 0.5f - min), 0, 1);
		}
		return new Result(support, colour, breaks, free, falls);
	}
}
