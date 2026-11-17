using System.Text.Json;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The pictures: the window saved as a JPEG, and the locations note closed before it is taken.
/// </summary>
public sealed partial class Editor
{
    /// <summary>
    /// The pictures' JPEG quality, 0 to 100: $DOCS_QUALITY, else 85. The translations' pictures are taken lower (every
    /// language's set is carried in the app).
    /// </summary>
    /// <exception cref="InvalidOperationException">$DOCS_QUALITY is not a number from 0 to 100.</exception>
    public static int Quality { get; } = Environment.GetEnvironmentVariable("DOCS_QUALITY") is { Length: > 0 } q
        ? int.TryParse(q, System.Globalization.CultureInfo.InvariantCulture, out int n) && n is >= 0 and <= 100 ? n : throw new InvalidOperationException($"DOCS_QUALITY={q}: a JPEG quality from 0 to 100")
        : 85;

    /// <summary>
    /// The pictures are WebP files instead of JPEGs ($DOCS_FORMAT=webp): each scene's x.jpg is written as x.webp. The
    /// translations' pictures are, being carried in the app for every language (about a third smaller).
    /// </summary>
    /// <exception cref="InvalidOperationException">$DOCS_FORMAT is neither jpg nor webp.</exception>
    public static bool WebP { get; } = Environment.GetEnvironmentVariable("DOCS_FORMAT") switch
    {
        null or "" or "jpg" => false,
        "webp" => true,
        var f => throw new InvalidOperationException($"DOCS_FORMAT={f}: jpg or webp"),
    };

    /// <summary>A picture encoded for the docs: WebP for a .webp file, else JPEG, at <see cref="Quality"/>.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="path">The file it goes to.</param>
    public static SKData Encode(SKBitmap picture, string path) =>
        picture.Encode(path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? SKEncodedImageFormat.Webp : SKEncodedImageFormat.Jpeg, Quality)
        ?? throw new InvalidOperationException($"{path}: the picture could not be encoded");

    /// <summary>
    /// The whole window, saved for the docs (JPEG, or WebP for a .webp file) at <see cref="Quality"/> (the status bar's message cleared unless
    /// kept: it would tell how the scene was set up).
    /// </summary>
    /// <param name="path">The JPEG file to write.</param>
    /// <param name="keepMessage">Keep the status bar's message in the picture.</param>
    /// <returns>The app's state when the picture was taken.</returns>
    public JsonElement Shot(string path, bool keepMessage = false)
    {
        if (!keepMessage)
        {
            Send("message");
        }
        string png = Path.Combine(_home, "shot.png");
        var state = Send($"shot {png}");
        LogClipped(path);
        using var shot = SKBitmap.Decode(png) ?? throw new InvalidOperationException($"{png}: not a picture");
        var img = shot;
        // At the window's own size (the screen's scaling makes the snapshot bigger).
        if (img.Width > _width)
        {
            var size = new SKImageInfo(_width, (int)Math.Round(img.Height * (double)_width / img.Width), SKColorType.Rgba8888, SKAlphaType.Opaque);
            img = shot.Resize(size, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                ?? throw new InvalidOperationException($"{png}: could not be resized");
        }
        try
        {
            using var jpeg = Encode(img, path);
            using var file = File.Create(path);
            jpeg.SaveTo(file);
        }
        finally
        {
            if (img != shot)
            {
                img.Dispose();
            }
        }
        return state;
    }

    /// <summary>
    /// Closes the 3D editor's locations note (shown when locations lie near the area), wherever the window puts it,
    /// with the driver's note command (as its × does).
    /// </summary>
    public void CloseLocationNote()
    {
        Send("wait 300");
        Send("note close");
    }
}
