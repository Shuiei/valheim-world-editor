using System.IO.Compression;

namespace TerrainEditor.Save;

public static partial class Png
{
    /// <summary>Unpacks the zlib-compressed image data, at most <paramref name="need"/> bytes and never more than the data's end allows.</summary>
    /// <param name="idat">The joined image data.</param>
    /// <param name="need">The bytes the rows need.</param>
    /// <returns>The unpacked rows (up to <paramref name="need"/> bytes).</returns>
    private static byte[] Inflate(byte[] idat, int need)
    {
        using ZLibStream zs = new(new MemoryStream(idat), CompressionMode.Decompress);
        using MemoryStream o = new();
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
        return o.ToArray();
    }

    /// <summary>The brightness of every pixel of the unpacked rows (colour as luminance, alpha ignored), by rows.</summary>
    /// <param name="raw">The unpacked rows, each with its filter byte.</param>
    /// <param name="h">The header.</param>
    /// <param name="channels">The channels per pixel.</param>
    /// <param name="bpp">The bytes per pixel.</param>
    /// <returns>The brightness per pixel, from 0 to 1.</returns>
    private static float[] Brightness(byte[] raw, PngHeader h, int channels, int bpp)
    {
        int width = h.Width, stride = width * bpp;
        float[] values = new float[width * h.Height];
        byte[] prev = new byte[stride], row = new byte[stride];
        float max = h.Depth == 16 ? 65535f : 255f;
        for (int y = 0; y < h.Height; y++)
        {
            int o = y * (stride + 1);
            Unfilter(raw, o, row, prev, bpp);
            for (int x = 0; x < width; x++)
            {
                // Gray as is; colour as brightness; alpha (when there is one) is ignored.
                values[y * width + x] = channels >= 3
                    ? 0.299f * Sample(row, x, 0, bpp, h.Depth, max) + 0.587f * Sample(row, x, 1, bpp, h.Depth, max) + 0.114f * Sample(row, x, 2, bpp, h.Depth, max)
                    : Sample(row, x, 0, bpp, h.Depth, max);
            }
            (prev, row) = (row, prev);
        }
        return values;
    }

    /// <summary>Undoes the row filter of one row into <paramref name="row"/>.</summary>
    /// <param name="raw">The unpacked rows.</param>
    /// <param name="o">The place of the row's filter byte in <paramref name="raw"/>.</param>
    /// <param name="row">Receives the row's pixels.</param>
    /// <param name="prev">The previous row's pixels.</param>
    /// <param name="bpp">The bytes per pixel.</param>
    private static void Unfilter(byte[] raw, int o, byte[] row, byte[] prev, int bpp)
    {
        int filter = raw[o];
        for (int i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? row[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
            row[i] = (byte)(raw[o + 1 + i] + filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), 0 => 0, _ => throw new InvalidDataException("unknown PNG row filter") });
        }
    }

    /// <summary>One channel of a pixel, as a fraction of its maximum.</summary>
    /// <param name="row">The unpacked row's pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="ch">The channel.</param>
    /// <param name="bpp">The bytes per pixel.</param>
    /// <param name="depth">Bits per channel (8 or 16).</param>
    /// <param name="max">The channel's maximum (255 or 65535).</param>
    /// <returns>The channel's value from 0 to 1.</returns>
    internal static float Sample(byte[] row, int x, int ch, int bpp, int depth, float max) =>
        depth == 16 ? ((row[x * bpp + ch * 2] << 8) | row[x * bpp + ch * 2 + 1]) / max : row[x * bpp + ch] / max;
}
