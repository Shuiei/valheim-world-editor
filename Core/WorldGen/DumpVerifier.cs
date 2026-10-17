using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

/// <summary>
/// Compares the offline generator with ground truth recorded in the real game by the TerrainDump
/// client plugin (BepInEx/TerrainDump.txt). Every value is compared bit-for-bit.
/// </summary>
public static class DumpVerifier
{
    /// <summary>
    /// What was compared and how much matched.
    /// </summary>
    /// <param name="RandomOk">Random calls (value and state after) that matched.</param>
    /// <param name="RandomWrong">Random calls that did not.</param>
    /// <param name="PerlinOk">Perlin noise samples that matched.</param>
    /// <param name="PerlinWrong">Perlin noise samples that did not.</param>
    /// <param name="HeightsOk">Point heights that matched.</param>
    /// <param name="HeightsWrong">Point heights that did not.</param>
    /// <param name="BiomesWrong">Points whose biome did not match.</param>
    /// <param name="ZonesOk">Zones whose every base height matched.</param>
    /// <param name="ZonesWrong">Zones with any height that did not.</param>
    /// <param name="WaterWrong">Lake, river and stream count lines that did not match.</param>
    /// <param name="Errors">The first mismatches (at most 25), described.</param>
    public sealed record Result(int RandomOk, int RandomWrong, int PerlinOk, int PerlinWrong, int HeightsOk, int HeightsWrong, int BiomesWrong, int ZonesOk, int ZonesWrong, int WaterWrong, List<string> Errors)
    {
        /// <summary>
        /// The largest point height difference, metres.
        /// </summary>
        public double HeightMaxDiff { get; init; }
        /// <summary>
        /// The largest height difference inside zones, metres.
        /// </summary>
        public double ZoneMaxDiff { get; init; }

        /// <summary>
        /// Lines of a known kind that could not be read (too short, an unknown Random call, a number that does not
        /// parse); each is also an error.
        /// </summary>
        public int Unreadable { get; init; }

        /// <summary>
        /// Whether nothing was wrong.
        /// </summary>
        public bool AllMatch => RandomWrong + PerlinWrong + HeightsWrong + BiomesWrong + ZonesWrong + WaterWrong + Unreadable == 0;
    }

    /// <summary>
    /// A float from its bits as hex, as the dump writes them.
    /// </summary>
    private static float F(string hex) => BitConverter.Int32BitsToSingle(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    /// <summary>
    /// The random state as the dump writes it (four words in hex).
    /// </summary>
    private static string State()
    {
        var s = Rnd.state;
        return $"{s.s0:x8} {s.s1:x8} {s.s2:x8} {s.s3:x8}";
    }

    /// <summary>
    /// The values between a Random call's name and its "|" (the call's arguments and result), by call; -1 for a call
    /// the verifier does not know.
    /// </summary>
    private static int RandomValues(string call) => call switch
    {
        "init" or "value" => 1,
        "circle" => 2,
        "rangei" or "rangef" => 3,
        _ => -1,
    };

    /// <summary>
    /// The values a line of this kind needs at least, its kind included (0: any line of it can be read).
    /// </summary>
    private static int Needed(string kind) => kind switch
    {
        "P" => 4,
        "H" => 5,
        "Z" => 3 + (BaseTerrain.Width + 1) * (BaseTerrain.Width + 1),
        _ => 0,
    };

    /// <summary>
    /// Replays a dump file and compares each line, printing a summary to the console. Lines: "R" Random calls with
    /// the state after, "P" Perlin samples, "W" lake, river and stream counts, "H" point biome and height, "Z" a
    /// zone's base heights; others are skipped. A line of these kinds that cannot be read (too short, an unknown
    /// Random call) is counted as unreadable and leaves the random state as it was.
    /// </summary>
    /// <param name="path">The dump file.</param>
    /// <param name="seedName">The world's seed name, for the lines that need a generator.</param>
    public static Result Run(string path, string seedName)
    {
        string[] lines = File.ReadAllLines(path);
        int rOk = 0, rBad = 0, pOk = 0, pBad = 0, hOk = 0, hBad = 0, bBad = 0, zOk = 0, zBad = 0, wBad = 0, unreadable = 0;
        double pMax = 0, hMax = 0, zMax = 0;
        List<string> firstErrors = [];
        void Error(string msg)
        {
            if (firstErrors.Count < 25)
            {
                firstErrors.Add(msg);
            }
        }
        WorldGenerator? gen = null;
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            string[] p = line.Split(' ');
            int bar = p[0] == "R" ? Array.IndexOf(p, "|") : -1;
            bool readable = p[0] == "R"
                ? p.Length > 1 && RandomValues(p[1]) is int values and >= 0 && bar == 2 + values && p.Length == bar + 5
                : p.Length >= Needed(p[0]);
            if (!readable)
            {
                unreadable++;
                Error($"Line {n + 1} not read: {line}");
                continue;
            }
            try
            {
                Compare(p, line);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                unreadable++;
                Error($"Line {n + 1} not read ({e.Message}): {line}");
            }
        }

        // Compares one line of the dump with the offline generator.
        void Compare(string[] p, string line)
        {
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
                    else // circle
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
                default:
                    break;
            }
        }
        Console.WriteLine($"Random:  {rOk} ok, {rBad} wrong");
        if (unreadable > 0)
        {
            Console.WriteLine($"Unreadable lines: {unreadable}");
        }
        Console.WriteLine($"Perlin:  {pOk} ok, {pBad} wrong (max diff {pMax:G4})");
        Console.WriteLine($"Heights: {hOk} ok, {hBad} wrong (max diff {hMax:G4} m), biome mismatches {bBad}");
        Console.WriteLine($"Zones:   {zOk} exact, {zBad} differ (max diff {zMax:G4} m)");
        foreach (string e in firstErrors)
        {
            Console.WriteLine("  " + e);
        }
        return new Result(rOk, rBad, pOk, pBad, hOk, hBad, bBad, zOk, zBad, wBad, firstErrors) { HeightMaxDiff = hMax, ZoneMaxDiff = zMax, Unreadable = unreadable };
    }

    /// <summary>
    /// A generator for a seed name (world gen version 2), timed on the console.
    /// </summary>
    private static WorldGenerator Init(string seedName)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        World world = new() { m_seedName = seedName, m_seed = TerrainEditor.Save.StableHash.Of(seedName), m_worldGenVersion = 2 };
        var gen = WorldGenerator.Create(world);
        Console.WriteLine($"  offline worldgen init {watch.ElapsedMilliseconds} ms, seed {world.m_seed}");
        return gen;
    }
}

