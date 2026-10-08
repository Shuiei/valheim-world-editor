namespace TerrainEditor.Desktop;

// The sculpt and paint tools, as the web editor applies them every frame while the button is held
// (editor.html applyBrush), with the brush shapes and falloffs of editor/brushes.js.
public enum BrushTool
{
	Raise,
	Lower,
	Flatten,
	Smooth,
	Natural,
	Erode,
	Restore,
	PaintDirt,
	PaintCultivated,
	PaintPaved,
	PaintClear,
}

public enum BrushShape { Circle, Square, Ring, Noise, Stamp }

public enum Falloff { Smooth, Linear, Dome, Flat, Peak, Sharp }

public sealed class Brush
{
	public BrushShape Shape { get; set; } = BrushShape.Circle;
	public Falloff Falloff { get; set; } = Falloff.Smooth;
	// The picture of the Stamp shape (Stamps.Size squared weights), and Stamp once: a click of Raise or
	// Lower puts the whole picture in, this many metres high.
	public float[]? StampData { get; set; }
	public string StampLabel { get; set; } = "";
	public bool StampOnce { get; set; }
	public float StampHeight { get; set; } = 4;
	// Square brushes and stamps turn (degrees).
	public float Turn { get; set; }
	public float Radius { get; set; } = 6;
	public float Strength { get; set; } = 0.4f;
	// Flatten: the height to level to, or the ground's where the stroke starts.
	public float Target { get; set; } = 35;
	public bool TargetFromClick { get; set; } = true;
	// Naturalize: how big the bumps are (m) and how wide.
	public float NoiseAmp { get; set; } = 1.2f;
	public float NoiseSize { get; set; } = 16;
	public Noise Noise { get; set; } = new(Random.Shared.Next());
	// Erode: rain (Water) or sliding to the rest angle (Thermal, degrees).
	public bool ErodeWater { get; set; }
	public float RestAngle { get; set; } = 33;
	public Random Random { get; set; } = new();

	public static string Label(BrushTool t) => t switch
	{
		BrushTool.Raise => "Raise",
		BrushTool.Lower => "Lower",
		BrushTool.Flatten => "Flatten",
		BrushTool.Smooth => "Smooth",
		BrushTool.Natural => "Naturalize",
		BrushTool.Erode => "Erode",
		BrushTool.Restore => "Restore",
		BrushTool.PaintDirt => "Dirt",
		BrushTool.PaintCultivated => "Cultivate",
		BrushTool.PaintPaved => "Paved",
		_ => "Clear paint",
	};

	public static string Help(BrushTool t) => t switch
	{
		BrushTool.Raise => "Lift the ground under the brush.",
		BrushTool.Lower => "Dig the ground down under the brush.",
		BrushTool.Flatten => "Level the ground to one height.",
		BrushTool.Smooth => "Even out bumps and sharp edges.",
		BrushTool.Natural => "Turn flat, tool-made ground into natural-looking bumps.",
		BrushTool.Erode => "Weather the ground: steep slopes slide down to rest (Thermal), or rain cuts gullies and fills hollows (Water).",
		BrushTool.Restore => "Bring the ground back to how the world generated it.",
		BrushTool.PaintDirt => "Paint bare dirt, like a path.",
		BrushTool.PaintCultivated => "Paint cultivated soil for planting.",
		BrushTool.PaintPaved => "Paint paved stone ground.",
		_ => "Remove paint: back to the biome's own ground.",
	};

	// The colour each paint tool moves the paint towards (dirt, cultivated, paved, vegetation).
	public static float[]? PaintOf(BrushTool t) => t switch
	{
		BrushTool.PaintDirt => new[] { 1f, 0, 0, 1 },
		BrushTool.PaintCultivated => new[] { 0f, 1, 0, 1 },
		BrushTool.PaintPaved => new[] { 0f, 0, 1, 1 },
		BrushTool.PaintClear => new[] { 0f, 0, 0, 1 },
		_ => null,
	};

	// t: 1 at the middle of the brush, 0 at its edge.
	public static float Fade(Falloff f, float t) => f switch
	{
		Falloff.Linear => t,
		Falloff.Dome => MathF.Sqrt(MathF.Max(0, 1 - (1 - t) * (1 - t))),
		Falloff.Flat => t >= 0.2f ? 1 : (t / 0.2f) * (t / 0.2f) * (3 - 2 * t / 0.2f),
		Falloff.Peak => t * t * t,
		// Full strength right up to the edge, nothing beyond: steep walls, like the game's pickaxe.
		Falloff.Sharp => t > 0 ? 1 : 0,
		_ => t * t * (3 - 2 * t),
	};

	private bool Turnable => Shape is BrushShape.Square or BrushShape.Stamp;

