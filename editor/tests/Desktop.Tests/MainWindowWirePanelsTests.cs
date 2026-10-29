using Avalonia.Headless.XUnit;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Load stamp…: a picture that reads becomes a brush stamp named after its file (MainWindow.LoadStamp).
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowWirePanelsTests
{
    [AvaloniaFact]
    public void APictureThatReadsBecomesAStampNamedAfterItsFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-stamp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "Hill.png");
            using (var bmp = new SKBitmap(4, 4))
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        byte v = (byte)(60 * (x + y));
                        bmp.SetPixel(x, y, new SKColor(v, v, v));
                    }
                }
                using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(file, png.ToArray());
            }
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            int stamps = w.Tools.StampList.Count;

            w.LoadStamp(file);

            Assert.Equal(stamps + 1, w.Tools.StampList.Count);
            Assert.Contains(w.Tools.StampList, s => s.Label == "Hill" && s.Loaded);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
