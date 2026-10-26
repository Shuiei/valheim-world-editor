using System.IO.Compression;
using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Just enough PNG for heightmaps: writes 16-bit grayscale pictures (with text notes), and reads the pictures other
/// programs make (gray, gray + alpha, RGB, RGBA; 8 or 16 bits; not interlaced), as brightness from 0 to 1. Browsers can
/// only read PNGs as 8 bits, so 16-bit heightmaps go through here.
/// </summary>
public static partial class Png
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
    /// <exception cref="ArgumentException">
    /// No pixels, fewer values than pixels, a filter PNG does not have, or a note PNG cannot hold (a keyword of 1 to 79
    /// printable Latin-1 characters without spaces at its ends or two in a row; Latin-1 text without NUL).
    /// </exception>
    public static byte[] WriteGray16(int width, int height, ushort[] values, IReadOnlyDictionary<string, string>? text = null, int filter = 1)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("A PNG picture has at least one pixel.", width <= 0 ? nameof(width) : nameof(height));
        }
        if (values.Length < (long)width * height)
        {
            throw new ArgumentException($"{width} × {height} pixels need as many values; there are {values.Length}.", nameof(values));
        }
        if (filter is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "PNG's row filters are 0 (none) to 4 (Paeth).");
        }
        foreach (var (k, v) in text ?? new Dictionary<string, string>())
        {
            if (!IsKeyword(k) || v.Any(c => c is '\0' or > '\u00ff'))
            {
                throw new ArgumentException($"The note \"{k}\" cannot be a PNG tEXt note.", nameof(text));
            }
        }
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
}
