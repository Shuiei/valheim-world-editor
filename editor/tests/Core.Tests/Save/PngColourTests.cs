using System.IO.Compression;
using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png.ReadRgb: colour pictures come back as their red, green and blue (alpha dropped), and palette pictures (what paint
/// programs save a picture of few colours as, at 4 bits a pixel for up to 16) are read through their palette.
/// </summary>
public class PngColourTests
{
    [Fact]
    public void AnRgbaPictureReadsAsItsColours()
    {
        byte[] rgba = [10, 20, 30, 255, 40, 50, 60, 0, 70, 80, 90, 128, 100, 110, 120, 255];

        var img = Png.ReadRgb(Png.WriteRgba8(2, 2, rgba));

        Assert.Equal((2, 2), (img.Width, img.Height));
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120], img.Rgb);
    }

    [Fact]
    public void AFourBitPalettePictureReadsThroughItsPalette()
    {
        // Three pixels a row (the last half byte unused), two rows, indices 0 1 2 / 2 1 0.
        byte[] palette = [255, 0, 0, 0, 255, 0, 0, 0, 255];
        byte[] png = Palette(3, 2, 4, palette, [[0x01, 0x20], [0x21, 0x00]]);

        var img = Png.ReadRgb(png);

        Assert.Equal([255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0], img.Rgb);
    }

    [Fact]
    public void APaletteIndexPastThePaletteIsRefused()
    {
        byte[] png = Palette(1, 1, 8, [1, 2, 3], [[5]]);

        Assert.Contains("palette does not have", Assert.Throws<InvalidDataException>(() => Png.ReadRgb(png)).Message);
    }

    [Fact]
    public void APalettePictureWithoutItsPaletteIsRefused()
    {
        byte[] png = Palette(1, 1, 8, null, [[0]]);

        Assert.Contains("no palette", Assert.Throws<InvalidDataException>(() => Png.ReadRgb(png)).Message);
    }

    /// <summary>A palette PNG (colour type 3), its rows unfiltered.</summary>
    private static byte[] Palette(int width, int height, int depth, byte[]? palette, byte[][] rows)
    {
        using MemoryStream png = new();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] ihdr = [.. BigEndian(width), .. BigEndian(height), (byte)depth, 3, 0, 0, 0];
        Chunk(png, "IHDR", ihdr);
        if (palette != null)
        {
            Chunk(png, "PLTE", palette);
        }
        using MemoryStream raw = new();
        using (ZLibStream z = new(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            foreach (byte[] row in rows)
            {
                z.WriteByte(0);
                z.Write(row);
            }
        }
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] BigEndian(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static void Chunk(Stream s, string type, byte[] data)
    {
        byte[] typed = [.. Encoding.ASCII.GetBytes(type), .. data];
        s.Write(BigEndian(data.Length));
        s.Write(typed);
        s.Write(BigEndian((int)Crc(typed)));
    }

    private static uint Crc(byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            c ^= b;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
        }
        return c ^ 0xFFFFFFFF;
    }
}
