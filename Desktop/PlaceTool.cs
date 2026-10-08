using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Place tool, like the web editor's (editor/plant.js): any kind of object, painted with a brush (a
// density and a minimum spacing, like WorldPainter's tree layers; Shift + drag removes the chosen
// kinds), or along lines, circles and rectangles (pieces end to end at their snap points, like the
// hammer), in grids and in zones. Positions are grid points of the block (x east, z north).
public sealed class PlaceTool
{
	public enum Modes { Brush, Line, Grid, Zone }
	public enum LineShapes { Points, Circle, Rect }
	public enum Elevations { Ground, Above, At }

	// ---- What the tool needs from the scene (set by the window).
	public Func<WorldScene?> Scene { get; set; } = () => null;
	public Func<Func<int, float>?> Mask { get; set; } = () => null;
	// A kind's model box in its own frame (Unity axes, scaled), or null while unknown.
	public Func<string, (Vector3 Min, Vector3 Max)?> ModelBox { get; set; } = _ => null;
	// The brush's radius (shared with the sculpt brushes) and strength.
	public Brush Brush { get; set; } = new();

	// ---- Settings.
	public Modes Mode { get; set; } = Modes.Brush;
	public LineShapes LineShape { get; set; } = LineShapes.Points;
	public List<string> Chosen { get; } = new() { "Beech1" };
	public Dictionary<string, int> Weights { get; } = new();
	public float Density { get; set; } = 3;
	public float Spacing { get; set; } = 4;
	public float Clump { get; set; }
	public float Patch { get; set; } = 30;
	public float SizeMin { get; set; } = 80;
	public float SizeMax { get; set; } = 120;
	public float Tilt { get; set; } = 3;
	public float Rotation { get; set; }
	public bool RandomYaw { get; set; } = true;
	public bool Single { get; set; }
	public float Every { get; set; } = 4;
	public float Wiggle { get; set; }
	public bool Along { get; set; } = true;
	public bool Curve { get; set; } = true;
	public bool EndToEnd { get; set; }
	public int Layers { get; set; } = 1;
	public bool Loop { get; set; }
	public float Cell { get; set; } = 4;
	public Elevations Elevation { get; set; } = Elevations.Ground;
	public float Elev { get; set; }
	public bool SnapTo { get; set; } = true;
	public bool OnTop { get; set; }
	public bool GrowRoom { get; set; } = true;
	// Settings or the line changed: the preview is worked out again.
	public event Action? Changed;
	public void Notify() => Changed?.Invoke();

	private Random _rand = new();
	private float _clumpSeed = Random.Shared.NextSingle() * 1000;
	public const int MaxShape = 2000;

	public int WeightOf(string n) => Weights.GetValueOrDefault(n, 1);

	// The kind for a random draw t in [0, 1), by weight.
	public string PickName(IReadOnlyList<string> names, float t)
	{
		float total = names.Sum(WeightOf), x = t * total;
		foreach (var n in names)
		{
			x -= WeightOf(n);
			if (x < 0)
			{
				return n;
			}
		}
		return names[^1];
	}

	public static float[][] SnapsOf(string name) => PieceCatalog.Get(StableHash.Of(name))?.Snaps ?? Array.Empty<float[]>();
	public static bool IsPiece(string name) => SnapsOf(name).Length >= 2;
	public bool PiecesChosen => Chosen.Any(IsPiece);

	// End to end switches itself on when every chosen kind is a piece the game snaps (fences, walls…),
	// and pieces go one at a time under the cursor, until those are set by hand.
	public bool EndToEndByHand { get; set; }
	public bool SingleByHand { get; set; }
	public void AutoSnap()
	{
		bool pieces = Chosen.Count > 0 && Chosen.All(IsPiece);
		if (!EndToEndByHand)
		{
			EndToEnd = pieces;
		}
		if (!SingleByHand)
		{
			Single = pieces;
		}
	}

	// ---- Saplings and crops: the game lets them grow only with nothing within their grow radius.
	private static readonly Dictionary<string, (float Radius, bool Cultivated)> GrowInfo = LoadGrow();

	private static Dictionary<string, (float, bool)> LoadGrow()
	{
		var d = PrefabCatalog.Placeable.Where(p => p.GrowRadius > 0).ToDictionary(p => p.Name, p => (p.GrowRadius, p.NeedsCultivated));
		foreach (var (name, g) in PrefabCatalog.GrownFrom)
		{
			d.TryAdd(name, (g.Radius, false));
		}
		return d;
	}

	public bool GrowMatters => Chosen.Any(n => GrowInfo.TryGetValue(n, out var g) && g.Radius > 0);
	public List<string> NeedCultivated => Chosen.Where(n => GrowInfo.TryGetValue(n, out var g) && g.Cultivated).ToList();
	private float GrowNeed(string name) => GrowMatters && GrowRoom && GrowInfo.TryGetValue(name, out var g) ? g.Radius : 0;

