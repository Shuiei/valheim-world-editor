using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The Area tool's selection, like the web editor's (editor/area.js): a box (drag) or a polygon (click
// points; double-click or Enter closes it) of grid points, with a soft edge inwards; and the ground
// actions done inside it. Points are grid points of the block (x east, z north, one per metre).
public sealed class AreaTool
{
	public bool Box { get; set; } = true;
	public float Soft { get; set; } = 3;
	public List<Vector2> Points { get; } = new();
	public bool Closed { get; private set; }
	private bool _dragging;
	public event Action? Changed;
	public void Notify() => Changed?.Invoke();

	// The kinds of objects the object actions work on.
	public HashSet<ObjectKind> Kinds { get; } = new() { ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables };

	// The selection's outline, or null while there is none.
	public List<Vector2>? Polygon()
	{
		lock (Points)
		{
			if (Box)
			{
				if (Points.Count < 2)
				{
					return null;
				}
				Vector2 a = Points[0], b = Points[1];
				return new() { a, new(b.X, a.Y), b, new(a.X, b.Y) };
			}
			return Points.Count >= 3 ? Points.ToList() : null;
		}
	}

	public void Clear()
	{
		lock (Points)
		{
			Points.Clear();
		}
		Closed = false;
		_dragging = false;
		Notify();
	}

	// ---- The mouse and keys.
	public void Down(Vector2? hit, int clicks)
	{
		if (hit is not { } h)
		{
			return;
		}
		if (Box)
		{
			lock (Points)
			{
				Points.Clear();
				Points.Add(h);
				Points.Add(h);
			}
			_dragging = true;
		}
		else if (clicks >= 2 && Points.Count >= 3)
		{
			// The double-click's first click already added its point.
			Closed = true;
		}
		else
		{
			lock (Points)
			{
				if (Closed)
				{
					Points.Clear();
					Closed = false;
				}
				Points.Add(h);
			}
		}
		Notify();
	}

	public void Moved(Vector2? hit)
	{
		if (_dragging && hit is { } h)
		{
			lock (Points)
			{
				Points[1] = h;
			}
			Notify();
		}
	}

	public void Up()
	{
		if (!_dragging)
		{
			return;
		}
		_dragging = false;
		if (Vector2.Distance(Points[0], Points[1]) < 1)
		{
			Clear();
		}
		else
		{
			Notify();
		}
	}

	// Enter on an open polygon closes it (true); otherwise Enter applies the action.
	public bool CloseWithEnter()
	{
		if (!Box && !Closed && Points.Count >= 3)
		{
			Closed = true;
			Notify();
			return true;
		}
		return false;
	}

	public void RemoveLast()
	{
		if (!Box && Points.Count > 0)
		{
			lock (Points)
			{
				Points.RemoveAt(Points.Count - 1);
			}
			Notify();
		}
	}

	// ---- Geometry.
	public static bool Inside(List<Vector2> poly, float x, float z) => SelectTool.Inside(poly, x, z);

	public static float EdgeDistance(List<Vector2> poly, float x, float z)
	{
		float best = float.MaxValue;
		for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
		{
			Vector2 a = poly[j], d = poly[i] - a;
			float l2 = MathF.Max(d.LengthSquared(), 1e-9f);
			float t = Math.Clamp(((x - a.X) * d.X + (z - a.Y) * d.Y) / l2, 0, 1);
			best = MathF.Min(best, Vector2.Distance(new Vector2(x, z), a + d * t));
		}
		return best;
	}

	public static float Area(List<Vector2> poly)
	{
		float a = 0;
		for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
		{
			a += (poly[j].X + poly[i].X) * (poly[j].Y - poly[i].Y);
		}
		return MathF.Abs(a / 2);
	}

	public sealed record Weights(List<Vector2> Poly, int X0, int Z0, int X1, int Z1, List<(int G, float W)> Cells);

	// The grid points inside the selection and their weights (the soft edge going inwards).
	// mask: also times the Mask (null: not).
	public Weights? WeightsIn(int w, int h, Func<int, float>? mask = null)
	{
		if (Polygon() is not { } poly)
		{
			return null;
		}
		int x0 = Math.Max(0, (int)MathF.Floor(poly.Min(p => p.X)) - 1), x1 = Math.Min(w - 1, (int)MathF.Ceiling(poly.Max(p => p.X)) + 1);
		int z0 = Math.Max(0, (int)MathF.Floor(poly.Min(p => p.Y)) - 1), z1 = Math.Min(h - 1, (int)MathF.Ceiling(poly.Max(p => p.Y)) + 1);
		var cells = new List<(int, float)>();
		for (int gz = z0; gz <= z1; gz++)
		{
			for (int gx = x0; gx <= x1; gx++)
			{
				bool inPoly = Inside(poly, gx, gz);
				float wt;
				if (Soft > 0)
				{
					if (!inPoly)
					{
						continue;
					}
					float t = MathF.Min(1, EdgeDistance(poly, gx, gz) / Soft);
					wt = t * t * (3 - 2 * t);
				}
				else
				{
					// No soft edge: the points the outline crosses (within half a metre) get a share, so the
					// edge follows the outline straight instead of stepping from point to point.
					float d = EdgeDistance(poly, gx, gz);
					wt = inPoly ? MathF.Min(1, 0.5f + d) : 0.5f - d;
				}
				wt *= mask?.Invoke(gz * w + gx) ?? 1;
				if (wt > 0)
				{
					cells.Add((gz * w + gx, wt));
				}
			}
		}
		return new Weights(poly, x0, z0, x1, z1, cells);
	}

	// ---- Ground actions.
	public enum GroundAction { Flatten, Raise, Lower, Smooth, Natural, Restore, Erode, Paint }

	public static string Label(GroundAction a) => a switch
	{
		GroundAction.Natural => "Naturalize",
		GroundAction.Restore => "Restore the ground",
		_ => a.ToString(),
	};

	// The action inside the selection. height: Flatten's; amount: Raise and Lower's; paint: the colour.
	public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect) Apply(Ground g, Brush b, GroundAction act, float height, float amount, float[] paint, Func<int, float>? mask = null)
	{
		var touched = new List<int>();
		if (WeightsIn(g.W, g.H, mask) is not { } a)
		{
			return (touched, (0, 0, 0, 0));
		}
		if (act == GroundAction.Erode)
		{
			var set = new HashSet<int>();
			var box = Erosion.Area(g, a, b.RestAngle, b.Random, set);
			return (set.ToList(), (box.X0 - 1, box.Z0 - 1, box.X1 + 1, box.Z1 + 1));
		}
		var before = new float[g.W * g.H];
		for (int i = 0; i < before.Length; i++)
		{
			before[i] = g.HeightOf(i);
		}
		float Blur(int p, int r)
		{
			int gx = p % g.W, gz = p / g.W, n = 0;
			float s = 0;
			for (int dz = -r; dz <= r; dz++)
			{
				for (int dx = -r; dx <= r; dx++)
				{
					int x = gx + dx, z = gz + dz;
					if (x >= 0 && z >= 0 && x < g.W && z < g.H)
					{
						s += before[z * g.W + x];
						n++;
					}
				}
			}
			return s / n;
		}
		int rNat = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
		float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		foreach (var (p, w) in a.Cells)
		{
			int gx = p % g.W, gz = p / g.W;
			if (g.Locked(gx, gz))
			{
				continue;
			}
			float h = g.HeightOf(p);
			touched.Add(p);
			switch (act)
			{
				case GroundAction.Flatten:
					g.SetHeight(p, h + (height - h) * w);
					break;
				case GroundAction.Raise:
					g.SetHeight(p, h + amount * w);
					break;
				case GroundAction.Lower:
					g.SetHeight(p, h - amount * w);
					break;
				case GroundAction.Smooth:
					g.SetHeight(p, h + (Blur(p, 3) - h) * w);
					break;
				case GroundAction.Natural:
					g.SetHeight(p, h + (Blur(p, rNat) + b.NoiseAmp * b.Noise.Fbm((ox + gx) / b.NoiseSize, (oz + gz) / b.NoiseSize) - h) * w);
					break;
				case GroundAction.Restore:
					if (g.Mod[p] != 0)
					{
						g.Level[p] *= 1 - w;
						g.Smooth[p] *= 1 - w;
						if (w > 0.98f || MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
						{
							g.Level[p] = g.Smooth[p] = 0;
							g.Mod[p] = 0;
						}
					}
					// Also back down from a No limit lift (a mountain, a deep paste), as the Restore brush.
					g.Lift[p] = w > 0.98f || MathF.Abs(g.Lift[p] * (1 - w)) < 0.01f ? 0 : g.Lift[p] * (1 - w);
					if (g.PMod[p] != 0 && w > 0.5f)
					{
						g.PMod[p] = 0;
					}
					break;
				case GroundAction.Paint:
					if (g.PMod[p] == 0)
					{
						g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
						g.Paint[p * 4 + 3] = 1;
						g.PMod[p] = 1;
					}
					for (int c = 0; c < 4; c++)
					{
						g.Paint[p * 4 + c] += (paint[c] - g.Paint[p * 4 + c]) * w;
					}
					break;
			}
		}
		return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1));
	}

	// Cut and fill (each point stands for 1 m²): what Flatten, Raise or Lower would move within the
	// game's ±8 m limit (and what is out of its reach), and how much the ground inside has been raised
	// and dug since it was generated.
	public string Volume(Ground g, GroundAction act, float height, float amount, Func<int, float>? mask = null)
	{
		if (WeightsIn(g.W, g.H, mask) is not { } a)
		{
			return "";
		}
		static string M3(double v) => v >= 1000 ? $"{v / 1000:0.0}k m³" : $"{Math.Round(v)} m³";
		double up = 0, down = 0, cut = 0, fill = 0, outOfReach = 0, raise = 0, lower = 0;
		foreach (var (p, w) in a.Cells)
		{
			if (g.Locked(p % g.W, p / g.W))
			{
				continue;
			}
			float h = g.HeightOf(p), b = g.Original(p), d = (h - b) * w;
			if (d > 0) up += d; else down -= d;
			float want = h + (height - h) * w, got = Math.Clamp(want, b - 8, b + 8);
			if (got > h) fill += got - h; else cut += h - got;
			outOfReach += MathF.Abs(want - got);
			raise += MathF.Max(0, MathF.Min(b + 8, h + amount * w) - h);
			lower += MathF.Max(0, h - MathF.Max(b - 8, h - amount * w));
		}
		string first = act switch
		{
			GroundAction.Flatten => $"Flatten to {height:0.#} m: dig {M3(cut)}, fill {M3(fill)}{(outOfReach >= 1 ? $" ({M3(outOfReach)} out of reach: ±8 m limit)" : "")}.",
			GroundAction.Raise => $"Raise {amount:0.#} m: fill {M3(raise)}.",
			GroundAction.Lower => $"Lower {amount:0.#} m: dig {M3(lower)}.",
			_ => "",
		};
		return (first == "" ? "" : first + "\n") + $"Ground inside: {M3(up)} raised, {M3(down)} dug since generated.";
	}

	// The average height of the ground inside (for Flatten's "avg").
	public float? Average(Ground g)
	{
		if (WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
		{
			return null;
		}
		return a.Cells.Average(c => g.HeightOf(c.G));
	}

	// The zones under the selection (world zone coordinates); a point just outside the outline does not count.
	public List<(int X, int Z)> ZonesUnder(Ground g)
	{
		var set = new SortedSet<(int, int)>();
		if (WeightsIn(g.W, g.H) is not { } a)
		{
			return new();
		}
		foreach (var (p, w) in a.Cells)
		{
			if (w < 0.5f)
			{
				continue;
			}
			int zx = Math.Min(g.Size - 1, p % g.W / 64), zz = Math.Min(g.Size - 1, p / g.W / 64);
			set.Add((g.X0 + zx, g.Z0 + zz));
		}
		return set.ToList();
	}
}
