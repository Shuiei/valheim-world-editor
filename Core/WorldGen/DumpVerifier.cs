using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

// Compares the offline generator with ground truth recorded in the real game by the TerrainDump
// client plugin (BepInEx/TerrainDump.txt). Every value is compared bit-for-bit.
public static class DumpVerifier
{
	// What was compared and how much matched; Errors: the first mismatches, described.
	public sealed record Result(int RandomOk, int RandomWrong, int PerlinOk, int PerlinWrong, int HeightsOk, int HeightsWrong, int BiomesWrong, int ZonesOk, int ZonesWrong, int WaterWrong, List<string> Errors)
	{
		// The largest differences in metres (heights, and heights inside zones).
		public double HeightMaxDiff { get; init; }
		public double ZoneMaxDiff { get; init; }

		public bool AllMatch => RandomWrong + PerlinWrong + HeightsWrong + BiomesWrong + ZonesWrong + WaterWrong == 0;
	}

	private static float F(string hex) => BitConverter.Int32BitsToSingle(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

	private static string State()
	{
		var s = Rnd.state;
		return $"{s.s0:x8} {s.s1:x8} {s.s2:x8} {s.s3:x8}";
	}

	// (Unity's Random is the whole program's: held for the whole check, which draws from it line by line.)
	public static Result Run(string path, string seedName)
	{
		lock (Rnd.Lock)
		{
			return RunLocked(path, seedName);
		}
	}

	private static Result RunLocked(string path, string seedName)
	{
		string[] lines = File.ReadAllLines(path);
		int rOk = 0, rBad = 0, pOk = 0, pBad = 0, hOk = 0, hBad = 0, bBad = 0, zOk = 0, zBad = 0, wBad = 0;
		double pMax = 0, hMax = 0, zMax = 0;
		List<string> firstErrors = new();
		void Error(string msg)
		{
			if (firstErrors.Count < 25)
			{
				firstErrors.Add(msg);
			}
		}
		WorldGenerator? gen = null;
		foreach (string line in lines)
		{
			string[] p = line.Split(' ');
			switch (p[0])
			{
				case "R":
				{
					int bar = Array.IndexOf(p, "|");
					string expectedState = string.Join(" ", p.Skip(bar + 1));
					string got;
					string expected = string.Join(" ", p.Skip(1).Take(bar - 1));
					if (p[1] == "init")
					{
						Rnd.InitState(int.Parse(p[2], CultureInfo.InvariantCulture));
						got = expected;
					}
					else if (p[1] == "rangei")
					{
						got = $"rangei {p[2]} {p[3]} {Rnd.Range(int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture)}";
					}
					else if (p[1] == "rangef")
					{
						got = $"rangef {p[2]} {p[3]} {BitConverter.SingleToInt32Bits(Rnd.Range(float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture))):x8}";
					}
					else if (p[1] == "value")
					{
						got = $"value {BitConverter.SingleToInt32Bits(Rnd.value):x8}";
					}
					else
					{
						Vector2 v = Rnd.insideUnitCircle;
						got = $"circle {BitConverter.SingleToInt32Bits(v.x):x8} {BitConverter.SingleToInt32Bits(v.y):x8}";
					}
					string gotState = State();
					if (got == expected && gotState == expectedState)
					{
						rOk++;
					}
					else
					{
						rBad++;
						Error($"Random: expected [{expected} | {expectedState}] got [{got} | {gotState}]");
					}
					break;
				}
				case "P":
				{
					float x = F(p[1]), y = F(p[2]), want = F(p[3]);
					float got = UnityPerlin.Noise(x, y);
					if (BitConverter.SingleToInt32Bits(got) == BitConverter.SingleToInt32Bits(want))
					{
						pOk++;
					}
					else
					{
						pBad++;
						pMax = Math.Max(pMax, Math.Abs(got - want));
						Error($"Perlin({x}, {y}): expected {want:R} got {got:R}");
					}
					break;
				}
				case "W":
				{
					gen ??= Init(seedName);
					string offline = $"W lakes {gen.GetLakes().Count} rivers {gen.GetRivers().Count} streams {gen.GetStreams().Count}";
					Console.WriteLine($"  game:    {line}");
					Console.WriteLine($"  offline: {offline}");
					if (offline != line.Trim())
					{
						wBad++;
						Error($"Water: expected [{line.Trim()}] got [{offline}]");
					}
					break;
				}
				case "H":
				{
					gen ??= Init(seedName);
					float x = F(p[1]), z = F(p[2]), want = F(p[4]);
					var biome = gen.GetBiome(x, z);
					float got = gen.GetBiomeHeight(biome, x, z, out _);
					if ((int)biome != int.Parse(p[3], CultureInfo.InvariantCulture))
					{
						bBad++;
						Error($"Biome at ({x}, {z}): expected {(Heightmap.Biome)int.Parse(p[3], CultureInfo.InvariantCulture)} got {biome}");
					}
					if (BitConverter.SingleToInt32Bits(got) == BitConverter.SingleToInt32Bits(want))
					{
						hOk++;
					}
					else
					{
						hBad++;
						hMax = Math.Max(hMax, Math.Abs(got - want));
						Error($"Height at ({x}, {z}): expected {want:R} got {got:R}");
					}
					break;
				}
				case "Z":
				{
					gen ??= Init(seedName);
					int zx = int.Parse(p[1], CultureInfo.InvariantCulture), zz = int.Parse(p[2], CultureInfo.InvariantCulture);
					float[] got = BaseTerrain.BuildZone(gen, zx, zz);
					int bad = 0;
					for (int i = 0; i < got.Length; i++)
					{
						float want = F(p[3 + i]);
						if (BitConverter.SingleToInt32Bits(got[i]) != BitConverter.SingleToInt32Bits(want))
						{
							bad++;
							zMax = Math.Max(zMax, Math.Abs(got[i] - want));
						}
					}
					if (bad == 0)
					{
						zOk++;
					}
					else
					{
						zBad++;
						Error($"Zone ({zx}, {zz}): {bad} of {got.Length} heights differ");
					}
					break;
				}
			}
		}
		Console.WriteLine($"Random:  {rOk} ok, {rBad} wrong");
		Console.WriteLine($"Perlin:  {pOk} ok, {pBad} wrong (max diff {pMax:G4})");
		Console.WriteLine($"Heights: {hOk} ok, {hBad} wrong (max diff {hMax:G4} m), biome mismatches {bBad}");
		Console.WriteLine($"Zones:   {zOk} exact, {zBad} differ (max diff {zMax:G4} m)");
		foreach (string e in firstErrors)
		{
			Console.WriteLine("  " + e);
		}
		return new Result(rOk, rBad, pOk, pBad, hOk, hBad, bBad, zOk, zBad, wBad, firstErrors) { HeightMaxDiff = hMax, ZoneMaxDiff = zMax };
	}

	private static WorldGenerator Init(string seedName)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		World world = new() { m_seedName = seedName, m_seed = TerrainEditor.Save.StableHash.Of(seedName), m_worldGenVersion = 2 };
		WorldGenerator.Initialize(world);
		Console.WriteLine($"  offline worldgen init {watch.ElapsedMilliseconds} ms, seed {world.m_seed}");
		return WorldGenerator.instance;
	}
}

