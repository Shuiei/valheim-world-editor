namespace WorldEditor.Tests;

/// <summary>
/// SPIR-V to SMOL-V (as Unity keeps its Vulkan programs), the other way from the editor's decoder (Smolv): each
/// instruction's length and op as one varint, its type and result ids as varints (results as deltas from the last),
/// decorations' targets as deltas, the ids SMOL-V takes as deltas from the result, the rest as varints or words. Only
/// the ops the tests' programs use are known here (SMOL-V's data for them); member decorations of one structure in a
/// row are merged, and vector shuffles of small components written compact, as SMOL-V does.
/// </summary>
public static class SmolvWriter
{
    /// <summary>
    /// SMOL-V's data for the ops used: has a result id, has a type id, how many ids after them are deltas from the
    /// result, whether the other words are varints.
    /// </summary>
    private static readonly Dictionary<int, (bool Result, bool Type, int Deltas, bool Varints)> Ops = new()
    {
        [5] = (false, false, 0, false),    // OpName
        [6] = (false, false, 0, false),    // OpMemberName
        [14] = (false, false, 0, true),    // OpMemoryModel
        [15] = (false, false, 0, true),    // OpEntryPoint
        [16] = (false, false, 0, true),    // OpExecutionMode
        [17] = (false, false, 0, true),    // OpCapability
        [19] = (true, false, 0, true),     // OpTypeVoid
        [22] = (true, false, 0, true),     // OpTypeFloat
        [23] = (true, false, 0, true),     // OpTypeVector
        [25] = (true, false, 0, true),     // OpTypeImage
        [27] = (true, false, 0, true),     // OpTypeSampledImage
        [30] = (true, false, 0, true),     // OpTypeStruct
        [32] = (true, false, 0, true),     // OpTypePointer
        [33] = (true, false, 0, true),     // OpTypeFunction
        [43] = (true, true, 0, false),     // OpConstant
        [44] = (true, true, 9, false),     // OpConstantComposite
        [54] = (true, true, 0, true),      // OpFunction
        [56] = (false, false, 0, false),   // OpFunctionEnd
        [59] = (true, true, 0, true),      // OpVariable
        [61] = (true, true, 1, true),      // OpLoad
        [62] = (false, false, 2, true),    // OpStore
        [65] = (true, true, 0, true),      // OpAccessChain
        [71] = (false, false, 0, true),    // OpDecorate
        [72] = (false, false, 0, true),    // OpMemberDecorate
        [79] = (true, true, 2, true),      // OpVectorShuffle
        [248] = (true, false, 0, false),   // OpLabel
        [253] = (false, false, 0, false),  // OpReturn
    };

    /// <summary>
    /// The ops SMOL-V swaps with small ones (both ways).
    /// </summary>
    private static readonly Dictionary<int, int> Swap = new[] { (71, 0), (61, 1), (62, 2), (65, 3), (79, 4), (72, 7), (248, 8), (59, 9), (133, 10), (129, 11), (32, 14), (127, 15) }
        .SelectMany(p => new[] { (p.Item1, p.Item2), (p.Item2, p.Item1) }).ToDictionary(p => p.Item1, p => p.Item2);

    /// <summary>
    /// The ops each encoding version knows; ops from there on are written as plain words.
    /// </summary>
    private static int Known(int version) => version == 0 ? 331 : 367;

    /// <summary>
    /// A SPIR-V module (words) as SMOL-V of this encoding version (0 or 1).
    /// </summary>
    public static byte[] Encode(uint[] spirv, int version = 1)
    {
        var o = new List<byte>();
        void Word(uint v) => o.AddRange(BitConverter.GetBytes(v));
        void Varint(uint v)
        {
            while (v >= 128)
            {
                o.Add((byte)(v | 128));
                v >>= 7;
            }
            o.Add((byte)v);
        }
        static uint Zig(uint delta) => (uint)(((int)delta << 1) ^ ((int)delta >> 31));
        o.AddRange("LOMS"u8.ToArray());
        Word((spirv[1] & 0xFFFFFF) | ((uint)version << 24));
        Word(spirv[2]);
        Word(spirv[3]);
        Word(spirv[4]);
        Word((uint)spirv.Length * 4);
        uint prevResult = 0, prevDecorate = 0;
        int i = 5;
        while (i < spirv.Length)
        {
            int len = (int)(spirv[i] >> 16), op = (int)(spirv[i] & 0xFFFF);
            // A shuffle of components 0 to 3 only, of at most four: compact (its components in one byte).
            bool compact = op == 79 && len <= 9 && Enumerable.Range(5, len - 5).All(k => spirv[i + k] < 4);
            int extra = op switch
            {
                79 => 4,
                71 => 2,
                61 or 65 => 3,
                _ => 0,
            };
            int raw = compact ? 13 : Swap.GetValueOrDefault(op, op);
            uint l = (uint)(len - 1 - extra);
            Varint(((l >> 4) << 20) | (((uint)raw & 0xFFF0) << 4) | ((l & 0xF) << 4) | ((uint)raw & 0xF));
            var info = op < Known(version) ? Ops[op] : (Result: false, Type: false, Deltas: 0, Varints: false);
            int k = 1;
            if (info.Type)
            {
                Varint(spirv[i + k++]);
            }
            if (info.Result)
            {
                uint result = spirv[i + k++];
                Varint(Zig(result - prevResult));
                prevResult = result;
            }
            uint target = 0;
            if (op is 71 or 72)
            {
                target = spirv[i + k++];
                Varint(Zig(target - prevDecorate));
                prevDecorate = target;
            }
            if (op == 72)
            {
                // The member decorations of this structure in a row, each: index (delta), decoration, its literals
                // (an offset as a delta from the last).
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
                    Varint(index - prevIndex);
                    prevIndex = index;
                    Varint(dec);
                    int known = dec is 0 or (>= 2 and <= 5) ? 0 : dec is >= 29 and <= 37 ? 1 : -1;
                    if (known < 0)
                    {
                        Varint((uint)(mlen - 4));
                    }
                    if (dec == 35)
                    {
                        Varint(spirv[m + 4] - prevOffset);
                        prevOffset = spirv[m + 4];
                    }
                    else
                    {
                        for (int w = 4; w < mlen; w++)
                        {
                            Varint(spirv[m + w]);
                        }
                    }
                }
                i = next;
                continue;
            }
            for (int d = 0; d < info.Deltas && k < len; d++, k++)
            {
                Varint(Zig(prevResult - spirv[i + k]));
            }
            if (compact)
            {
                byte s = 0;
                foreach (var (at, shift) in new[] { (5, 6), (6, 4), (7, 2), (8, 0) })
                {
                    if (len > at)
                    {
                        s |= (byte)(spirv[i + at] << shift);
                    }
                }
                o.Add(s);
            }
            else
            {
                for (; k < len; k++)
                {
                    if (info.Varints)
                    {
                        Varint(spirv[i + k]);
                    }
                    else
                    {
                        Word(spirv[i + k]);
                    }
                }
            }
            i += len;
        }
        return [.. o];
    }
}