	// The weight of a point (dx, dz) from the brush middle (0..1). wx, wz: its world position, for the noise.
	public float Weight(float dx, float dz, float wx = 0, float wz = 0)
	{
		float r = Radius, u = dx / r, v = dz / r;
		if (Turnable)
		{
			float a = -Turn * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
			(u, v) = ((dx * c - dz * s) / r, (dx * s + dz * c) / r);
		}
		// A picture has its own falloff.
		if (Shape == BrushShape.Stamp)
		{
			return StampData != null ? Stamps.Sample(StampData, u, v) : 0;
		}
		float d = Shape switch
		{
			BrushShape.Square => MathF.Max(MathF.Abs(u), MathF.Abs(v)),
			BrushShape.Ring => MathF.Abs(MathF.Sqrt(u * u + v * v) - 0.7f) / 0.3f,
			BrushShape.Noise => MathF.Sqrt(u * u + v * v) * (1 + 0.35f * Noise.Fbm(wx / MathF.Max(2, r * 0.45f), wz / MathF.Max(2, r * 0.45f))),
			_ => MathF.Sqrt(u * u + v * v),
		};
		return d >= 1 ? 0 : Fade(Falloff, 1 - d);
	}

	// How far from the middle a point can be affected (a turned square reaches its corners).
	public float Reach => Turnable || Shape == BrushShape.Noise ? Radius * 1.42f : Radius;

	// The outline drawn on the ground: n points around the brush, as offsets from its middle.
	public List<(float X, float Z)> Outline(int n)
	{
		var pts = new List<(float, float)>();
		float a = Turn * MathF.PI / 180;
		for (int i = 0; i < n; i++)
		{
			float t = i / (float)n * MathF.Tau, x = MathF.Cos(t), z = MathF.Sin(t);
			if (Turnable)
			{
				float m = MathF.Max(MathF.Abs(x), MathF.Abs(z));
				x /= m;
				z /= m;
				(x, z) = (x * MathF.Cos(a) - z * MathF.Sin(a), x * MathF.Sin(a) + z * MathF.Cos(a));
			}
			pts.Add((x * Radius, z * Radius));
		}
		return pts;
	}

	// The Ring shape works on a band from 40% of the radius to the edge: its inner edge, or null.
	public List<(float X, float Z)>? InnerOutline(int n) => Shape != BrushShape.Ring ? null
		: Enumerable.Range(0, n).Select(i => (MathF.Cos(i / (float)n * MathF.Tau) * Radius * 0.4f, MathF.Sin(i / (float)n * MathF.Tau) * Radius * 0.4f)).ToList();
}

// One stroke of a brush: from the button going down to it coming up.
public sealed class Stroke
{
	public required BrushTool Tool { get; init; }
	public required Ground.State Start { get; init; }
	public HashSet<int> Touched { get; } = new();
	public Dictionary<int, float> Natural { get; } = new();
	public float? Target { get; set; }
	// The game's ±8 m limit stopped the ground somewhere.
	public bool Clamped { get; set; }

	public float StartHeight(Ground g, int p) => g.Base[p] + Start.Lift[p] + (Start.Mod[p] != 0
		? Math.Clamp(Start.Level[p] + Start.Smooth[p], -TerrainEditor.Editing.EditStore.MaxLevel, TerrainEditor.Editing.EditStore.MaxLevel)
		: 0);
}

