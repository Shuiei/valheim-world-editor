namespace TerrainEditor.App;

/// <summary>
/// The SPIR-V op table SMOL-V decodes by: each op's result, type and operand layout, and the ops SMOL-V encodes specially.
/// </summary>
internal static class SmolvOpcodes
{
    /// <summary>
    /// Per SPIR-V op (0..366), four digits: has a result id, has a type id, how many ids after them are
    /// kept as deltas from the result, whether the other words are varints (SMOL-V's kSpirvOpData).
    /// </summary>
    internal const string Table =
        "000011000000000100000000000000000001110000001000110111210001000100010001110010011001100110011001" +
        "100110011001100110011001100110011001100110011001100110011001000111001100110011001190110111001100" +
        "110011001100119011001100110111000000119011001101110011110021000000001101110011001100110011000001" +
        "000110000000000011001111112111211190111111211110110011001100112111211131113111211121113111311121" +
        "113111311121003111101110111011201110112011101110110011101110111011101110111011101110111011101110" +
        "111011101110111111101100111011101120112011201120112011201120112011201120112011201120112011201120" +
        "112011201120112011201120112011201120110011101110111011101110111011101120112011201120112011201120" +
        "111011301120112011201120112011201120112011201120112011201120112011201120112011201120112011201120" +
        "110011001120112011201120112011201110114011301130111011101100110011001100110011001100110011001100" +
        "110011000000000000000000110011000030002011001100000011001100110011001100110011001100110011001100" +
        "110011001100110011001100002100111000001000310000000000000000000000000000110011000000110011001100" +
        "110011001100110011001100110011001100110011001100110011001100110000000000110011001100110011000000" +
        "000011001100110011001100110011001100000000001100110000000000110011001121112111311131112111211131" +
        "113111211131113111100000110000001100110011001100110011001100110011010021110000010001111111111111" +
        "111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111" +
        "1111111111111111111111111111";

    /// <summary>
    /// The ops each encoding version knows (version 0: up to OpModuleProcessed): ops from this number up are
    /// read without <see cref="Table"/>'s data.
    /// </summary>
    internal static readonly Dictionary<int, int> Known = new() { [0] = 331, [1] = 367 };

    /// <summary>
    /// The ops SMOL-V encodes specially (OpVectorShuffle, its compact form, OpDecorate, OpMemberDecorate,
    /// OpLoad, OpAccessChain).
    /// </summary>
    internal const int Shuffle = 79, ShuffleCompact = 13, Decorate = 71, MemberDecorate = 72, Load = 61, AccessChain = 65;

    /// <summary>The most common ops are swapped with rare small ones, to fit a one-byte varint.</summary>
    internal static readonly Dictionary<int, int> Swap = BuildSwap();

    /// <summary>The swaps both ways (each pair maps to the other).</summary>
    private static Dictionary<int, int> BuildSwap()
    {
        var s = new Dictionary<int, int>();
        foreach (var (a, b) in new[] { (71, 0), (61, 1), (62, 2), (65, 3), (79, 4), (72, 7), (248, 8), (59, 9), (133, 10), (129, 11), (32, 14), (127, 15) })
        {
            s[a] = b;
            s[b] = a;
        }
        return s;
    }

    /// <summary>An op's four <see cref="Table"/> digits.</summary>
    internal static (int Result, int Type, int Deltas, int Varints) Op(int op) =>
        (Table[op * 4] - '0', Table[op * 4 + 1] - '0', Table[op * 4 + 2] - '0', Table[op * 4 + 3] - '0');
}
