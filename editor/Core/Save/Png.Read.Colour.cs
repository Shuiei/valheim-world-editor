namespace TerrainEditor.Save;

public static partial class Png
{
    /// <summary>A picture read in colour by <see cref="ReadRgb"/>.</summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Rgb">Three bytes (red, green, blue) per pixel, row by row from the top; alpha is dropped.</param>
    public sealed record ColourImage(int Width, int Height, byte[] Rgb);

    /// <summary>
    /// Reads a PNG picture as 8-bit red, green and blue per pixel: gray, gray with alpha, RGB and RGBA at 8 or 16 bits
    /// (16 bits rounded to 8), and palette pictures at 1, 2, 4 or 8 bits (what paint programs save a picture of few
    /// colours as). Alpha is dropped.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Not a PNG, cut short, interlaced, a depth its colour type does not have, a palette picture without its palette or
    /// with an index past it, an unknown row filter, empty, or more than 64 Mi (2^26) pixels.
    /// </exception>
    public static ColourImage ReadRgb(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException("not a PNG picture");
        }
        var (header, _, idat) = ScanChunks(bytes);
        if (header is not { } h)
        {
            throw new InvalidDataException("the PNG picture has no header (IHDR)");
        }
        if (h.Width <= 0 || h.Height <= 0 || (long)h.Width * h.Height > 64L << 20)
        {
            throw new InvalidDataException("the picture is empty or too large");
        }
        bool palette = h.Color == 3;
        int channels = palette ? 1 : ChannelsOf(h.Color);
        bool depthOk = palette ? h.Depth is 1 or 2 or 4 or 8 : h.Depth is 8 or 16;
        if (!depthOk)
        {
            throw new InvalidDataException($"{h.Depth}-bit PNG pictures of this kind are not supported");
        }
        byte[]? colours = palette ? PaletteOf(bytes) : null;
        // Bits per pixel, then bytes per row and the filter's step (at least one byte).
        int bits = channels * h.Depth;
        int stride = (int)(((long)h.Width * bits + 7) / 8);
        int bpp = Math.Max(1, bits / 8);
        int need = (stride + 1) * h.Height;
        byte[] raw = Inflate(idat, need);
        if (raw.Length < need)
        {
            throw new InvalidDataException("the PNG picture data is cut short");
        }
        byte[] rgb = new byte[h.Width * h.Height * 3];
        byte[] prev = new byte[stride], row = new byte[stride];
        for (int y = 0; y < h.Height; y++)
        {
            Unfilter(raw, y * (stride + 1), row, prev, bpp);
            for (int x = 0; x < h.Width; x++)
            {
                int o = (y * h.Width + x) * 3;
                if (colours != null)
                {
                    int index = PackedIndex(row, x, h.Depth);
                    if (index * 3 + 2 >= colours.Length)
                    {
                        throw new InvalidDataException("the PNG picture uses a colour its palette does not have");
                    }
                    rgb[o] = colours[index * 3];
                    rgb[o + 1] = colours[index * 3 + 1];
                    rgb[o + 2] = colours[index * 3 + 2];
                }
                else if (channels >= 3)
                {
                    rgb[o] = Channel8(row, x, 0, bpp, h.Depth);
                    rgb[o + 1] = Channel8(row, x, 1, bpp, h.Depth);
                    rgb[o + 2] = Channel8(row, x, 2, bpp, h.Depth);
                }
                else
                {
                    rgb[o] = rgb[o + 1] = rgb[o + 2] = Channel8(row, x, 0, bpp, h.Depth);
                }
            }
            (prev, row) = (row, prev);
        }
        return new ColourImage(h.Width, h.Height, rgb);
    }

    /// <summary>One channel of a pixel as 8 bits (a 16-bit one rounded to its high byte's nearest).</summary>
    private static byte Channel8(byte[] row, int x, int ch, int bpp, int depth) =>
        depth == 16 ? (byte)Math.Min(255, (((row[x * bpp + ch * 2] << 8) | row[x * bpp + ch * 2 + 1]) + 128) / 257) : row[x * bpp + ch];

    /// <summary>A palette index packed at <paramref name="depth"/> bits a pixel, the first pixel in the high bits.</summary>
    private static int PackedIndex(byte[] row, int x, int depth)
    {
        if (depth == 8)
        {
            return row[x];
        }
        int perByte = 8 / depth;
        int shift = 8 - depth * (x % perByte + 1);
        return (row[x / perByte] >> shift) & ((1 << depth) - 1);
    }

    /// <summary>The palette (PLTE: three bytes a colour) of a palette picture.</summary>
    /// <exception cref="InvalidDataException">The picture has none, or its length is not a whole number of colours.</exception>
    private static byte[] PaletteOf(byte[] bytes)
    {
        int p = 8;
        while (p + 8 <= bytes.Length)
        {
            int len = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
            if (len < 0 || len > bytes.Length - p - 12)
            {
                break;
            }
            if (bytes[p + 4] == 'P' && bytes[p + 5] == 'L' && bytes[p + 6] == 'T' && bytes[p + 7] == 'E')
            {
                if (len == 0 || len % 3 != 0)
                {
                    throw new InvalidDataException("the PNG picture's palette is damaged");
                }
                return bytes.AsSpan(p + 8, len).ToArray();
            }
            p += 12 + len;
        }
        throw new InvalidDataException("the PNG palette picture has no palette");
    }
}