public static class Sculpt
{
	// One frame of a stroke with the brush's middle at grid point (cx, cz), dt seconds after the last.
	// mask: how much each point may change (1 everywhere when null). Returns the grid rectangle
	// changed (to redraw), or null.
	public static (int X0, int Z0, int X1, int Z1)? Apply(Ground g, Brush b, Stroke s, float cx, float cz, float dt, Func<int, float>? mask = null)
	{
		float reach = b.Reach;
		int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
		int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
		if (x0 > x1 || z0 > z1)
		{
			return null;
		}
		float rate = b.Strength * MathF.Min(dt, 0.1f);
		float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		// Erosion works on the neighbourhood of every point (Erosion).
		if (s.Tool == BrushTool.Erode)
		{
			float r = b.Radius, er = reach + 2, talus = Erosion.Talus(b.RestAngle);
			bool clamped = false;
			var box = Erosion.Run(g, (int)MathF.Floor(cx - er), (int)MathF.Floor(cz - er), (int)MathF.Ceiling(cx + er), (int)MathF.Ceiling(cz + er),
				(gx, gz, p) => b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1),
				(h, wt, w, d) =>
				{
					if (b.ErodeWater)
					{
						Erosion.Hydraulic(h, wt, w, d, (int)MathF.Ceiling(r * r * rate * 6), 1, b.Random);
					}
					else
					{
						Erosion.Thermal(h, wt, w, d, talus, MathF.Min(1, rate * 12), 2);
					}
				}, s.Touched, ref clamped);
			s.Clamped |= clamped;
			return (box.X0 - 1, box.Z0 - 1, box.X1 + 1, box.Z1 + 1);
		}
		// Smoothing reads the heights from before this frame.
		Dictionary<int, float>? snapshot = null;
		if (s.Tool == BrushTool.Smooth)
		{
			snapshot = new();
			for (int gz = z0 - 1; gz <= z1 + 1; gz++)
			{
				for (int gx = x0 - 1; gx <= x1 + 1; gx++)
				{
					snapshot[gz * g.W + gx] = g.HeightOf(gz * g.W + gx);
				}
			}
		}
		float[]? colour = Brush.PaintOf(s.Tool);
		for (int gz = z0; gz <= z1; gz++)
		{
			for (int gx = x0; gx <= x1; gx++)
			{
				if (g.Locked(gx, gz))
				{
					continue;
				}
				float wb = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz);
				if (wb <= 0)
				{
					continue;
				}
				int p = gz * g.W + gx;
				float w = wb * (mask?.Invoke(p) ?? 1);
				if (w <= 0)
				{
					continue;
				}
				float h = g.HeightOf(p);
				s.Touched.Add(p);
				switch (s.Tool)
				{
					case BrushTool.Raise:
						s.Clamped |= g.SetHeight(p, h + 6 * rate * w);
						break;
					case BrushTool.Lower:
						s.Clamped |= g.SetHeight(p, h - 6 * rate * w);
						break;
					case BrushTool.Flatten:
						s.Clamped |= g.SetHeight(p, h + ((s.Target ?? b.Target) - h) * MathF.Min(1, 8 * rate * w));
						break;
					case BrushTool.Smooth:
					{
						float sum = 0;
						for (int dz = -1; dz <= 1; dz++)
						{
							for (int dx = -1; dx <= 1; dx++)
							{
								sum += snapshot![(gz + dz) * g.W + gx + dx];
							}
						}
						s.Clamped |= g.SetHeight(p, h + (sum / 9 - h) * MathF.Min(1, 10 * rate * w));
						break;
					}
					case BrushTool.Natural:
						s.Clamped |= g.SetHeight(p, h + (NaturalTarget(g, b, s, p) - h) * MathF.Min(1, 4 * rate * w));
						break;
					case BrushTool.Restore:
					{
						float k = MathF.Min(1, 8 * rate * w);
						if (g.Mod[p] != 0)
						{
							g.Level[p] *= 1 - k;
							g.Smooth[p] *= 1 - k;
							if (MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
							{
								g.Level[p] = 0;
								g.Smooth[p] = 0;
								g.Mod[p] = 0;
							}
						}
						// Also back down from a No limit lift.
						g.Lift[p] = MathF.Abs(g.Lift[p] * (1 - k)) < 0.01f ? 0 : g.Lift[p] * (1 - k);
						if (g.PMod[p] != 0 && w > 0.5f)
						{
							g.PMod[p] = 0;
						}
						break;
					}
					default:
					{
						float k = MathF.Min(1, 8 * rate * w);
						if (g.PMod[p] == 0)
						{
							g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
							g.Paint[p * 4 + 3] = 1;
							g.PMod[p] = 1;
						}
						for (int c = 0; c < 4; c++)
						{
							g.Paint[p * 4 + c] += (colour![c] - g.Paint[p * 4 + c]) * k;
						}
						break;
					}
				}
			}
		}
		return (x0 - 1, z0 - 1, x1 + 1, z1 + 1);
	}

	// Stamp once: the ground under the stamp moves by amount (metres) times the picture, all at once.
	public static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) StampOnce(Ground g, Brush b, float cx, float cz, float amount, Func<int, float>? mask)
	{
		var touched = new List<int>();
		bool clamped = false;
		float reach = b.Reach, ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
		int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
		for (int gz = z0; gz <= z1; gz++)
		{
			for (int gx = x0; gx <= x1; gx++)
			{
				if (g.Locked(gx, gz))
				{
					continue;
				}
				int p = gz * g.W + gx;
				float w = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1);
				if (w <= 0)
				{
					continue;
				}
				clamped |= g.SetHeight(p, g.HeightOf(p) + amount * w);
				touched.Add(p);
			}
		}
		return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1), clamped);
	}

	// Naturalize's aim: the ground's broad shape (blurred, from the start of the stroke) plus detail noise,
	// in world coordinates so the pattern is continuous across areas.
	private static float NaturalTarget(Ground g, Brush b, Stroke s, int p)
	{
		if (s.Natural.TryGetValue(p, out float t))
		{
			return t;
		}
		int gx = p % g.W, gz = p / g.W, r = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
		float sum = 0;
		int n = 0;
		for (int dz = -r; dz <= r; dz += 2)
		{
			for (int dx = -r; dx <= r; dx += 2)
			{
				int x = Math.Clamp(gx + dx, 0, g.W - 1), z = Math.Clamp(gz + dz, 0, g.H - 1);
				sum += s.StartHeight(g, z * g.W + x);
				n++;
			}
		}
		float wx = g.X0 * 64 - 32 + gx, wz = g.Z0 * 64 - 32 + gz;
		t = sum / n + b.NoiseAmp * b.Noise.Fbm(wx / b.NoiseSize, wz / b.NoiseSize);
		s.Natural[p] = t;
		return t;
	}
}
