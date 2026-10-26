using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Past the graphics-card budget, opening another area lets the models go: they are read again, and the area still
/// draws them (with their textures).
/// </summary>
[Collection("Visual budget")]
[Trait("Category", "Visual")]
public sealed class BudgetVisualTests(TinyBudgetEditorProcess editor)
{
    [Fact]
    public void ModelsLetGoAreReadAgain()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        DirectVisualTests.WaitForPage(editor, "editor");
        string dir = Path.Combine(Path.GetTempPath(), "vwe-visual-budget-pics-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            editor.Send("camera 40 20 30 50 70");
            editor.Send($"picture {Path.Combine(dir, "a.png")}");
            for (int n = 0; n < 2; n++)
            {
                editor.Send("area 0 0 2");
            }
            editor.Send("camera 40 20 30 50 70");
            editor.Send($"picture {Path.Combine(dir, "b.png")}");
            Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
            string log = File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));
            Assert.Contains("Models let go from the graphics card", log);
            using var a = SKBitmap.Decode(Path.Combine(dir, "a.png"));
            using var b = SKBitmap.Decode(Path.Combine(dir, "b.png"));
            Assert.Equal(Mean(a), Mean(b), 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// A picture's average brightness (luminance), every fourth pixel each way.
    /// </summary>
    private static double Mean(SKBitmap bmp)
    {
        double sum = 0;
        int n = 0;
        for (int y = 0; y < bmp.Height; y += 4)
        {
            for (int x = 0; x < bmp.Width; x += 4)
            {
                var c = bmp.GetPixel(x, y);
                sum += 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
                n++;
            }
        }
        return sum / n / 8;
    }
}
