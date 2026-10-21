using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The PNG written from a view's pixels (GlPicture).
/// </summary>
public class GlPictureTests
{
    [Fact]
    public void ThePictureIsOpaqueAndTopRowFirst()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vwe-picture-{Guid.NewGuid():N}.png");
        try
        {
            // Two rows as OpenGL reads them (bottom first): red with no alpha, then blue half see-through.
            byte[] px = [255, 0, 0, 0, 255, 0, 0, 0, 0, 0, 255, 128, 0, 0, 255, 128];
            GlPicture.Write(px, 2, 2, path);
            using var bmp = SKBitmap.Decode(path);
            Assert.Equal((2, 2), (bmp.Width, bmp.Height));
            Assert.Equal(new SKColor(0, 0, 255, 255), bmp.GetPixel(0, 0));
            Assert.Equal(new SKColor(255, 0, 0, 255), bmp.GetPixel(1, 1));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
