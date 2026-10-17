using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// How much a picture shows: average brightness, its spread, distinct colours (5 bits a channel) and share of
/// near-black.
/// </summary>
public sealed record PictureLook(double Mean, double Spread, int Colours, double Dark)
{
    /// <summary>
    /// Measures a saved picture, every third pixel each way.
    /// </summary>
    public static PictureLook Of(string path)
    {
        using var bmp = SKBitmap.Decode(path);
        Assert.NotNull(bmp);
        var colours = new HashSet<uint>();
        double sum = 0, sq = 0;
        int dark = 0, n = 0;
        for (int y = 0; y < bmp.Height; y += 3)
        {
            for (int x = 0; x < bmp.Width; x += 3)
            {
                var c = bmp.GetPixel(x, y);
                double l = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
                sum += l;
                sq += l * l;
                dark += l < 12 ? 1 : 0;
                colours.Add((uint)(c.Red >> 3 << 10 | c.Green >> 3 << 5 | c.Blue >> 3));
                n++;
            }
        }
        double mean = sum / n;
        return new PictureLook(mean, Math.Sqrt(sq / n - mean * mean), colours.Count, dark / (double)n);
    }
}
