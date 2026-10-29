using System.IO.Compression;

namespace TerrainEditor.Save;

/// <summary>
/// The PNG writer for 8-bit RGBA pictures (the world overview's picture): no row filtering, fast compression.
/// </summary>
public static partial class Png
{
    /// <summary>
    /// A PNG file of an 8-bit RGBA picture, rows top first, 4 bytes per pixel.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">width * height * 4 bytes, row by row from the top.</param>
    public static byte[] WriteRgba8(int width, int height, byte[] rgba)
    {
        using MemoryStream png = new();
        png.Write(Signature);
        byte[] ihdr = new byte[13];
        BigEndian(ihdr, 0, width);
        BigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        Chunk(png, "IHDR", ihdr);
        using (MemoryStream raw = new())
        {
            using (ZLibStream z = new(raw, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgba, y * width * 4, width * 4);
                }
            }
            Chunk(png, "IDAT", raw.ToArray());
        }
        Chunk(png, "IEND", []);
        return png.ToArray();
    }
}
