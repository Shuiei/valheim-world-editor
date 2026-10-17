using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

/// <summary>
/// Compares the offline generator with ground truth recorded in the real game by the TerrainDump
/// client plugin (BepInEx/TerrainDump.txt). Every value is compared bit-for-bit.
/// </summary>
public static partial class DumpVerifier
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
        var tally = new Tally();
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            string[] p = line.Split(' ');
            if (!Readable(p))
            {
                tally.Unreadable++;
                tally.Error($"Line {n + 1} not read: {line}");
                continue;
            }
            try
            {
                Compare(p, line, tally, seedName);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                tally.Unreadable++;
                tally.Error($"Line {n + 1} not read ({e.Message}): {line}");
            }
        }
        PrintSummary(tally);
        return new Result(tally.RandomOk, tally.RandomWrong, tally.PerlinOk, tally.PerlinWrong, tally.HeightsOk, tally.HeightsWrong, tally.BiomesWrong, tally.ZonesOk, tally.ZonesWrong, tally.WaterWrong, tally.Errors)
        {
            HeightMaxDiff = tally.HeightMax,
            ZoneMaxDiff = tally.ZoneMax,
            Unreadable = tally.Unreadable,
        };
    }

    /// <summary>
    /// Prints the counts of each kind of line and the first mismatches to the console.
    /// </summary>
    private static void PrintSummary(Tally t)
    {
        Console.WriteLine($"Random:  {t.RandomOk} ok, {t.RandomWrong} wrong");
        if (t.Unreadable > 0)
        {
            Console.WriteLine($"Unreadable lines: {t.Unreadable}");
        }
        Console.WriteLine($"Perlin:  {t.PerlinOk} ok, {t.PerlinWrong} wrong (max diff {t.PerlinMax:G4})");
        Console.WriteLine($"Heights: {t.HeightsOk} ok, {t.HeightsWrong} wrong (max diff {t.HeightMax:G4} m), biome mismatches {t.BiomesWrong}");
        Console.WriteLine($"Zones:   {t.ZonesOk} exact, {t.ZonesWrong} differ (max diff {t.ZoneMax:G4} m)");
        foreach (string e in t.Errors)
        {
            Console.WriteLine("  " + e);
        }
    }

    /// <summary>
    /// A generator for a seed name (world gen version 2), timed on the console.
    /// </summary>
    internal static WorldGenerator Init(string seedName)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        World world = new() { m_seedName = seedName, m_seed = TerrainEditor.Save.StableHash.Of(seedName), m_worldGenVersion = 2 };
        var gen = WorldGenerator.Create(world);
        Console.WriteLine($"  offline worldgen init {watch.ElapsedMilliseconds} ms, seed {world.m_seed}");
        return gen;
    }
}
