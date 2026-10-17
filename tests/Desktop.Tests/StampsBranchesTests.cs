using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Stamps' edges: sampling outside the picture on every side, a see-through picture, and the kept stamps' file with
/// good, wrong-sized and broken entries, missing, or in a place it cannot be written.
/// </summary>
[Collection("DataDir")]
public class StampsBranchesTests
{
    [Fact]
    public void OutsideThePictureIsZeroOnEverySide()
    {
        float[] full = [.. Enumerable.Repeat(1f, Stamps.Size * Stamps.Size)];
        Assert.Equal(0, Stamps.Sample(full, -1, 0));
        Assert.Equal(0, Stamps.Sample(full, 1, 0));
        Assert.Equal(0, Stamps.Sample(full, 0, -1));
        Assert.Equal(0, Stamps.Sample(full, 0, 1));
        Assert.Equal(1, Stamps.Sample(full, 0.999f, -0.999f), 4);
        Assert.Equal(1, Stamps.Sample(full, -0.999f, 0.999f), 4);
    }

    [Fact]
    public void ASeeThroughPictureWeighsNothing()
    {
        using var bmp = new SKBitmap(16, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bmp.Erase(new SKColor(255, 255, 255, 0));
        using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        float[] data = Stamps.FromPicture(png.ToArray())!;
        Assert.Equal(Stamps.Size * Stamps.Size, data.Length);
        Assert.All(data, v => Assert.Equal(0, v, 3));
    }

    [Fact]
    public void TheKeptFileKeepsGoodStampsOnly()
    {
        using var kept = new KeptStamps("sub");
        string dir = kept.Dir;
        // No file yet: none.
        Assert.Empty(Stamps.LoadKept());
        // Written (its folder made) and read back, the weights rounded to bytes.
        float[] half = [.. Enumerable.Repeat(0.5f, Stamps.Size * Stamps.Size)];
        Stamps.SaveKept([new Stamps.Stamp("pic:half", "Half", half, Loaded: true)]);
        var back = Assert.Single(Stamps.LoadKept());
        Assert.Equal("Half", back.Label);
        Assert.True(back.Loaded);
        Assert.Equal(128 / 255f, back.Data[0], 4);
        // A stamp of the wrong size is left out, the good one kept.
        File.WriteAllText(kept.FilePath,
            $"[{{\"Name\":\"pic:small\",\"Label\":\"Small\",\"Weights\":\"{Convert.ToBase64String(new byte[10])}\"}},"
            + $"{{\"Name\":\"pic:ok\",\"Label\":\"Ok\",\"Weights\":\"{Convert.ToBase64String(new byte[Stamps.Size * Stamps.Size])}\"}}]");
        Assert.Equal("Ok", Assert.Single(Stamps.LoadKept()).Label);
        // Weights that are not base64, and a file that is only "null": none.
        File.WriteAllText(kept.FilePath, "[{\"Name\":\"pic:bad\",\"Label\":\"Bad\",\"Weights\":\"not base64!\"}]");
        Assert.Empty(Stamps.LoadKept());
        File.WriteAllText(kept.FilePath, "null");
        Assert.Empty(Stamps.LoadKept());
        // A folder where the file should be: it cannot be written, nothing is thrown.
        Directory.CreateDirectory(Path.Combine(dir, "taken", "stamps.json"));
        Stamps.PathOverride = Path.Combine(dir, "taken", "stamps.json");
        Stamps.SaveKept([new Stamps.Stamp("pic:half", "Half", half, Loaded: true)]);
        Assert.Empty(Stamps.LoadKept());
        // Written whole through a temporary file, which never stays behind.
        Assert.Empty(Directory.GetFiles(Path.Combine(dir, "taken"), "*.tmp"));
    }
}
