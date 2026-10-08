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
		new("Lone peak", MountainKind.Peak, (60, 160), (70, 150), (0.4f, 0.8f), "One mountain rising to a peak, with ridges down its sides."),
		new("Ridge", MountainKind.Ridge, (40, 110), (90, 200), (0.4f, 0.8f), "A long, narrow crest (Turn sets its direction)."),
		new("Mountain range", MountainKind.Range, (60, 150), (140, 260), (0.4f, 0.9f), "Several peaks along a line (Turn sets its direction)."),
		new("Mesa", MountainKind.Mesa, (25, 70), (50, 130), (0.2f, 0.6f), "A flat top with steep sides: room to build up there."),
		new("Volcano", MountainKind.Volcano, (60, 140), (80, 170), (0.3f, 0.7f), "A cone with a crater at the top."),
		new("Rolling hills", MountainKind.Hills, (15, 45), (90, 220), (0.3f, 0.7f), "A few gentle hills together."),
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
				bumps.Add((MathF.Cos(ang) * dist, MathF.Sin(ang) * dist, 0.3f + (float)rnd.NextDouble() * 0.2f, 0.45f + (float)rnd.NextDouble() * 0.55f));
			}
		}
		else if (m.Kind == MountainKind.Range)
		{
			int n = 3 + rnd.Next(3);
			for (int i = 0; i < n; i++)
			{
				float t = n == 1 ? 0 : -0.62f + 1.24f * i / (n - 1) + ((float)rnd.NextDouble() - 0.5f) * 0.12f;
				bumps.Add((t, ((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.24f + (float)rnd.NextDouble() * 0.12f, 0.55f + (float)rnd.NextDouble() * 0.45f));
			}
		}
		static float Smooth(float t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
		static float Bell(float d) => d >= 1 ? 0 : (1 - d * d) * (1 - d * d);
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
				MountainKind.Peak => MathF.Pow(Bell(d), 1.3f),
				MountainKind.Ridge => MathF.Pow(Bell(MathF.Sqrt(u * u + (v / 0.32f) * (v / 0.32f))), 1.15f),
				MountainKind.Mesa => Smooth((1 - d) / 0.28f),
				MountainKind.Volcano => MathF.Pow(Bell(d), 1.2f) - 0.55f * Bell(d / 0.24f),
				_ => bumps.Aggregate(0f, (acc, b) =>
				{
					float bu = (u - b.X) / b.R, bv = (v - b.Z) / b.R;
					return MathF.Max(acc, b.H * MathF.Pow(Bell(MathF.Sqrt(bu * bu + bv * bv)), 1.2f));
				}),
			};
			if (body <= 0)
			{
				return 0;
			}
			// Detail: broad swells, and ridges with gullies between them (ridged noise), stronger higher up.
			float swell = noise.Fbm(x / (R * 0.35f) + 11.3f, z / (R * 0.35f) - 4.2f);
			float ridged = 1 - MathF.Abs(noise.Fbm(x / (R * 0.16f) - 2.6f, z / (R * 0.16f) + 8.4f));
			float detail = m.Rough * (0.25f * swell + 0.35f * (ridged - 0.55f) * MathF.Sqrt(body));
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
