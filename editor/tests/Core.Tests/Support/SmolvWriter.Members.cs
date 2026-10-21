namespace WorldEditor.Tests;

/// <summary>
/// The parts of SMOL-V that are not one instruction's plain words: the member decorations of a structure (merged), the
/// compact vector shuffles and the words an op has beyond its own. For <see cref="SmolvWriter"/>.
/// </summary>
public static partial class SmolvWriter
{
    /// <summary>
    /// The words an op has beyond the ones SMOL-V counts for it (its literals are written apart).
    /// </summary>
    private static int ExtraWords(int op) => op switch
    {
        79 => 4,
        71 => 2,
        61 or 65 => 3,
        _ => 0,
    };

    /// <summary>
    /// The member decorations of one structure in a row (from the one at <paramref name="i"/>), written as one: their
    /// count, then each one's index (delta), decoration, its literals (an offset as a delta from the last). Returns the
    /// position after the last one written.
    /// </summary>
    private static int MemberDecorations(SmolvBuffer o, uint[] spirv, int i, int len, uint target)
    {
        var members = new List<int> { i };
        int next = i + len;
        while (next < spirv.Length && (spirv[next] & 0xFFFF) == 72 && spirv[next + 1] == target)
        {
            members.Add(next);
            next += (int)(spirv[next] >> 16);
        }
        o.Add((byte)members.Count);
        uint prevIndex = 0, prevOffset = 0;
        foreach (int m in members)
        {
            int mlen = (int)(spirv[m] >> 16);
            uint index = spirv[m + 2], dec = spirv[m + 3];
            o.Varint(index - prevIndex);
            prevIndex = index;
            o.Varint(dec);
            int known = dec is 0 or (>= 2 and <= 5) ? 0 : dec is >= 29 and <= 37 ? 1 : -1;
            if (known < 0)
            {
                o.Varint((uint)(mlen - 4));
            }
            if (dec == 35)
            {
                o.Varint(spirv[m + 4] - prevOffset);
                prevOffset = spirv[m + 4];
            }
            else
            {
                for (int w = 4; w < mlen; w++)
                {
                    o.Varint(spirv[m + w]);
                }
            }
        }
        return next;
    }

    /// <summary>
    /// The components of a compact vector shuffle (each one 0 to 3, the first four at 2 bits each) in one byte.
    /// </summary>
    private static byte CompactComponents(uint[] spirv, int i, int len)
    {
        byte s = 0;
        foreach (var (at, shift) in new[] { (5, 6), (6, 4), (7, 2), (8, 0) })
        {
            if (len > at)
            {
                s |= (byte)(spirv[i + at] << shift);
            }
        }
        return s;
    }
}
