using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// The Path tool, like the web editor's (editor.html, Path tool): draw a line, then apply an action
// along it with a width and a soft edge: flatten to a height, ramp from start to end, raise or lower,
// smooth, dig a river, dig a cave, or paint. The line is kept after applying, so another action can follow.
// Cave: a trench along the line (its floor follows the ground's lie below it, deepest in the middle,
// with an entrance slope at each end), roofed over with the game's boulders where it is deep enough
// (CaveRoof):
// the ground cannot overhang (one height per point), so the roof is rocks, like the game's own caves.
// Points are grid points of the block (x east, z north, one per metre).
public sealed class PathTool
{
	public enum Action { Flatten, Ramp, Raise, Lower, Smooth, River, Cave, PaintDirt, PaintPaved, PaintCultivated, PaintClear }

	// The boulders a cave's roof is made of: the game's big world rocks (they keep a saved size), how wide
	// they are at least (m, unscaled) and how far their lowest point is below their middle (CaveRoof
	// hangs them by their real underside).
	public enum CaveRock { Auto, Forest, Coast, Heath, Mountain }

	public static readonly Dictionary<CaveRock, (string Prefab, float Footprint, float Bottom)> RoofRocks = new()
	{
		[CaveRock.Forest] = ("rock4_forest", 24.2f, 12.9f),
		[CaveRock.Coast] = ("rock4_coast", 24.2f, 12.9f),
		[CaveRock.Heath] = ("rock4_heath", 24.2f, 12.9f),
		[CaveRock.Mountain] = ("rock3_mountain", 12.7f, 8.2f),
	};

	public static string Label(CaveRock r) => r switch
	{
		CaveRock.Auto => "By biome",
		CaveRock.Forest => "Forest boulders",
		CaveRock.Coast => "Coast boulders",
		CaveRock.Heath => "Heath boulders",
		_ => "Mountain rocks",
	};

	// Cave presets: width, soft edge (the walls' slope), depth below the ground and headroom inside.
	public sealed record CavePreset(string Name, float Width, float Soft, float Depth, float Headroom, string Help);

	public static readonly CavePreset[] CavePresets =
	{
		new("Tunnel", 5, 1.5f, 6, 3.5f, "A narrow passage, just high enough to walk through."),
		new("Cave", 8, 2.5f, 7.5f, 4.5f, "A cave to explore or hide a base in."),
		new("Cavern", 14, 3.5f, 8, 5.5f, "A wide hall under the rocks."),
	};

	public static string Label(Action a) => a switch
	{
		Action.Flatten => "Flatten to height",
		Action.Ramp => "Ramp (start → end)",
		Action.Raise => "Raise by",
		Action.Lower => "Lower by",
		Action.Smooth => "Smooth",
		Action.River => "River / canal (water)",
		Action.Cave => "Cave (dig and roof)",
		Action.PaintDirt => "Paint dirt",
		Action.PaintPaved => "Paint paved",
		Action.PaintCultivated => "Paint cultivated",
		_ => "Clear paint",
	};

	public Action Act { get; set; } = Action.Flatten;
	public float Width { get; set; } = 8;
	public float Soft { get; set; } = 4;
	public float Height { get; set; } = 32;
	public float Amount { get; set; } = 2;
	public float Start { get; set; } = 32;
	public float End { get; set; } = 32;
	public float Depth { get; set; } = 2;
	public bool Curved { get; set; } = true;
	// Cave: depth below the ground in the middle, headroom inside, the roof's boulders, and the seed
	// that turns and sizes them.
	public float CaveDepth { get; set; } = 7.5f;
	public float Headroom { get; set; } = 4.5f;
	public CaveRock Rock { get; set; }
	public int Seed { get; set; } = Random.Shared.Next();
	public bool Natural { get; set; }
	// The ramp's ends follow the ground at the line's ends until typed in.
	public bool RampEdited { get; set; }

	// Changed under its own lock: the drawing thread reads it.
	public List<Vector2> Points { get; } = new();
	// Something about the line or the values changed (to redraw and refill the panel).
	public event System.Action? Changed;
	public void Notify() => Changed?.Invoke();