// Base (unmodified) terrain heights for one zone, exactly as HeightmapBuilder.Build computes them.
public static class BaseTerrain
{
	public const int Width = 64;

	public static float[] BuildZone(WorldGenerator gen, int zx, int zz) => BuildZone(gen, zx, zz, null);

	// mask: optional 65x65 RGBA base paint mask (Heightmap m_baseMask), as GetBiomeHeight reports it.
	public static float[] BuildZone(WorldGenerator gen, int zx, int zz, float[]? mask)
	{
		const float scale = 1f;
		int num = Width + 1;
		float[] heights = new float[num * num];
		float vx = zx * 64f + Width * scale * -0.5f;
		float vz = zz * 64f + Width * scale * -0.5f;
		float far = (float)((double)Width * scale);
		Heightmap.Biome b1 = gen.GetBiome(vx, vz);
		Heightmap.Biome b2 = gen.GetBiome((float)((double)vx + far), vz);
		Heightmap.Biome b3 = gen.GetBiome(vx, (float)((double)vz + far));
		Heightmap.Biome b4 = gen.GetBiome((float)((double)vx + far), (float)((double)vz + far));
		for (int k = 0; k < num; k++)
		{
			float wy = (float)((double)vz + k * (double)scale);
			float t = DUtils.SmoothStep(0f, 1f, (float)((double)k / Width));
			for (int l = 0; l < num; l++)
			{
				float wx = (float)((double)vx + l * (double)scale);
				float t2 = DUtils.SmoothStep(0f, 1f, (float)((double)l / Width));
				float h;
				Color m;
				if (b3 == b1 && b2 == b1 && b4 == b1)
				{
					h = gen.GetBiomeHeight(b1, wx, wy, out m);
				}
				else
				{
					float h1 = gen.GetBiomeHeight(b1, wx, wy, out Color m1);
					float h2 = gen.GetBiomeHeight(b2, wx, wy, out Color m2);
					float h3 = gen.GetBiomeHeight(b3, wx, wy, out Color m3);
					float h4 = gen.GetBiomeHeight(b4, wx, wy, out Color m4);
					h = DUtils.Lerp(DUtils.Lerp(h1, h2, t2), DUtils.Lerp(h3, h4, t2), t);
					m = Lerp(Lerp(m1, m2, t2), Lerp(m3, m4, t2), t);
				}
				heights[k * num + l] = h;
				if (mask != null)
				{
					int o = (k * num + l) * 4;
					mask[o] = m.r; mask[o + 1] = m.g; mask[o + 2] = m.b; mask[o + 3] = m.a;
				}
			}
		}
		return heights;
	}

	private static Color Lerp(Color a, Color b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
	}
}
