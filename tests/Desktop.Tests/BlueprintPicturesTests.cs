using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Blueprints' pictures decoded once: a picture file again while it is unchanged, read again once it changes; a data
/// URL once per text; what cannot be read is no picture.
/// </summary>
public class BlueprintPicturesTests
{
    /// <summary>A blank PNG of this size.</summary>
    private static byte[] Blank(int size)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var ms = new MemoryStream();
        bitmap.Save(ms, PngBitmapEncoderOptions.Default);
        return ms.ToArray();
    }

    [AvaloniaFact]
    public void APictureIsDecodedAgainOnlyOnceItChanges()
    {
        string dir = Directory.CreateTempSubdirectory("vwe-pictures").FullName;
        try
        {
            string file = Path.Combine(dir, "a.png");
            File.WriteAllBytes(file, Blank(4));
            var first = BlueprintPictures.File(file);
            Assert.NotNull(first);
            Assert.Same(first, BlueprintPictures.File(file));
            File.WriteAllBytes(file, Blank(2));
            File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));
            var changed = BlueprintPictures.File(file);
            // Decoded again (the headless platform's bitmaps carry no real size to compare).
            Assert.NotNull(changed);
            Assert.NotSame(first, changed);
            // A file that cannot be read (gone; the headless platform's bitmaps take any bytes, so a broken picture
            // cannot stand in for it here).
            Assert.Null(BlueprintPictures.File(Path.Combine(dir, "gone.png")));
            Assert.Null(BlueprintPictures.File(null));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ADataUrlIsDecodedOncePerText()
    {
        string url = "data:image/png;base64," + Convert.ToBase64String(Blank(3));
        var first = BlueprintPictures.Url(url);
        Assert.NotNull(first);
        Assert.Same(first, BlueprintPictures.Url(url));
        Assert.Null(BlueprintPictures.Url("data:image/png;base64,not-base64!!"));
        Assert.Null(BlueprintPictures.Url("data:image/jpeg;base64,AAAA"));
    }
}