	// ---- Where objects can go.
	private sealed class Hash
	{
		public readonly float Cell;
		private readonly Dictionary<(int, int), List<Vector2>> _map = new();
		public Hash(float cell) => Cell = cell;
		public void Add(Vector2 p)
		{
			var k = ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell));
			(_map.TryGetValue(k, out var l) ? l : _map[k] = new()).Add(p);
		}
		public bool Free(Vector2 p, float spacing)
		{
			int cx = (int)MathF.Floor(p.X / Cell), cz = (int)MathF.Floor(p.Y / Cell), n = Math.Max(1, (int)MathF.Ceiling(spacing / Cell));
			for (int dz = -n; dz <= n; dz++)
			{
				for (int dx = -n; dx <= n; dx++)
				{
					if (_map.TryGetValue((cx + dx, cz + dz), out var l) && l.Any(q => Vector2.Distance(q, p) < spacing))
					{
						return false;
					}
				}
			}
			return true;
		}
	}

	// Every object standing in the block (grid positions), for the spacing check.
	private Hash Standing(WorldScene s, float cell)
	{
		var h = new Hash(cell);
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		lock (s.Things)
		{
			foreach (var t in s.Things)
			{
				if (!t.Gone)
				{
					h.Add(new Vector2(t.Position.X - ox, t.Position.Z - oz));
				}
			}
		}
		return h;
	}

	public sealed record Draw(float T, float K, float Rx, float Ry, float Rz, float S, float W);
	private Draw NewDraw() => new(_rand.NextSingle(), _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle());

	// A placement: the kind, where (world position, and the grid point), turned and scaled.
	public sealed record Placement(string Name, Vector3 Position, Vector3 Rotation, float Scale, Vector2 G);

	private float HeightAt(WorldScene s, Vector2 g) => Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Y - (s.H - 1) / 2f));

	// Clumping (Brush and Zone): the density follows a noise pattern in world coordinates, so objects
	// gather in groves and leave clearings. 0: even.
	private bool ClumpKeeps(WorldScene s, Vector2 g, float k)
	{
		float c = Clump / 100;
		if (c <= 0)
		{
			return true;
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		float n = Math.Clamp(0.5f + 0.5f * Brush.Noise.Fbm((ox + g.X) / Patch + _clumpSeed, (oz + g.Y) / Patch - _clumpSeed), 0, 1);
		float lo = c * 0.75f - 0.1f, t = Math.Clamp((n - lo) / 0.25f, 0, 1);
		return k < t * t * (3 - 2 * t);
	}

	private static bool UnderwaterOk(IEnumerable<string> names) => names.Any(n => n.Contains("kelp", StringComparison.OrdinalIgnoreCase) || n.Contains("seaweed", StringComparison.OrdinalIgnoreCase));

	// A piece's bottom below its origin (its lowest snap point: walls have their origin in the middle).
	private static float? BottomOf(string name) => SnapsOf(name) is { Length: > 0 } sp ? sp.Min(p => p[1]) : null;

	// Elevation: on the ground, a height above it, or one height for all.
	public float Elevated(string name, float groundY)
	{
		float? b = BottomOf(name);
		return Elevation == Elevations.At ? Elev - (b ?? 0) : groundY - (b ?? 0.05f) + (Elevation == Elevations.Above ? Elev : 0);
	}

	// Zone and grid shapes can be turned as a whole; their objects turn with them.
	public float ShapeTurn { get; private set; }
	private Vector2? _pivot;
	private bool Turnable => Mode is Modes.Zone or Modes.Grid;
	private float RotationNow => Rotation + (Turnable ? ShapeTurn : 0);

	private Placement? PlacementAt(WorldScene s, Vector2 g, Draw d, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, float? minDist = null, float? yaw = null)
	{
		if (g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2)
		{
			return null;
		}
		if (mask != null && mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0)
		{
			return null;
		}
		if (Mode is Modes.Brush or Modes.Zone && !ClumpKeeps(s, g, d.K))
		{
			return null;
		}
		string name = PickName(names, d.T);
		// One at a time: you pick the spot, so only an object right on top (0.3 m) blocks it; a piece that
		// snaps to the pieces there goes where the snapping puts it (On top: right over the one below).
		float near = Single ? SnapTo && IsPiece(name) && Mode == Modes.Brush ? 0 : 0.3f : Spacing;
		if (!hash.Free(g, MathF.Max(minDist ?? near, GrowNeed(name))))
		{
			return null;
		}
		float y = HeightAt(s, g);
		if (Elevation == Elevations.Ground && y < s.Water - 0.3f && !UnderwaterOk(names))
		{
			return null;
		}
		float smin = SizeMin / 100, smax = MathF.Max(smin, SizeMax / 100);
		float ry = (yaw ?? (RandomYaw ? d.Ry * 360 : 0)) + RotationNow;
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		return new Placement(name, new Vector3(ox + g.X, Elevated(name, y), oz + g.Y), new Vector3(d.Rx * Tilt, ry, d.Rz * Tilt), smin + d.S * (smax - smin), g);
	}

	// ---- Brush: the layout under the cursor (offsets), kept until the settings change or R.
	private List<(Vector2 D, Draw Draw)> _pattern = new();
	private string _patternKey = "";
	private string SettingsKey => $"{Brush.Radius}|{Density}|{Spacing}|{Single}|{string.Join(",", Chosen)}";

	public void NewLayout()
	{
		_patternKey = "";
		_draws.Clear();
		_scatter = null;
		_clumpSeed = _rand.NextSingle() * 1000;
		Notify();
	}

	private void MakePattern()
	{
		float r = Brush.Radius;
		int n = Math.Min(400, (int)MathF.Round(Density / 100 * MathF.PI * r * r));
		_pattern = new();
		if (Single)
		{
			_pattern.Add((Vector2.Zero, NewDraw()));
		}
		else
		{
			// Dart throwing: random points in the disc, at least the spacing apart.
			for (int tries = 0; _pattern.Count < n && tries < n * 30; tries++)
			{
				float a = _rand.NextSingle() * MathF.Tau, dist = MathF.Sqrt(_rand.NextSingle()) * r;
				var p = new Vector2(MathF.Cos(a) * dist, MathF.Sin(a) * dist);
				if (_pattern.Any(q => Vector2.Distance(q.D, p) < Spacing))
				{
					continue;
				}
				_pattern.Add((p, NewDraw()));
			}
		}
		_patternKey = SettingsKey;
	}

	// ---- Line, grid and zone shapes.
	public List<Vector2> Points { get; } = new();
	public Vector2? GridA { get; set; }
	public Vector2? GridB { get; set; }
	public Vector2? FigA { get; set; }
	public Vector2? FigB { get; set; }
	private readonly List<Draw> _draws = new();
	private Draw DrawAt(int i)
	{
		while (_draws.Count <= i)
		{
			_draws.Add(NewDraw());
		}
		return _draws[i];
	}

	public Vector2 Xf(Vector2 p)
	{
		if (ShapeTurn == 0 || _pivot is not { } c)
		{
			return p;
		}
		float t = ShapeTurn * MathF.PI / 180, co = MathF.Cos(t), sn = MathF.Sin(t);
		var d = p - c;
		return new Vector2(c.X + d.X * co + d.Y * sn, c.Y - d.X * sn + d.Y * co);
	}

	public Vector2 Inv(Vector2 p)
	{
		if (ShapeTurn == 0 || _pivot is not { } c)
		{
			return p;
		}
		float t = -ShapeTurn * MathF.PI / 180, co = MathF.Cos(t), sn = MathF.Sin(t);
		var d = p - c;
		return new Vector2(c.X + d.X * co + d.Y * sn, c.Y - d.X * sn + d.Y * co);
	}

	// A point as kept: zone outlines are kept unturned.
	public Vector2 Local(Vector2 p) => Mode == Modes.Zone ? Inv(p) : p;

	public bool TurnShape(float degrees)
	{
		var pts = Mode == Modes.Zone ? Points.ToList() : GridA is { } a && GridB is { } b ? new List<Vector2> { a, b } : new();
		if (pts.Count == 0)
		{
			return false;
		}
		_pivot ??= new Vector2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
		ShapeTurn = ((ShapeTurn + degrees + 180) % 360 + 360) % 360 - 180;
		Notify();
		return true;
	}

	// A new grid box starts unturned.
	public void ClearTurn()
	{
		ShapeTurn = 0;
		_pivot = null;
	}

	public void ClearShape()
	{
		lock (Points)
		{
			Points.Clear();
		}
		FigA = FigB = GridA = GridB = null;
		ShapeTurn = 0;
		_pivot = null;
		Notify();
	}

	public void TurnBy(float degrees)
	{
		Rotation = ((Rotation + degrees + 180) % 360 + 360) % 360 - 180;
		Notify();
	}

	// The line through the points: a smooth curve or straight stretches (Seg: the point each starts from).
	public List<(Vector2 P, int Seg)> LineCurve()
	{
		List<Vector2> p;
		lock (Points)
		{
			p = Points.ToList();
		}
		if (p.Count < 3 || !Curve)
		{
			return p.Select((q, i) => (q, Math.Min(i, Math.Max(0, p.Count - 2)))).ToList();
		}
		var outp = new List<(Vector2, int)>();
		for (int i = 0; i < p.Count - 1; i++)
		{
			Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Count - 1, i + 2)];
			int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
			for (int k = 0; k < steps; k++)
			{
				float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
				Vector2 Cr(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => 0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
				outp.Add((Cr(p0, p1, p2, p3), i));
			}
		}
		outp.Add((p[^1], p.Count - 2));
		return outp;
	}

	// ---- End to end: a piece's two ends are the middles of the faces along its longer side, from its
	// snap points or, for kinds without any, from its model's box.
	public sealed record Ends(Vector3 A, Vector3 B, float Len, float H, float Heading);

	private static Ends? EndsFrom(float[] xs, float[] ys, float[] zs)
	{
		float x0 = xs.Min(), x1 = xs.Max(), z0 = zs.Min(), z1 = zs.Max(), y = ys.Min();
		bool alongX = x1 - x0 >= z1 - z0 - 1e-3f;
		float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
		var a = alongX ? new Vector3(x0, y, cz) : new Vector3(cx, y, z0);
		var b = alongX ? new Vector3(x1, y, cz) : new Vector3(cx, y, z1);
		float len = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
		return len > 0.05f ? new Ends(a, b, len, ys.Max() - y, MathF.Atan2(b.X - a.X, b.Z - a.Z)) : null;
	}

	public Ends? EndsOf(string name)
	{
		var sp = SnapsOf(name);
		if (sp.Length >= 2 && EndsFrom(sp.Select(p => p[0]).ToArray(), sp.Select(p => p[1]).ToArray(), sp.Select(p => p[2]).ToArray()) is { } e)
		{
			return e;
		}
		return ModelBox(name) is var (lo, hi) ? EndsFrom(new[] { lo.X, hi.X }, new[] { lo.Y, hi.Y }, new[] { lo.Z, hi.Z }) : null;
	}

	// End to end: the figure's size snaps so that whole pieces close it.
	public Vector2 SnapFigure(Vector2 a, Vector2 b)
	{
		var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
		if (e == null)
		{
			return b;
		}
		if (LineShape == LineShapes.Rect)
		{
			float Fit(float d) => MathF.Sign(d == 0 ? 1 : d) * MathF.Max(1, MathF.Round(MathF.Abs(d) / e.Len)) * e.Len;
			return new Vector2(a.X + Fit(b.X - a.X), a.Y + Fit(b.Y - a.Y));
		}
		float r = Vector2.Distance(a, b);
		int n = Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len));
		float R = e.Len / (2 * MathF.Sin(MathF.PI / n)), t = MathF.Atan2(b.Y - a.Y, b.X - a.X);
		return new Vector2(a.X + MathF.Cos(t) * R, a.Y + MathF.Sin(t) * R);
	}

	// The line as runs of points: one run, or a rectangle's four sides each on their own when placing
	// end to end (so its corners stay exact).
	public List<List<Vector2>> LineRuns()
	{
		if (LineShape == LineShapes.Points)
		{
			var c = LineCurve().Select(q => q.P).ToList();
			if (Loop && c.Count >= 3)
			{
				c.Add(c[0]);
			}
			return c.Count >= 2 ? new() { c } : new();
		}
		if (FigA is not { } a || FigB is not { } fb)
		{
			return new();
		}
		var b = SnapFigure(a, fb);
		if (LineShape == LineShapes.Rect)
		{
			if (MathF.Abs(b.X - a.X) < 0.5f || MathF.Abs(b.Y - a.Y) < 0.5f)
			{
				return new();
			}
			var c = new List<Vector2> { a, new(b.X, a.Y), b, new(a.X, b.Y) };
			return EndToEnd ? Enumerable.Range(0, 4).Select(i => new List<Vector2> { c[i], c[(i + 1) % 4] }).ToList() : new() { c.Append(c[0]).ToList() };
		}
		float R = Vector2.Distance(a, b);
		if (R < 0.5f)
		{
			return new();
		}
		// End to end: one point per piece, so the pieces meet at the corners of a regular polygon.
		var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
		int n = e != null ? Math.Max(3, (int)MathF.Round(MathF.Tau * R / e.Len)) : Math.Max(16, (int)MathF.Ceiling(MathF.Tau * R / 1.5f));
		float t0 = MathF.Atan2(b.Y - a.Y, b.X - a.X);
		return new() { Enumerable.Range(0, n + 1).Select(i => a + new Vector2(MathF.Cos(t0 + i / (float)n * MathF.Tau), MathF.Sin(t0 + i / (float)n * MathF.Tau)) * R).ToList() };
	}

	// How much of the line is left at the end once no whole piece fits (end to end).
	public float SnapGap { get; private set; }

	// The point along the run at a straight distance dist from s, searching from segment seg at
	// fraction t0 (forwards only), or null past its end.
	private static (Vector2 Q, int Seg, float T)? AlongRun(List<Vector2> pts, Vector2 s, int seg, float t0, float dist)
	{
		for (; seg < pts.Count - 1; seg++, t0 = 0)
		{
			Vector2 a = pts[seg], d = pts[seg + 1] - a, f = a - s;
			float A = d.LengthSquared(), B = 2 * Vector2.Dot(f, d), C = f.LengthSquared() - dist * dist, disc = B * B - 4 * A * C;
			if (A < 1e-9f || disc < 0)
			{
				continue;
			}
			float t = (-B + MathF.Sqrt(disc)) / (2 * A);
			// A millimetre of slack at the end: float rounding over a whole ring of pieces otherwise
			// leaves the last one just past the end, and the ring one piece short.
			float slack = 1e-3f / MathF.Sqrt(A);
			if (t >= t0 - 1e-6f && t <= 1 + slack)
			{
				t = MathF.Min(t, 1);
				return (a + d * t, seg, t);
			}
		}
		return null;
	}

	// Unity's rotation (Euler degrees: z, then x, then y) of a local point.
	public static Vector3 RotU(Vector3 p, Vector3 r)
	{
		const float D = MathF.PI / 180;
		float cz = MathF.Cos(r.Z * D), sz = MathF.Sin(r.Z * D), cx = MathF.Cos(r.X * D), sx = MathF.Sin(r.X * D), cy = MathF.Cos(r.Y * D), sy = MathF.Sin(r.Y * D);
		float x = p.X, y = p.Y, z = p.Z;
		(x, y) = (x * cz - y * sz, x * sz + y * cz);
		(y, z) = (y * cx - z * sx, y * sx + z * cx);
		return new Vector3(x * cy + z * sy, y, -x * sy + z * cy);
	}

	// The snap points of every piece in the block (grid x, height, grid z).
	private sealed record Other(int Index, string Name, Vector2 G, Vector3 Rotation, List<Vector3> Pts, float Low);
	private List<Other> OtherSnaps(WorldScene s, Func<int, string?> nameOf)
	{
		var list = new List<Other>();
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		lock (s.Things)
		{
			for (int i = 0; i < s.Things.Count; i++)
			{
				var t = s.Things[i];
				if (t.Gone || PieceCatalog.Get(t.Prefab) is not { Snaps.Length: > 0 } info)
				{
					continue;
				}
				var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
				var pts = info.Snaps.Select(p => { var q = RotU(new Vector3(p[0], p[1], p[2]), t.Rotation); return new Vector3(g.X + q.X, t.Position.Y + q.Y, g.Y + q.Z); }).ToList();
				list.Add(new Other(i, nameOf(t.Prefab) ?? info.Name, g, t.Rotation, pts, pts.Min(p => p.Y)));
			}
		}
		return list;
	}

	public Func<int, string?> NameOf { get; set; } = _ => null;

	// The snap point of a piece already there nearest to p (along the ground), within d metres.
	private static Vector3? NearestSnap(List<Other> others, Vector2 p, float d)
	{
		Vector3? best = null;
		float bd = d;
		foreach (var o in others)
		{
			if (MathF.Abs(o.G.X - p.X) > d + 8 || MathF.Abs(o.G.Y - p.Y) > d + 8)
			{
				continue;
			}
			foreach (var q in o.Pts)
			{
				float dd = Vector2.Distance(new Vector2(q.X, q.Z), p);
				if (dd < bd)
				{
					bd = dd;
					best = q;
				}
			}
		}
		return best;
	}

	// Places pieces end to end along one run, from its start; on a slope each one is a little higher or
	// lower than the last, touching it. k: pieces placed so far.
	private int SnappedRun(WorldScene s, List<Vector2> pts, IReadOnlyList<string> names, int k, List<Placement> outp, Func<int, float>? mask, List<Other>? others)
	{
		if (pts.Count < 2)
		{
			return k;
		}
		Vector2 st = pts[0];
		int seg = 0, sSeg = 0;
		float t0 = 0;
		// Started next to a piece already there (within 1 m): the run continues it, at its level.
		var start = others != null ? NearestSnap(others, st, 1) : null;
		if (start is { } st0)
		{
			st = new Vector2(st0.X, st0.Z);
		}
		int layers = Math.Clamp(Layers, 1, 20);
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		for (; k < MaxShape; k++)
		{
			string name = names[k % names.Count];
			sSeg = seg;
			if (EndsOf(name) is not { } e || AlongRun(pts, st, seg, t0, e.Len) is not var (q, hseg, ht))
			{
				break;
			}
			// Turn so that the piece's start-to-end runs along st → q (Unity yaw, clockwise seen from above).
			float yaw = (MathF.Atan2(q.X - st.X, q.Y - st.Y) - e.Heading) * 180 / MathF.PI;
			float r = yaw * MathF.PI / 180, c = MathF.Cos(r), sn = MathF.Sin(r);
			float px = st.X - (e.A.X * c + e.A.Z * sn), pz = st.Y - (-e.A.X * sn + e.A.Z * c);
			float bottom = Elevation == Elevations.At ? Elev : start is { } sv && Elevation == Elevations.Ground ? sv.Y
				: MathF.Min(MathF.Min(HeightAt(s, st), HeightAt(s, q)), HeightAt(s, (st + q) / 2)) + (Elevation == Elevations.Above ? Elev : 0);
			if (px >= 1 && pz >= 1 && px <= s.W - 2 && pz <= s.H - 2 && (mask == null || mask((int)MathF.Round(pz) * s.W + (int)MathF.Round(px)) > 0))
			{
				for (int l = 0; l < (e.H > 0.1f ? layers : 1); l++)
				{
					outp.Add(new Placement(name, new Vector3(ox + px, bottom - e.A.Y + l * e.H, oz + pz), new Vector3(0, (yaw % 360 + 360) % 360, 0), 0, new Vector2(px, pz)));
				}
			}
			st = q;
			seg = hseg;
			t0 = ht;
		}
		for (int i = sSeg; i < pts.Count - 1; i++)
		{
			SnapGap += Vector2.Distance(i == sSeg ? st : pts[i], pts[i + 1]);
		}
		return k;
	}

	private (float Key, List<Vector2> Pts)? _scatter;
	// Random spots inside the zone (Density, Spacing), kept while the zone stays the same (R: new ones).
	private List<Vector2> ZoneSpots()
	{
		List<Vector2> poly;
		lock (Points)
		{
			poly = Points.ToList();
		}
		float key = poly.Sum(p => p.X * 31 + p.Y * 17) + Density * 1000 + Spacing * 7;
		if (_scatter is { } sc && sc.Key == key)
		{
			return sc.Pts;
		}
		float x0 = poly.Min(p => p.X), x1 = poly.Max(p => p.X), z0 = poly.Min(p => p.Y), z1 = poly.Max(p => p.Y);
		int n = Math.Min(MaxShape, (int)MathF.Round(Density / 100 * AreaTool.Area(poly)));
		var pts = new List<Vector2>();
		var h = new Hash(MathF.Max(0.5f, Spacing));
		for (int tries = 0; pts.Count < n && tries < n * 30; tries++)
		{
			var p = new Vector2(x0 + _rand.NextSingle() * (x1 - x0), z0 + _rand.NextSingle() * (z1 - z0));
			if (!AreaTool.Inside(poly, p.X, p.Y) || !h.Free(p, Spacing))
			{
				continue;
			}
			h.Add(p);
			pts.Add(p);
		}
		_scatter = (key, pts);
		return pts;
	}

	private List<Placement> ShapePlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, List<Other>? others)
	{
		var outp = new List<Placement>();
		SnapGap = 0;
		if (Mode == Modes.Zone)
		{
			if (Points.Count < 3)
			{
				return outp;
			}
			var spots = ZoneSpots();
			for (int i = 0; i < spots.Count; i++)
			{
				if (PlacementAt(s, Xf(spots[i]), DrawAt(i), names, hash, mask, Spacing) is { } o)
				{
					outp.Add(o);
				}
			}
			return outp;
		}
		if (Mode == Modes.Line && EndToEnd)
		{
			var kinds = names.Where(n => EndsOf(n) != null).ToList();
			if (kinds.Count == 0)
			{
				return outp;
			}
			int k = 0;
			foreach (var run in LineRuns())
			{
				k = SnappedRun(s, run, kinds, k, outp, mask, SnapTo ? others : null);
			}
			return outp;
		}
		if (Mode == Modes.Line)
		{
			var c = LineRuns().SelectMany(r => r).ToList();
			if (c.Count < 2)
			{
				return outp;
			}
			float walked = 0, next = 0;
			int i = 0;
			for (int sgi = 1; sgi < c.Count; sgi++)
			{
				Vector2 a = c[sgi - 1], b = c[sgi];
				float len = Vector2.Distance(a, b);
				while (next <= walked + len + 1e-6f)
				{
					float t = len > 0 ? (next - walked) / len : 0, dx = (b.X - a.X) / (len == 0 ? 1 : len), dz = (b.Y - a.Y) / (len == 0 ? 1 : len);
					var d = DrawAt(i);
					float side = (d.W - 0.5f) * 2 * Wiggle;
					var g = new Vector2(a.X + (b.X - a.X) * t - dz * side, a.Y + (b.Y - a.Y) * t + dx * side);
					// Unity yaw: clockwise from north (+z): the heading of (dx, dz) is atan2(dx, dz). A kind with
					// a known length turns its length along the line; others face along it.
					var e = EndsOf(PickName(names, d.T));
					float? yaw = Along || LineShape != LineShapes.Points ? (MathF.Atan2(dx, dz) - (e?.Heading ?? 0)) * 180 / MathF.PI : null;
					if (PlacementAt(s, g, d, names, hash, mask, 0.3f, yaw) is { } o)
					{
						outp.Add(o);
					}
					i++;
					next += MathF.Max(0.1f, Every);
					if (i >= MaxShape)
					{
						return outp;
					}
				}
				walked += len;
			}
			return outp;
		}
		if (Mode == Modes.Grid && GridA is { } ga && GridB is { } gb)
		{
			float x0 = MathF.Min(ga.X, gb.X), x1 = MathF.Max(ga.X, gb.X), z0 = MathF.Min(ga.Y, gb.Y), z1 = MathF.Max(ga.Y, gb.Y);
			// As many whole cells as fit best (at least one each way), centred in the box.
			int nx = Math.Max(1, (int)MathF.Round((x1 - x0) / Cell)), nz = Math.Max(1, (int)MathF.Round((z1 - z0) / Cell));
			float ox = (x0 + x1) / 2 - nx * Cell / 2, oz = (z0 + z1) / 2 - nz * Cell / 2;
			int i = 0;
			for (int iz = 0; iz < nz; iz++)
			{
				for (int ix = 0; ix < nx; ix++)
				{
					if (i >= MaxShape)
					{
						return outp;
					}
					if (PlacementAt(s, Xf(new Vector2(ox + (ix + 0.5f) * Cell, oz + (iz + 0.5f) * Cell)), DrawAt(i++), names, hash, mask, 0.3f) is { } o)
					{
						outp.Add(o);
					}
				}
			}
		}
		return outp;
	}

	// ---- Snapping a single piece to the pieces already there: beside one (end to end, at its level)
	// or on top of the one under the cursor. Its turn follows that piece (in quarter turns).
	public string? SnappedTo { get; private set; }
	private float AlignYaw(float reference) => reference + 90 * MathF.Round(Rotation / 90);

	private Placement SnapSingle(WorldScene s, Placement o, List<Other> others, int? under)
	{
		SnappedTo = null;
		var sp = SnapsOf(o.Name).Select(p => new Vector3(p[0], p[1], p[2])).ToList();
		var near = others.Where(x => MathF.Abs(x.G.X - o.G.X) < 12 && MathF.Abs(x.G.Y - o.G.Y) < 12).ToList();
		if (near.Count == 0)
		{
			return o;
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		Placement Place(Vector2 g, float y, float ry) => o with { G = g, Position = new Vector3(ox + g.X, y, oz + g.Y), Rotation = new Vector3(0, (ry % 360 + 360) % 360, 0), Scale = 0 };
		if (OnTop)
		{
			var below = near.FirstOrDefault(x => x.Index == under)
				?? near.Where(x => Vector2.Distance(x.G, o.G) < 3).OrderBy(x => Vector2.Distance(x.G, o.G)).FirstOrDefault();
			if (below != null)
			{
				float top = below.Pts.Max(p => p.Y);
				var tops = below.Pts.Where(p => p.Y > top - 0.05f).ToList();
				float ry = AlignYaw(below.Rotation.Y);
				var mine = sp.Select(p => RotU(p, new Vector3(0, ry, 0))).ToList();
				float low = mine.Min(p => p.Y);
				var bottoms = mine.Where(p => p.Y < low + 0.05f).ToList();
				(float D, Vector2 G)? best = null;
				foreach (var t in tops)
				{
					foreach (var m in bottoms)
					{
						var g = new Vector2(t.X - m.X, t.Z - m.Z);
						float d = Vector2.Distance(g, o.G);
						if (best == null || d < best.Value.D)
						{
							best = (d, g);
						}
					}
				}
				SnappedTo = $"on top of {below.Name}";
				return Place(best!.Value.G, top - low, ry);
			}
		}
		// Beside: turned like the nearest piece (the quarter turn that meets best, the facing asked for
		// first), then the closest pair of snap points along the ground (within 1 m) meets.
		var close = near.OrderBy(x => Vector2.Distance(x.G, o.G)).First();
		var yaws = Vector2.Distance(close.G, o.G) < 5 ? new[] { 0f, 180, 90, 270 }.Select(k => AlignYaw(close.Rotation.Y) + k).ToArray() : new[] { o.Rotation.Y };
		(float Score, Vector3 Q, Vector3 M, float Ry, string Name)? bestB = null;
		for (int k = 0; k < yaws.Length; k++)
		{
			var mine = sp.Select(p => RotU(p, new Vector3(0, yaws[k], 0)) + new Vector3(o.G.X, o.Position.Y, o.G.Y)).ToList();
			float myLow = mine.Min(p => p.Y);
			foreach (var x in near)
			{
				foreach (var q in x.Pts)
				{
					foreach (var m in mine)
					{
						float d = MathF.Sqrt((q.X - m.X) * (q.X - m.X) + (q.Z - m.Z) * (q.Z - m.Z));
						if (d > 1)
						{
							continue;
						}
						float score = d + MathF.Abs(q.Y - x.Low - (m.Y - myLow)) + 0.01f * k;
						if (bestB == null || score < bestB.Value.Score)
						{
							bestB = (score, q, m, yaws[k], x.Name);
						}
					}
				}
			}
		}
		if (bestB is not { } bb)
		{
			return o;
		}
		SnappedTo = $"beside {bb.Name}";
		return Place(o.G + new Vector2(bb.Q.X - bb.M.X, bb.Q.Z - bb.M.Z), o.Position.Y + bb.Q.Y - bb.M.Y, bb.Ry);
	}

	// Saplings and crops: the grow radius is kept between the placements themselves too.
	private List<Placement> RoomToGrow(List<Placement> list)
	{
		if (!GrowMatters || !GrowRoom || !list.Any(o => GrowNeed(o.Name) > 0))
		{
			return list;
		}
		var kept = new List<(Vector2 G, float Need)>();
		var outp = new List<Placement>();
		foreach (var o in list)
		{
			float need = GrowNeed(o.Name);
			if (kept.Any(k => Vector2.Distance(k.G, o.G) < MathF.Max(need, k.Need)))
			{
				continue;
			}
			kept.Add((o.G, need));
			outp.Add(o);
		}
		return outp;
	}

	// ---- The preview: what a click (Brush) or Enter (shapes) would place. at: the cursor's grid point;
	// under: the thing under the cursor (for On top).
	public List<Placement> Preview(Vector2? at, int? under = null)
	{
		var s = Scene();
		var names = Chosen.ToList();
		SnappedTo = null;
		if (s == null || names.Count == 0 || Mode == Modes.Brush && at == null)
		{
			return new();
		}
		var mask = Mask();
		var hash = Standing(s, MathF.Max(1, Spacing));
		var others = PiecesChosen && (SnapTo || Mode == Modes.Line) ? OtherSnaps(s, NameOf) : null;
		List<Placement> placed;
		if (Mode != Modes.Brush)
		{
			placed = ShapePlacements(s, names, hash, mask, others);
		}
		else
		{
			if (SettingsKey != _patternKey)
			{
				MakePattern();
			}
			// The layout turns with the rotation (clockwise from above, like the objects' facing).
			float t = RotationNow * MathF.PI / 180, c = MathF.Cos(t), sn = MathF.Sin(t);
			placed = new();
			foreach (var (d, dr) in _pattern)
			{
				var g = new Vector2(at!.Value.X + d.X * c + d.Y * sn, at.Value.Y - d.X * sn + d.Y * c);
				if (PlacementAt(s, g, dr, names, hash, mask) is { } o)
				{
					placed.Add(o);
				}
			}
			if (placed.Count == 1 && SnapTo && others != null && IsPiece(placed[0].Name))
			{
				placed[0] = SnapSingle(s, placed[0], others, under);
			}
		}
		return RoomToGrow(placed);
	}

	// ---- Brush painting: placements a frame of a drag adds (around the cursor), kept apart from what
	// stands and from what this stroke already placed.
	public List<Placement> PaintStep(Vector2 at, float dt, List<Placement> already)
	{
		var s = Scene();
		var names = Chosen.ToList();
		var outp = new List<Placement>();
		if (s == null || names.Count == 0)
		{
			return outp;
		}
		var hash = Standing(s, MathF.Max(1, Spacing));
		foreach (var o in already)
		{
			hash.Add(o.G);
		}
		float r = Brush.Radius, want = Density / 100 * MathF.PI * r * r * MathF.Min(dt, 0.1f) * (0.5f + 2.5f * Brush.Strength);
		int n = (int)MathF.Floor(want) + (_rand.NextSingle() < want % 1 ? 1 : 0);
		var mask = Mask();
		for (int tries = 0; n > 0 && tries < n * 6; tries++)
		{
			float a = _rand.NextSingle() * MathF.Tau, d = MathF.Sqrt(_rand.NextSingle()) * r;
			if (PlacementAt(s, at + new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d), NewDraw(), names, hash, mask) is not { } o)
			{
				continue;
			}
			hash.Add(o.G);
			outp.Add(o);
			n--;
		}
		return outp;
	}

	// Shift + drag: the things of the chosen kinds under the brush (indices).
	public List<int> EraseAt(Vector2 at)
	{
		var s = Scene();
		var outp = new List<int>();
		if (s == null)
		{
			return outp;
		}
		var mask = Mask();
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		var chosen = Chosen.Select(StableHash.Of).ToHashSet();
		lock (s.Things)
		{
			for (int i = 0; i < s.Things.Count; i++)
			{
				var t = s.Things[i];
				var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
				if (t.Gone || !chosen.Contains(t.Prefab) || Vector2.Distance(g, at) > Brush.Radius)
				{
					continue;
				}
				if (mask != null && mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0)
				{
					continue;
				}
				outp.Add(i);
			}
		}
		return outp;
	}

	// Alt + click: one height for all, from the top of the piece clicked (to build on it) or the ground.
	public string PickElevation(int? thing, float groundY)
	{
		var s = Scene();
		float y = groundY;
		string where = " (the ground there)";
		if (s != null && thing is int i && PieceCatalog.Get(s.Things[i].Prefab) is { Snaps.Length: > 0 } info)
		{
			var t = s.Things[i];
			y = info.Snaps.Max(p => RotU(new Vector3(p[0], p[1], p[2]), t.Rotation).Y) + t.Position.Y;
			where = $" (the top of {NameOf(t.Prefab) ?? info.Name})";
		}
		Elevation = Elevations.At;
		Elev = MathF.Round(y, 2);
		Notify();
		return $"Elevation: everything at {Elev:0.00} m{where}. PgUp / PgDn change it.";
	}

	// PgUp / PgDn: up or down; from the ground they start lifting above it.
	public string NudgeElevation(float d)
	{
		if (Elevation == Elevations.Ground)
		{
			Elevation = Elevations.Above;
			Elev = 0;
		}
		Elev = MathF.Round(Elev + d, 2);
		Notify();
		return $"Elevation: {(Elevation == Elevations.At ? "at" : "above the ground by")} {Elev} m.";
	}

	// ---- Presets: kinds with their weights and the scatter settings.
	public sealed record Preset(string Name, Dictionary<string, int> Kinds, float Density, float Spacing, float SizeMin, float SizeMax, float Tilt, float Clump = 0, float Patch = 30);

	public static readonly Preset[] BuiltIn =
	{
		new("Meadows woods", new() { ["Beech1"] = 4, ["Birch1"] = 1, ["Birch2"] = 1, ["Oak1"] = 1, ["Bush01"] = 2 }, 3, 4, 80, 120, 3, 50, 35),
		new("Black forest", new() { ["FirTree"] = 3, ["Pinetree_01"] = 3, ["FirTree_big"] = 1, ["Bush01"] = 1 }, 4, 3.5f, 80, 130, 2, 30, 40),
		new("Swamp", new() { ["SwampTree1"] = 3, ["SwampTree2"] = 1 }, 2, 5, 80, 120, 4),
		new("Berry patch", new() { ["RaspberryBush"] = 2, ["BlueberryBush"] = 1 }, 6, 3, 90, 110, 0, 70, 15),
		new("Forest floor", new() { ["Pickable_Branch"] = 3, ["Pickable_Stone"] = 2, ["Pickable_Mushroom"] = 1, ["Pickable_Dandelion"] = 1 }, 2, 3, 100, 100, 0),
		new("Meadows rocks", new() { ["Rock_3"] = 2, ["Rock_4"] = 1, ["Rock_7"] = 1 }, 1, 6, 60, 140, 10, 40, 25),
	};

	// Loads a preset (kinds the world cannot make are left out). Returns what to say.
	public string Load(Preset p, Func<string, bool> canMake)
	{
		var kinds = p.Kinds.Where(k => canMake(k.Key)).ToList();
		if (kinds.Count == 0)
		{
			return $"None of the kinds of {p.Name} can be placed in this world.";
		}
		Chosen.Clear();
		foreach (var (n, w) in kinds)
		{
			Chosen.Add(n);
			Weights[n] = w;
		}
		(Density, Spacing, SizeMin, SizeMax, Tilt, Clump, Patch, RandomYaw) = (p.Density, p.Spacing, p.SizeMin, p.SizeMax, p.Tilt, p.Clump, p.Patch, true);
		AutoSnap();
		NewLayout();
		int left = p.Kinds.Count - kinds.Count;
		return $"Preset {p.Name}: {string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}"))}{(left > 0 ? $" ({left} kind(s) this world cannot place left out)" : "")}.";
	}
}
