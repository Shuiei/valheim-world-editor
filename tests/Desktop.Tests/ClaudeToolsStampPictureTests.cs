using Avalonia.Headless.XUnit;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's stamp tool (ClaudeTools.Stamp) given a picture's path as the stamp, a mask naming a biome there is not, or
/// no area to stamp.
/// </summary>
[Collection("World files")]
public class ClaudeToolsStampPictureTests
{
    /// <summary>A 4 x 4 greyscale PNG, dark to light across it, written in the folder.</summary>
    private static string Picture(string dir)
    {
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "Hill.png");
        using var bmp = new SKBitmap(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                byte v = (byte)(40 * (x + y));
                bmp.SetPixel(x, y, new SKColor(v, v, v));
            }
        }
        using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(file, png.ToArray());
        return file;
    }

    [AvaloniaFact]
    public async Task APicturesPathIsAStampAndWhatCannotBeStampedIsSaid()
    {
        using var r = new ClaudeRun();
        Assert.StartsWith("No area is open", await r.T.Stamp("Mountain", 0, 0));
        await r.OpenArea();

        Assert.StartsWith("Stamped ", await r.T.Stamp(Picture(r.Homestead), 0, 0, radius: 15, height: 5));
        Assert.StartsWith("Nowhere: the biomes are ", await r.T.Stamp("Mountain", 0, 0, mask: new ClaudeTools.MaskSpec { Biomes = ["Nowhere"] }));
    }
}
