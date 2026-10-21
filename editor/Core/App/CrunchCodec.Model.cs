// CrunchCodec: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// The Huffman code reading of <see cref="CrunchCodec"/>: the models the levels are coded with.
/// </summary>
internal sealed partial class CrunchCodec
{
    /// <summary>
    /// A Huffman code as the data sends it (decode_receive_static_data_model): how many symbols, then the code
    /// lengths' own code, then the lengths with runs of zeros and repeats. Null when it has no symbol.
    /// </summary>
    /// <exception cref="InvalidDataException">A broken code.</exception>
    public CrunchModel? ReadModel()
    {
        int total = (int)Bits(14);
        if (total == 0)
        {
            return null;
        }
        if (total > CrunchFormat.MaxSymbols)
        {
            throw new InvalidDataException("crn code too large");
        }
        int sent = (int)Bits(5);
        if (sent is < 1 or > 21)
        {
            throw new InvalidDataException("crn code lengths broken");
        }
        byte[] lengthSizes = new byte[21];
        for (int i = 0; i < sent; i++)
        {
            lengthSizes[CrunchFormat.CodeLengthOrder[i]] = (byte)Bits(3);
        }
        var lengths = new CrunchModel(lengthSizes);
        byte[] sizes = new byte[total];
        int ofs = 0;
        while (ofs < total)
        {
            int left = total - ofs, code = Decode(lengths);
            if (code <= 16)
            {
                sizes[ofs++] = (byte)code;
                continue;
            }
            int run = code switch
            {
                17 => (int)Bits(3) + 3,
                18 => (int)Bits(7) + 11,
                19 => (int)Bits(2) + 3,
                _ => (int)Bits(6) + 7,
            };
            if (run > left || (code >= 19 && (ofs == 0 || sizes[ofs - 1] == 0)))
            {
                throw new InvalidDataException("crn code lengths broken");
            }
            byte value = code >= 19 ? sizes[ofs - 1] : (byte)0;
            sizes.AsSpan(ofs, run).Fill(value);
            ofs += run;
        }
        return new CrunchModel(sizes);
    }
}
