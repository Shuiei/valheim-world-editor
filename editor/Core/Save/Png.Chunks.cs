using System.Text;

namespace TerrainEditor.Save;

public static partial class Png
{
    /// <summary>The PNG Paeth predictor: whichever of left, up and up-left is nearest to left + up - up-left.</summary>
    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    /// <summary>Writes an int big-endian (PNG byte order) at offset o.</summary>
    private static void BigEndian(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    /// <summary>Writes one PNG chunk: length, type, data and CRC-32 of type and data.</summary>
    private static void Chunk(Stream s, string type, byte[] data)
    {
        byte[] len = new byte[4];
        BigEndian(len, 0, data.Length);
        s.Write(len);
        byte[] t = Encoding.ASCII.GetBytes(type);
        s.Write(t);
        s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, t), data) ^ 0xFFFFFFFF;
        byte[] c = new byte[4];
        BigEndian(c, 0, (int)crc);
        s.Write(c);
    }

    /// <summary>The CRC-32 lookup table (polynomial 0xEDB88320) PNG chunks use.</summary>
    private static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c;
    })];

    /// <summary>Folds data into a running CRC-32 (not inverted at either end).</summary>
    private static uint Crc(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc;
    }

    /// <summary>
    /// Whether a text is a PNG tEXt keyword: 1 to 79 printable Latin-1 characters (32 to 126, 161 to 255), no space at
    /// either end nor two in a row.
    /// </summary>
    private static bool IsKeyword(string k) =>
        k.Length is >= 1 and <= 79 && k.All(c => c is >= ' ' and <= '~' or >= '\u00a1' and <= '\u00ff')
        && k[0] != ' ' && k[^1] != ' ' && !k.Contains("  ", StringComparison.Ordinal);
}
