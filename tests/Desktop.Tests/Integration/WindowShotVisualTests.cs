using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A window shot with a dialog open (WindowShot): the dialog is drawn over the window, which is dimmed behind it.
/// </summary>
[Collection("Visual game look")]
[Trait("Category", "Visual")]
public sealed class WindowShotVisualTests(GameLookEditorProcess editor) : VisualPicturesTestBase("shot")
{
    [Fact]
    public void AnOpenDialogIsDrawnOverTheDimmedWindow()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Directory.CreateDirectory(Dir);
        GameLookVisualTests.BackToStart(editor);
        string alone = Path.Combine(Dir, "alone.png"), withDialog = Path.Combine(Dir, "dialog.png");
        editor.Send($"shot {alone}");
        editor.Send("click Settings");
        // Shown, and drawn once.
        editor.Send("wait 1000");
        try
        {
            editor.Send($"shot {withDialog}");
        }
        finally
        {
            editor.Send("click Cancel");
        }
        using var a = SKBitmap.Decode(alone);
        using var b = SKBitmap.Decode(withDialog);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        // The corner, where the dialog is not, is dimmed.
        double before = Brightness(a, 4, 4), after = Brightness(b, 4, 4);
        Assert.True(after < before, $"corner {after:0.0} with the dialog, {before:0.0} without");
    }

    /// <summary>
    /// The average brightness (luminance) of the 20 × 20 block from x, y.
    /// </summary>
    private static double Brightness(SKBitmap bmp, int x0, int y0)
    {
        double sum = 0;
        for (int y = y0; y < y0 + 20; y++)
        {
            for (int x = x0; x < x0 + 20; x++)
            {
                var c = bmp.GetPixel(x, y);
                sum += 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
            }
        }
        return sum / 400;
    }
}