/// <summary>
/// Base (unmodified) terrain heights for one zone, exactly as HeightmapBuilder.Build computes them.
/// </summary>
public static class BaseTerrain
{
    /// <summary>
    /// Metres per zone side; a zone has Width + 1 vertices per side.
    /// </summary>
    public const int Width = 64;

    /// <summary>
    /// The 65x65 vertex heights of zone (zx, zz), row by row (row is z), starting at the zone's corner (its
    /// centre less 32 m on each axis).
    /// </summary>
    public static float[] BuildZone(WorldGenerator gen, int zx, int zz) => BuildZone(gen, zx, zz, null);

    /// <summary>The corners' blend weight at each vertex along a side: the same for rows and columns.</summary>
    private static readonly float[] Blend = [.. Enumerable.Range(0, Width + 1).Select(i => GenMath.Smooth(0f, 1f, (float)((double)i / Width)))];

    /// <summary>
    /// The same, also filling a base paint mask.
    /// </summary>
    /// <param name="gen">The world's generator.</param>
    /// <param name="zx">Zone x.</param>
    /// <param name="zz">Zone z.</param>
    /// <param name="mask">Optional 65x65 RGBA base paint mask (Heightmap m_baseMask), as GetBiomeHeight reports it,
    /// filled in place (4 floats per vertex).</param>
    public static float[] BuildZone(WorldGenerator gen, int zx, int zz, float[]? mask)
    {
        const float Scale = 1f;
        int num = Width + 1;
        float[] heights = new float[num * num];
        float vx = zx * 64f + Width * Scale * -0.5f;
        float vz = zz * 64f + Width * Scale * -0.5f;
        float far = (float)((double)Width * Scale);
        Heightmap.Biome b1 = gen.GetBiome(vx, vz);
        Heightmap.Biome b2 = gen.GetBiome((float)((double)vx + far), vz);
        Heightmap.Biome b3 = gen.GetBiome(vx, (float)((double)vz + far));
        Heightmap.Biome b4 = gen.GetBiome((float)((double)vx + far), (float)((double)vz + far));
        for (int k = 0; k < num; k++)
        {
            float wy = (float)((double)vz + k * (double)Scale);
            float t = Blend[k];
            for (int l = 0; l < num; l++)
            {
                float wx = (float)((double)vx + l * (double)Scale);
                float t2 = Blend[l];
                float h;
                Color m = default;
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
                    h = GenMath.Lerp(GenMath.Lerp(h1, h2, t2), GenMath.Lerp(h3, h4, t2), t);
                    if (mask != null)
                    {
                        m = Lerp(Lerp(m1, m2, t2), Lerp(m3, m4, t2), t);
                    }
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

    /// <summary>
    /// a to b by t, held to 0..1, per channel.
    /// </summary>
    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }
}
