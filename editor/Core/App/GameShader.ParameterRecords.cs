using System.Text;

namespace TerrainEditor.App;

// Reading the records of a parameter blob: the names and the numbers after each (see GameShader).
public static partial class GameShader
{
    /// <summary>
    /// The named records of a parameter blob: a name, then the numbers that follow it up to the next name.
    /// </summary>
    /// <param name="entry">The parameter blob's entry.</param>
    private static List<(string Name, List<uint> Nums)> ParameterRecords(byte[] entry)
    {
        var recs = new List<(string Name, List<uint> Nums)>();
        int p = 24;
        while (p + 4 <= entry.Length)
        {
            uint n = BitConverter.ToUInt32(entry, p);
            if (n is > 0 and < 128 && p + 4 + n <= entry.Length && IsName(entry.AsSpan(p + 4, (int)n)))
            {
                recs.Add((Encoding.ASCII.GetString(entry, p + 4, (int)n), new List<uint>()));
                p = p + 4 + (int)n + (int)((4 - n % 4) % 4);
                continue;
            }
            if (recs.Count > 0)
            {
                recs[^1].Nums.Add(n);
            }
            p += 4;
        }
        return recs;
    }

    /// <summary>
    /// Whether the bytes are an identifier (ASCII letter or underscore, then letters, digits, underscores).
    /// </summary>
    private static bool IsName(ReadOnlySpan<byte> s)
    {
        if (s.Length == 0 || !(char.IsAsciiLetter((char)s[0]) || s[0] == '_'))
        {
            return false;
        }
        foreach (byte c in s)
        {
            if (!(char.IsAsciiLetterOrDigit((char)c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }
}
