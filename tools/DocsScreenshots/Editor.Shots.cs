using System.Text.Json;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The pictures: the window saved as a JPEG, and the locations note closed before it is taken.
/// </summary>
public sealed partial class Editor
{
    /// <summary>
    /// The whole window, saved as a JPEG for the docs (the status bar's message cleared unless kept: it would tell
    /// how the scene was set up).
    /// </summary>
    /// <param name="path">The JPEG file to write.</param>
    /// <param name="quality">JPEG quality, 0 to 100.</param>
    /// <param name="keepMessage">Keep the status bar's message in the picture.</param>
    /// <returns>The app's state when the picture was taken.</returns>
    public JsonElement Shot(string path, int quality = 85, bool keepMessage = false)
    {
        if (!keepMessage)
        {
            Send("message");
        }
        string png = Path.Combine(_home, "shot.png");
        var state = Send($"shot {png}");
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
            using var jpeg = img.Encode(SKEncodedImageFormat.Jpeg, quality);
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
    /// Closes the 3D editor's locations note (bottom left, shown when locations lie near the area): its close button
    /// is only an icon, which the driver's click cannot name, so it is clicked on the display with xdotool. The pointer
    /// is then moved off the window, where it cannot touch the scene. Does nothing without xdotool.
    /// </summary>
    public void CloseLocationNote()
    {
        Send("wait 300");
        string? window = GameWindow();
        if (window == null)
        {
            return;
        }
        // The note is 420 pixels wide at most (its text always wraps to three lines), 10 from the left and 58 from
        // the bottom; its close button is at its right end, halfway up.
        string height = Xdotool("getwindowgeometry", "--shell", window)?.Split('\n').FirstOrDefault(l => l.StartsWith("HEIGHT=", StringComparison.Ordinal))?[7..] ?? "900";
        int y = int.Parse(height, System.Globalization.CultureInfo.InvariantCulture) - 89;
        Xdotool("mousemove", "--window", window, "408", y.ToString(System.Globalization.CultureInfo.InvariantCulture), "click", "1");
        Xdotool("mousemove", "1915", "1075");
        Send("wait 200");
    }
}
