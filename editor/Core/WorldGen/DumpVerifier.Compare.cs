namespace ValheimGen;

public static partial class DumpVerifier
{
    /// <summary>
    /// Compares one line of the dump with the offline generator, by its kind.
    /// </summary>
    /// <param name="p">The line's values.</param>
    /// <param name="line">The line as written.</param>
    /// <param name="t">Where the result is counted.</param>
    /// <param name="seedName">The world's seed name, for the lines that need a generator.</param>
    internal static void Compare(string[] p, string line, Tally t, string seedName)
    {
        switch (p[0])
        {
            case "R":
                CompareRandom(p, t);
                break;
            case "P":
                ComparePerlin(p, t);
                break;
            case "W":
                CompareWater(line, t, seedName);
                break;
            case "H":
                CompareHeight(p, t, seedName);
                break;
            case "Z":
                CompareZone(p, t, seedName);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Compares one Perlin noise sample, bit for bit.
    /// </summary>
    internal static void ComparePerlin(string[] p, Tally t)
    {
        float x = F(p[1]), y = F(p[2]), want = F(p[3]);
        float got = UnityPerlin.Noise(x, y);
        if (BitConverter.SingleToInt32Bits(got) == BitConverter.SingleToInt32Bits(want))
        {
            t.PerlinOk++;
        }
        else
        {
            t.PerlinWrong++;
            t.PerlinMax = Math.Max(t.PerlinMax, Math.Abs(got - want));
            t.Error($"Perlin({x}, {y}): expected {want:R} got {got:R}");
        }
    }

    /// <summary>
    /// Compares the lake, river and stream counts of the seed's generator with the line.
    /// </summary>
    internal static void CompareWater(string line, Tally t, string seedName)
    {
        WorldGenerator gen = t.Generator ??= Init(seedName);
        string offline = $"W lakes {gen.GetLakes().Count} rivers {gen.GetRivers().Count} streams {gen.GetStreams().Count}";
        Console.WriteLine($"  game:    {line}");
        Console.WriteLine($"  offline: {offline}");
        if (offline != line.Trim())
        {
            t.WaterWrong++;
            t.Error($"Water: expected [{line.Trim()}] got [{offline}]");
        }
    }
}
