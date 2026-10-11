using System.IO.Compression;
using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Just enough PNG for heightmaps: writes 16-bit grayscale pictures (with text notes), and reads the pictures other
/// programs make (gray, gray + alpha, RGB, RGBA; 8 or 16 bits; not interlaced), as brightness from 0 to 1. Browsers can
/// only read PNGs as 8 bits, so 16-bit heightmaps go through here.
/// </summary>
public static class Png
{
    /// <summary>The eight bytes every PNG file starts with.</summary>
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>A picture read by <c>Read</c>.</summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Values">Brightness per pixel, 0..1, width * height, row by row from the top.</param>
    /// <param name="Text">The picture's tEXt notes, keyword to text.</param>
    public sealed record Image(int Width, int Height, float[] Values, Dictionary<string, string> Text);

    /// <summary>A 16-bit grayscale PNG file.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="values">width * height, row by row from the top, 0..65535.</param>
    /// <param name="text">Notes to store as tEXt chunks (Latin-1), or null for none.</param>
    /// <param name="filter">
    /// The PNG row filter to use (0 none ... 4 Paeth; the default 1 Sub compresses heightmaps well).
    /// </param>
    public static byte[] WriteGray16(int width, int height, ushort[] values, IReadOnlyDictionary<string, string>? text = null, int filter = 1)
    {
        using MemoryStream png = new();
        png.Write(Signature);
        byte[] ihdr = new byte[13];
        BigEndian(ihdr, 0, width);
        BigEndian(ihdr, 4, height);
        ihdr[8] = 16;  // bit depth
        ihdr[9] = 0;   // grayscale
        Chunk(png, "IHDR", ihdr);
        foreach (var (k, v) in text ?? new Dictionary<string, string>())
        {
            Chunk(png, "tEXt", Encoding.Latin1.GetBytes(k + "\0" + v));
        }
        int stride = width * 2;
        byte[] raw = new byte[(stride + 1) * height], prev = new byte[stride], row = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ushort v = values[y * width + x];
                row[x * 2] = (byte)(v >> 8);
                row[x * 2 + 1] = (byte)v;
            }
            int o = y * (stride + 1);
            raw[o] = (byte)filter;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= 2 ? row[i - 2] : 0, b = prev[i], c = i >= 2 ? prev[i - 2] : 0;
                raw[o + 1 + i] = (byte)(row[i] - filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => 0 });
            }
            (prev, row) = (row, prev);
        }
        using (MemoryStream z = new())
        {
            using (ZLibStream zs = new(z, CompressionLevel.Optimal, leaveOpen: true))
            {
                zs.Write(raw);
            }
            Chunk(png, "IDAT", z.ToArray());
        }
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Reads a PNG picture as brightness per pixel; colour becomes luminance and alpha is ignored.</summary>
    /// <exception cref="InvalidDataException">
    /// Not a PNG, cut short, interlaced, palette-based, not 8 or 16 bits, an unknown row filter, empty, or more than
    /// 64 Mi (2^26) pixels.
    /// </exception>
    public static Image Read(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException("not a PNG picture");
        }
        int width = 0, height = 0, depth = 0, color = 0;
        Dictionary<string, string> text = [];
        using MemoryStream idat = new();
        int p = 8;
        while (p + 8 <= bytes.Length)
        {
            int len = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
            string type = Encoding.ASCII.GetString(bytes, p + 4, 4);
            // Compared as what remains: p + 12 + len overflows for a length near int.MaxValue.
            if (len < 0 || len > bytes.Length - p - 12)
            {
                throw new InvalidDataException("the PNG picture is cut short");
            }
            ReadOnlySpan<byte> data = bytes.AsSpan(p + 8, len);
            switch (type)
            {
                case "IHDR":
                    if (len < 13)
                    {
                        throw new InvalidDataException("the PNG picture's header is cut short");
                    }
                    width = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
                    height = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
                    depth = data[8];
                    color = data[9];
                    if (data[12] != 0)
                    {
                        throw new InvalidDataException("interlaced PNG pictures are not supported; save it without interlacing");
                    }
                    break;
                case "tEXt":
                    int zero = data.IndexOf((byte)0);
                    if (zero > 0)
                    {
                        text[Encoding.Latin1.GetString(data[..zero])] = Encoding.Latin1.GetString(data[(zero + 1)..]);
                    }
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
            }
            p += 12 + len;
            if (type == "IEND")
            {
                break;
            }
        }
        int channels = color switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new InvalidDataException("palette PNG pictures are not supported; save it as grayscale or RGB") };
        if (depth is not 8 and not 16)
        {
            throw new InvalidDataException($"{depth}-bit PNG pictures are not supported (8 or 16 bits)");
        }
        if (width <= 0 || height <= 0 || (long)width * height > 64L << 20)
        {
            throw new InvalidDataException("the picture is empty or too large");
        }
        int bpp = channels * depth / 8, stride = width * bpp;
        // Only as much as the rows need is unpacked: the data may claim far more.
        int need = (stride + 1) * height;
        byte[] raw;
        using (ZLibStream zs = new(new MemoryStream(idat.ToArray()), CompressionMode.Decompress))
        using (MemoryStream o = new())
        {
            byte[] buffer = new byte[81920];
            int n;
            try
            {
                while (o.Length < need && (n = zs.Read(buffer, 0, (int)Math.Min(buffer.Length, need - o.Length))) > 0)
                {
                    o.Write(buffer, 0, n);
                }
            }
            catch (IOException e)
            {
                // Damaged compressed data (ZLibException): read from memory, nothing else can fail.
                throw new InvalidDataException("the PNG picture data is damaged", e);
            }
            raw = o.ToArray();
        }
        if (raw.Length < need)
        {
            throw new InvalidDataException("the PNG picture data is cut short");
        }
        float[] values = new float[width * height];
        byte[] prev = new byte[stride], row = new byte[stride];
        float max = depth == 16 ? 65535f : 255f;
        for (int y = 0; y < height; y++)
        {
            int o = y * (stride + 1), filter = raw[o];
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? row[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                row[i] = (byte)(raw[o + 1 + i] + filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), 0 => 0, _ => throw new InvalidDataException("unknown PNG row filter") });
            }
            for (int x = 0; x < width; x++)
            {
                float Sample(int ch) => depth == 16 ? ((row[x * bpp + ch * 2] << 8) | row[x * bpp + ch * 2 + 1]) / max : row[x * bpp + ch] / max;
                // Gray as is; colour as brightness; alpha (when there is one) is ignored.
                values[y * width + x] = channels >= 3 ? 0.299f * Sample(0) + 0.587f * Sample(1) + 0.114f * Sample(2) : Sample(0);
            }
            (prev, row) = (row, prev);
        }
        return new Image(width, height, values, text);
    }

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
}
