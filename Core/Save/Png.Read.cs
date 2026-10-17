using System.IO.Compression;
using System.Text;

namespace TerrainEditor.Save;

public static partial class Png
{
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
        bool header = false;
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
                    header = true;
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
                default:
                    break;
            }
            p += 12 + len;
            if (type == "IEND")
            {
                break;
            }
        }
        if (!header)
        {
            throw new InvalidDataException("the PNG picture has no header (IHDR)");
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
            bool more = false;
            try
            {
                while (o.Length < need && (n = zs.Read(buffer, 0, (int)Math.Min(buffer.Length, need - o.Length))) > 0)
                {
                    o.Write(buffer, 0, n);
                }
                // The rows are all there: what follows must be the data's end, where zlib checks its checksum
                // (one byte asked for, so a picture claiming more is not unpacked).
                more = o.Length == need && zs.Read(buffer, 0, 1) > 0;
            }
            catch (Exception e) when (e is IOException or InvalidDataException)
            {
                // Damaged compressed data (a ZLibException, or a wrong checksum): read from memory, nothing else
                // can fail.
                throw new InvalidDataException("the PNG picture data is damaged", e);
            }
            if (more)
            {
                throw new InvalidDataException("the PNG picture holds more data than its size");
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
}
