using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>Reading, cutting and writing the pictures the scenes put together (halves, insets, labels, crops).</summary>
public sealed partial class Scenes
{
    /// <summary>Pixels copied as they are, without smoothing.</summary>
    private static readonly SKSamplingOptions Exact = new(SKFilterMode.Nearest);

    /// <summary>A picture file, read; throws when it is not a picture.</summary>
    private static SKImage LoadPicture(string file) => SKImage.FromEncodedData(file) ?? throw new InvalidOperationException($"{file}: not a picture");

    /// <summary>A picture file, read to be drawn on; throws when it is not a picture.</summary>
    private static SKBitmap LoadBitmap(string file) => SKBitmap.Decode(file) ?? throw new InvalidOperationException($"{file}: not a picture");

    /// <summary>A picture saved as the editor's shots are (JPEG, or WebP for a .webp file) (written over the file).</summary>
    private static void SavePicture(SKBitmap picture, string path)
    {
        using var jpeg = Editor.Encode(picture, path);
        using var file = File.Create(path);
        jpeg.SaveTo(file);
    }

    /// <summary>A part of a picture saved as a picture of its own.</summary>
    private static void Crop(string from, string to, SKRectI part)
    {
        using var shot = LoadPicture(from);
        using var result = new SKBitmap(part.Width, part.Height);
        using (var canvas = new SKCanvas(result))
        {
            canvas.DrawImage(shot, part, new SKRect(0, 0, part.Width, part.Height), Exact);
        }
        SavePicture(result, to);
    }
}
