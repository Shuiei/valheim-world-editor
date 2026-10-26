// CrunchFormat.cs: the crn constants and CRC shared by the Crunch decoder parts. Ported from crunch's crn_decomp.h;
// the source notes and the zlib license are in Crunch.cs.

namespace TerrainEditor.App;

/// <summary>The crn format's constants and CRC (see <see cref="Crunch"/>).</summary>
internal static class CrunchFormat
{
    /// <summary>The crn formats read: DXT1 and DXT5 (crunch's cCRNFmtDXT1 and cCRNFmtDXT5).</summary>
    internal const int Dxt1 = 0, Dxt5 = 2;

    /// <summary>The bytes of a crn header before its levels' offsets.</summary>
    internal const int FixedHeader = 70;

    /// <summary>The largest width or height crunch writes.</summary>
    internal const int MaxResolution = 4096;

    /// <summary>The most symbols a Huffman code has.</summary>
    internal const int MaxSymbols = 8192;

    /// <summary>The longest Huffman code, in bits.</summary>
    internal const int MaxCodeSize = 16;

    /// <summary>The most bits a Huffman decoding table looks up at once.</summary>
    internal const int MaxTableBits = 11;

    /// <summary>
    /// The code length codes in the order they are sent: zero runs (short, long), repeats of the last length (short,
    /// long), then lengths.
    /// </summary>
    internal static readonly byte[] CodeLengthOrder = [17, 18, 19, 20, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15, 16];

    /// <summary>A DXT5 alpha selector from its linear order (0 to 7, first to second endpoint) to a block's.</summary>
    internal static readonly byte[] Dxt5FromLinear = [0, 2, 3, 4, 5, 6, 7, 1];

    /// <summary>crunch's CRC-16 of bytes (crc16 in crn_decomp.h), for the header's and the data's checks.</summary>
    internal static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            ushort q = (ushort)(b ^ (crc >> 8));
            crc <<= 8;
            ushort r = (ushort)((q >> 4) ^ q);
            crc ^= r;
            r <<= 5;
            crc ^= r;
            r <<= 7;
            crc ^= r;
        }
        return (ushort)~crc;
    }
}
