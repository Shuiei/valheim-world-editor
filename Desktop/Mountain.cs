namespace TerrainEditor.Desktop;

// The Mountain tool's shapes: how many metres a mountain adds to the ground at each point around its
// middle, made from a seed so a mountain can be made again or rolled anew (Randomize). Natural detail
// comes from the editor's fractal noise (Noise): the outline is warped, the slopes get ridges and
// gullies. The ground goes past the game's ±8 m (No limit), so saving turns it into ground discs.
public enum MountainKind { Peak, Ridge, Range, Mesa, Volcano, Hills }

public sealed record MountainSpec(MountainKind Kind, float Height, float Radius, float Rough, float Turn, int Seed)
{
	// How far from the middle the mountain reaches (its outline is warped up to this).
	public float Reach => Radius * 1.15f;
}

public static class Mountain
{
	// The presets: a kind with the ranges Randomize picks its size from.
	public sealed record Preset(string Name, MountainKind Kind, (float Min, float Max) Height, (float Min, float Max) Radius, (float Min, float Max) Rough, string Help);

	public static readonly Preset[] Presets =
	{
		new("Lone peak", MountainKind.Peak, (50, 110), (120, 240), (0.4f, 0.8f), "One mountain rising to a peak, with ridges down its sides."),
		new("Ridge", MountainKind.Ridge, (35, 80), (140, 260), (0.4f, 0.8f), "A long crest (Turn sets its direction)."),
		new("Mountain range", MountainKind.Range, (40, 90), (180, 280), (0.4f, 0.8f), "Several peaks along a line (Turn sets its direction)."),
		new("Mesa", MountainKind.Mesa, (20, 50), (70, 150), (0.2f, 0.6f), "A flat top with steep, rocky sides: room to build up there."),
		new("Volcano", MountainKind.Volcano, (45, 100), (130, 240), (0.3f, 0.7f), "A cone with a crater at the top."),
		new("Rolling hills", MountainKind.Hills, (12, 30), (100, 220), (0.3f, 0.7f), "A few gentle hills together."),
	};

	// A new mountain of the preset: its size picked within the preset's ranges.
	public static MountainSpec Roll(Preset p, Random r) => new(p.Kind, Pick(r, p.Height), Pick(r, p.Radius), Pick(r, p.Rough), r.Next(0, 180), r.Next());

	private static float Pick(Random r, (float Min, float Max) range) => MathF.Round(range.Min + (float)r.NextDouble() * (range.Max - range.Min));

	// The shape's maker for a spec: metres to add at (x, z) metres east and north of the middle.
	public static Func<float, float, float> Shape(MountainSpec m)
	{
		var noise = new Noise(m.Seed);
		var rnd = new Random(m.Seed);
		float a = m.Turn * MathF.PI / 180, ca = MathF.Cos(a), sa = MathF.Sin(a), R = m.Radius, H = m.Height;
		// Hills and ranges: bumps of their own (along the line for a range), picked once from the seed.
		var bumps = new List<(float X, float Z, float R, float H)>();
		if (m.Kind == MountainKind.Hills)
		{
			int n = 3 + rnd.Next(4);
			for (int i = 0; i < n; i++)
			{
				float ang = (float)(rnd.NextDouble() * MathF.Tau), dist = (float)Math.Sqrt(rnd.NextDouble()) * 0.55f;
				bumps.Add((MathF.Cos(ang) * dist, MathF.Sin(ang) * dist, 0.4f + (float)rnd.NextDouble() * 0.2f, 0.45f + (float)rnd.NextDouble() * 0.55f));
			}
		}
		else if (m.Kind == MountainKind.Range)
		{
			int n = 3 + rnd.Next(3);
			for (int i = 0; i < n; i++)
			{
				float t = n == 1 ? 0 : -0.62f + 1.24f * i / (n - 1) + ((float)rnd.NextDouble() - 0.5f) * 0.12f;
				bumps.Add((t, ((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.34f + (float)rnd.NextDouble() * 0.12f, 0.55f + (float)rnd.NextDouble() * 0.45f));
			}
		}
		static float Smooth(float t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
		// A natural profile: steeper up high, easing out at the foot (a gaussian, brought to 0 at the edge).
		static float Bell(float d) => d >= 1 ? 0 : (MathF.Exp(-3.2f * d * d) - 0.0408f) / 0.9592f;
		return (x, z) =>
		{
			// The outline warped by noise, so no mountain is a perfect circle.
			float wx = x + R * 0.18f * noise.Fbm(x / (R * 0.6f) + 3.1f, z / (R * 0.6f) - 7.7f);
			float wz = z + R * 0.18f * noise.Fbm(x / (R * 0.6f) - 5.3f, z / (R * 0.6f) + 1.9f);
			// Turned, in radii: u along the Turn direction, v across.
			float u = (wx * ca + wz * sa) / R, v = (-wx * sa + wz * ca) / R;
			float d = MathF.Sqrt(u * u + v * v);
			float body = m.Kind switch
			{
				MountainKind.Peak => Bell(d),
				MountainKind.Ridge => Bell(MathF.Sqrt(u * u + (v / 0.5f) * (v / 0.5f))),
				MountainKind.Mesa => Smooth((1 - d) / 0.35f),
				MountainKind.Volcano => Bell(d) - 0.45f * Bell(d / 0.22f),
				_ => bumps.Aggregate(0f, (acc, b) =>
				{
					float bu = (u - b.X) / b.R, bv = (v - b.Z) / b.R;
					// A smooth maximum: where two peaks meet, a saddle instead of a crease.
					float h = b.H * Bell(MathF.Sqrt(bu * bu + bv * bv)), k = 0.15f, t = Math.Clamp(0.5f + 0.5f * (h - acc) / k, 0, 1);
					return h <= 0 ? acc : acc <= 0 ? h : acc + (h - acc) * t + k * t * (1 - t);
				}),
			};
			if (body <= 0)
			{
				return 0;
			}
			// Detail: broad swells, then ridges with gullies between them (ridged noise) down the slopes,
			// both in proportion to the mountain, and a little fine detail capped at a few metres (deep,
			// narrow cuts look torn, and the ground discs cannot follow them).
			float swell = noise.Fbm(x / (R * 0.45f) + 11.3f, z / (R * 0.45f) - 4.2f);
			float ridged = 1 - MathF.Abs(noise.Fbm(x / (R * 0.3f) - 2.6f, z / (R * 0.3f) + 8.4f));
			float fine = noise.Fbm(x / 18f + 5.5f, z / 18f - 3.3f) * MathF.Min(0.03f * H, 3f) / H;
			float detail = m.Rough * (0.18f * swell + 0.22f * (ridged - 0.55f) * MathF.Sqrt(body) + fine / MathF.Max(0.05f, body));
			// A mesa keeps its top flat: detail only on its sides.
			if (m.Kind == MountainKind.Mesa)
			{
				detail *= 1 - Smooth((body - 0.85f) / 0.15f);
			}
			// Back to nothing at the edge.
			float edge = 1 - Smooth((d - 0.85f) / 0.3f);
			return H * MathF.Max(0, body + detail * body) * edge;
		};
	}
}