	// The dense line (about 1 m apart): a Catmull-Rom curve through the points, or straight stretches;
	// Seg is the index of the point each stretch starts from.
	public List<(Vector2 P, int Seg)> Curve()
	{
		var p = Points;
		var outp = new List<(Vector2, int)>();
		if (p.Count < 2)
		{
			outp.AddRange(p.Select(q => (q, 0)));
			return outp;
		}
		bool smooth = Curved && p.Count >= 3;
		for (int i = 0; i < p.Count - 1; i++)
		{
			Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Count - 1, i + 2)];
			int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
			for (int k = 0; k < steps; k++)
			{
				float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
				if (!smooth)
				{
					outp.Add((p1 + (p2 - p1) * t, i));
					continue;
				}
				Vector2 Cr(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => 0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
				outp.Add((Cr(p0, p1, p2, p3), i));
			}
		}
		outp.Add((p[^1], p.Count - 2));
		return outp;
	}

	public static float Length(List<(Vector2 P, int Seg)> curve)
	{
		float l = 0;
		for (int i = 1; i < curve.Count; i++)
		{
			l += Vector2.Distance(curve[i].P, curve[i - 1].P);
		}
		return l;
	}

	// A cave along the line: for each point of the dense line, how far along it is, the ground's lie
	// there (the ground averaged over 6 m each way, so bumps do not show in the floor), how deep the cave
	// is (Depth in the middle, an entrance slope at each end) and its floor.
	public sealed record CavePlan(List<(Vector2 P, int Seg)> Curve, float[] Along, float[] Depth, float[] Floor);

	public CavePlan PlanCave(Ground g)
	{
		var curve = Curve();
		int n = curve.Count;
		var along = new float[n];
		for (int i = 1; i < n; i++)
		{
			along[i] = along[i - 1] + Vector2.Distance(curve[i].P, curve[i - 1].P);
		}
		var surface = curve.Select(c => g.HeightOf(Math.Clamp((int)MathF.Round(c.P.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(c.P.X), 0, g.W - 1))).ToArray();
		float total = n > 0 ? along[^1] : 0, entrance = MathF.Max(8, CaveDepth * 2.5f);
		var depth = new float[n];
		var floor = new float[n];
		for (int i = 0; i < n; i++)
		{
			float sum = 0;
			int k = 0;
			for (int j = 0; j < n; j++)
			{
				if (MathF.Abs(along[j] - along[i]) <= 6)
				{
					sum += surface[j];
					k++;
				}
			}
			float t = Math.Clamp(MathF.Min(along[i], total - along[i]) / entrance, 0, 1);
			depth[i] = CaveDepth * t * t * (3 - 2 * t);
			floor[i] = sum / k - depth[i];
		}
		return new CavePlan(curve, along, depth, floor);
	}

	// The action along the line. heightAt: the ground's height at a grid point (for the ramp's ends).
	// Returns the points changed, the rectangle around them, and whether the game's limit stopped some.
	// cave: the cave worked out before the ground changes (Cave; worked out here when not given).
	public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, float water, Func<int, float>? mask = null, CavePlan? cave = null)
	{
		var curve = Curve();
		if (Act == Action.Cave)
		{
			cave ??= PlanCave(g);
		}
		var touched = new List<int>();
		bool clamped = false;
		if (curve.Count < 2)
		{
			return (touched, (0, 0, 0, 0), false);
		}
		float half = Width / 2, soft = Soft, total = Math.Max(Length(curve), 1e-6f);
		// Natural paths can be up to 45% wider and have ragged edges, so look a bit further out.
		float reach = Natural ? half * 1.45f + soft + MathF.Max(soft, 2) * 0.6f * 1.6f : half + soft;
		var cum = new float[curve.Count];
		for (int i = 1; i < curve.Count; i++)
		{
			cum[i] = cum[i - 1] + Vector2.Distance(curve[i].P, curve[i - 1].P);
		}
		int x0 = Math.Max(1, (int)MathF.Floor(curve.Min(c => c.P.X) - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(curve.Max(c => c.P.X) + reach));
		int z0 = Math.Max(1, (int)MathF.Floor(curve.Min(c => c.P.Y) - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(curve.Max(c => c.P.Y) + reach));
		float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		float WorldNoise(float gx, float gz, float scale = 1) => b.Noise.Fbm((ox + gx) / (b.NoiseSize * scale), (oz + gz) / (b.NoiseSize * scale));
		// Smoothing reads the heights from before.
		float[]? before = null;
		if (Act == Action.Smooth)
		{
			before = new float[g.W * g.H];
			for (int i = 0; i < before.Length; i++)
			{
				before[i] = g.HeightOf(i);
			}
		}
		float[]? colour = Act switch
		{
			Action.PaintDirt => Brush.PaintOf(BrushTool.PaintDirt),
			Action.PaintPaved => Brush.PaintOf(BrushTool.PaintPaved),
			Action.PaintCultivated => Brush.PaintOf(BrushTool.PaintCultivated),
			Action.PaintClear => Brush.PaintOf(BrushTool.PaintClear),
			_ => null,
		};
		for (int gz = z0; gz <= z1; gz++)
		{
			for (int gx = x0; gx <= x1; gx++)
			{
				if (g.Locked(gx, gz))
				{
					continue;
				}
				// The nearest point of the line: distance, and how far along it.
				float best = float.MaxValue, along = 0, caveFloor = 0;
				for (int i = 1; i < curve.Count; i++)
				{
					Vector2 a = curve[i - 1].P, d = curve[i].P - a;
					float l2 = d.LengthSquared();
					if (l2 == 0)
					{
						l2 = 1e-9f;
					}
					float t = Math.Clamp(((gx - a.X) * d.X + (gz - a.Y) * d.Y) / l2, 0, 1);
					float dist = Vector2.Distance(new Vector2(gx, gz), a + d * t);
					if (dist < best)
					{
						best = dist;
						along = cum[i - 1] + t * MathF.Sqrt(l2);
						if (cave != null)
						{
							caveFloor = cave.Floor[i - 1] + (cave.Floor[i] - cave.Floor[i - 1]) * t;
						}
					}
				}
				float dEff = best, halfEff = half, bump = 0;
				if (Natural)
				{
					// Width varies along the path, edges are ragged, and the surface gets bumps.
					halfEff = half * (1 + 0.45f * b.Noise.Fbm(along / (b.NoiseSize * 1.5f) + 31.7f, 4.2f));
					dEff = best + MathF.Max(soft, 2) * 0.6f * WorldNoise(gx, gz, 0.6f);
					bump = b.NoiseAmp * WorldNoise(gx + 500, gz - 300);
				}
				// Without a soft edge, the points the edge crosses (within half a metre) get a share, so the
				// edge follows the line straight instead of stepping from point to point.
				if (dEff > halfEff + (soft > 0 ? soft : 0.5f))
				{
					continue;
				}
				float f = soft > 0 ? (dEff <= halfEff ? 1 : 1 - (dEff - halfEff) / soft) : Math.Clamp(halfEff + 0.5f - dEff, 0, 1);
				int p = gz * g.W + gx;
				float w = (soft > 0 ? f * f * (3 - 2 * f) : f) * (mask?.Invoke(p) ?? 1);
				if (w <= 0)
				{
					continue;
				}
				float h = g.HeightOf(p);
				touched.Add(p);
				switch (Act)
				{
					case Action.Raise:
						clamped |= g.SetHeight(p, h + (Amount + bump) * w);
						break;
					case Action.Lower:
						clamped |= g.SetHeight(p, h - (Amount + bump) * w);
						break;
					case Action.Flatten:
						clamped |= g.SetHeight(p, h + (Height + bump - h) * w);
						break;
					case Action.Ramp:
					{
						float target = Start + (End - Start) * (along / total) + bump;
						clamped |= g.SetHeight(p, h + (target - h) * w);
						break;
					}
					case Action.River:
					{
						// A U-shaped bed: Depth below sea level in the middle, at the waterline at the path's
						// width, then the soft edge blends it into the banks. It only digs.
						float r = MathF.Min(dEff, halfEff) / halfEff;
						float bed = water - Depth * MathF.Max(0, 1 - r * r) + bump * 0.3f;
						if (bed < h)
						{
							clamped |= g.SetHeight(p, h + (bed - h) * w);
						}
						break;
					}
					case Action.Cave:
					{
						// The floor across the cave's width; the soft edge makes its walls. It only digs, and keeps
						// within the game's ±8 m unless No limit is on (bumps above the ground's lie would reach it).
						float floor = caveFloor + bump * 0.3f;
						if (!g.NoLimit)
						{
							floor = MathF.Max(floor, g.Original(p) - TerrainEditor.Editing.EditStore.MaxLevel + 0.1f);
						}
						if (floor < h)
						{
							clamped |= g.SetHeight(p, h + (floor - h) * w);
						}
						break;
					}
					case Action.Smooth:
					{
						float sum = 0;
						int n = 0;
						for (int dz = -2; dz <= 2; dz++)
						{
							for (int dx = -2; dx <= 2; dx++)
							{
								// Inside the area on both axes (a flat index would wrap into the next row).
								int x = gx + dx, z = gz + dz;
								if (x >= 0 && x < g.W && z >= 0 && z < g.H)
								{
									sum += before![z * g.W + x];
									n++;
								}
							}
						}
						clamped |= g.SetHeight(p, h + (sum / n - h) * w);
						break;
					}
					default:
						if (g.PMod[p] == 0)
						{
							g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
							g.Paint[p * 4 + 3] = 1;
							g.PMod[p] = 1;
						}
						for (int c = 0; c < 4; c++)
						{
							g.Paint[p * 4 + c] += (colour![c] - g.Paint[p * 4 + c]) * w;
						}
						break;
				}
			}
		}
		return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1), clamped);
	}

	// ---- The mouse: click points along the route, or hold and drag; drag a point to move it, drag the
	// line to add a point there, Ctrl + click a point to remove it; Alt + click picks the height (Alt +
	// Shift: the ramp's end). screen: where a grid point is on the view (null behind the camera).
	private int? _drag;
	private bool _drawing;

	// A drag is drawing the line now (the view then leaves out the cursor's preview).
	public bool Drawing => _drawing;

	public void Down(Vector2? hit, Point at, Func<Vector2, Point?> screen, bool ctrl, bool alt, bool shift, Func<Vector2, float> heightAt)
	{
		if (alt)
		{
			if (hit is { } a)
			{
				float h = MathF.Round(heightAt(a), 1);
				if (Act == Action.Ramp)
				{
					if (shift) End = h; else Start = h;
					RampEdited = true;
				}
				else
				{
					Height = h;
				}
				Notify();
			}
			return;
		}
		int pt = PointAt(at, screen);
		if (pt >= 0)
		{
			if (ctrl)
			{
				lock (Points) { Points.RemoveAt(pt); }
				Notify();
				return;
			}
			_drag = pt;
			return;
		}
		if (hit is not { } h2)
		{
			return;
		}
		int seg = Points.Count >= 2 ? SegmentAt(at, screen) : -1;
		if (seg >= 0)
		{
			lock (Points) { Points.Insert(seg + 1, h2); }
			_drag = seg + 1;
			Notify();
			return;
		}
		lock (Points) { Points.Add(h2); }
		_drawing = true;
		Notify();
	}

	public void Moved(Vector2? hit)
	{
		if (hit is not { } h)
		{
			return;
		}
		if (_drag is int i && i < Points.Count)
		{
			lock (Points) { Points[i] = h; }
			Notify();
		}
		else if (_drawing && (Points.Count == 0 || Vector2.Distance(Points[^1], h) >= 2))
		{
			lock (Points) { Points.Add(h); }
			Notify();
		}
	}

	public void Up()
	{
		_drag = null;
		_drawing = false;
	}

	public void Clear()
	{
		lock (Points) { Points.Clear(); }
		RampEdited = false;
		Notify();
	}

	public void RemoveLast()
	{
		if (Points.Count > 0)
		{
			lock (Points) { Points.RemoveAt(Points.Count - 1); }
			Notify();
		}
	}

	// The point within 10 px of the mouse, or -1.
	public int PointAt(Point at, Func<Vector2, Point?> screen)
	{
		int best = -1;
		double bd = 10;
		for (int i = 0; i < Points.Count; i++)
		{
			if (screen(Points[i]) is { } s && Point.Distance(s, at) < bd)
			{
				bd = Point.Distance(s, at);
				best = i;
			}
		}
		return best;
	}

	// The stretch of the line within 8 px of the mouse: the index of the point it starts from, or -1.
	public int SegmentAt(Point at, Func<Vector2, Point?> screen)
	{
		var dense = Curve();
		int best = -1;
		double bd = 8;
		for (int i = 1; i < dense.Count; i++)
		{
			if (screen(dense[i - 1].P) is not { } a || screen(dense[i].P) is not { } b)
			{
				continue;
			}
			double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
			if (l2 < 1e-9)
			{
				l2 = 1e-9;
			}
			double t = Math.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / l2, 0, 1);
			double d = Point.Distance(at, new Point(a.X + dx * t, a.Y + dy * t));
			if (d < bd)
			{
				bd = d;
				best = dense[i - 1].Seg;
			}
		}
		return best;
	}
}
