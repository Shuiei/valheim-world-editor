using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// The Path tool, like the web editor's (editor.html, Path tool): draw a line, then apply an action
// along it with a width and a soft edge: flatten to a height, ramp from start to end, raise or lower,
// smooth, dig a river, or paint. The line is kept after applying, so another action can follow.
// Points are grid points of the block (x east, z north, one per metre).
public sealed class PathTool
{
	public enum Action { Flatten, Ramp, Raise, Lower, Smooth, River, PaintDirt, PaintPaved, PaintCultivated, PaintClear }

	public static string Label(Action a) => a switch
	{
		Action.Flatten => "Flatten to height",
		Action.Ramp => "Ramp (start → end)",
		Action.Raise => "Raise by",
		Action.Lower => "Lower by",
		Action.Smooth => "Smooth",
		Action.River => "River / canal (water)",
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

	// The action along the line. heightAt: the ground's height at a grid point (for the ramp's ends).
	// Returns the points changed, the rectangle around them, and whether the game's limit stopped some.
	public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, float water, Func<int, float>? mask = null)
	{
		var curve = Curve();
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
				float best = float.MaxValue, along = 0;
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
					case Action.Smooth:
					{
						float sum = 0;
						int n = 0;
						for (int dz = -2; dz <= 2; dz++)
						{
							for (int dx = -2; dx <= 2; dx++)
							{
								int k = (gz + dz) * g.W + gx + dx;
								if (k >= 0 && k < before!.Length)
								{
									sum += before[k];
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
