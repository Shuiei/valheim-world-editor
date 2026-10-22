using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Pictures put together after the shots: Settings' dialog shown at two places side by side, and its secrets written
/// over.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>
    /// The part of a shot around the green unsaved dot nearest its middle (a tree's foot and its trunk), for an inset
    /// three times as large: 87 x 73 pixels, the dot low in the middle.
    /// </summary>
    /// <param name="path">The shot.</param>
    /// <returns>The part to show.</returns>
    private static SKRectI NearestDot(string path)
    {
        using var shot = LoadBitmap(path);
        var best = (X: -1, Y: -1);
        double nearest = double.MaxValue;
        for (int y = 80; y < shot.Height - 80; y++)
        {
            for (int x = 120; x < shot.Width - 120; x++)
            {
                var c = shot.GetPixel(x, y);
                double d = Math.Pow(x - (shot.Width / 2), 2) + Math.Pow(y - (shot.Height / 2), 2);
                if (c.Green > 200 && c.Red < 170 && c.Blue > 120 && d < nearest)
                {
                    (best, nearest) = ((x, y), d);
                }
            }
        }
        return best.X < 0 ? throw new InvalidOperationException($"{path}: no green dot") : new SKRectI(best.X - 43, best.Y - 52, best.X + 44, best.Y + 21);
    }

    /// <summary>The first found seed's picture on the new world page (1440 x 1100), its Find a seed card scrolled up.</summary>
    private static readonly (int X, int Y) FirstFoundSeed = (103, 800);

    /// <summary>Settings' dialog in a 1440 x 900 shot: 720 x 760, centred, 70 pixels from the top.</summary>
    private static readonly SKRectI SettingsDialog = new(360, 70, 1080, 830);

    /// <summary>The wheel notches (50 pixels each) that bring Settings' Claude heading to the dialog's top.</summary>
    private const int ClaudeNotches = 10;

    /// <summary>
    /// What is written over the shot scrolled to the Claude section: the Claude Code command's token, and the program's
    /// path in Claude Desktop's lines (the stand-in's own, dotnet's, as a player has it). In the boxes' monospace font,
    /// 7.2 pixels a letter, 16 a line.
    /// </summary>
    private static readonly Retyped[] ClaudeSecrets =
    [
        new(new SKRectI(554, 333, 925, 350), 554.6f, 346, "<token>\""),
        new(new SKRectI(511, 457, 900, 474), 511.4f, 470, "\"/home/you/ValheimWorldEditor/ValheimWorldEditor\","),
    ];

    /// <summary>The same in the shot of Settings' end, Claude on (both boxes are higher there).</summary>
    private static readonly Retyped[] EndSecrets =
    [
        new(new SKRectI(554, 114, 925, 131), 554.6f, 127, "<token>\""),
        new(new SKRectI(511, 238, 900, 255), 511.4f, 251, "\"/home/you/ValheimWorldEditor/ValheimWorldEditor\","),
    ];

    /// <summary>A text written over a shot.</summary>
    /// <param name="Cover">The part covered first, with the colour at its top right corner (the box's own, past its text).</param>
    /// <param name="X">Where the text starts.</param>
    /// <param name="Baseline">The text's baseline.</param>
    /// <param name="Text">The text.</param>
    private readonly record struct Retyped(SKRectI Cover, float X, float Baseline, string Text);

    /// <summary>
    /// Settings' dialog from two shots, side by side in one picture (1440 x 760): the dialog shows only part of its
    /// sections at once.
    /// </summary>
    /// <param name="path">The picture to write.</param>
    /// <param name="left">The shot whose dialog goes left.</param>
    /// <param name="right">The shot whose dialog goes right.</param>
    private static void SideBySide(string path, string left, string right)
    {
        var d = SettingsDialog;
        using var picture = new SKBitmap(d.Width * 2, d.Height);
        using (var canvas = new SKCanvas(picture))
        {
            foreach (var (shot, x) in new[] { (left, 0), (right, d.Width) })
            {
                using var bitmap = LoadBitmap(shot);
                using var image = SKImage.FromBitmap(bitmap);
                canvas.DrawImage(image, d, SKRect.Create(x, 0, d.Width, d.Height), new SKSamplingOptions(SKFilterMode.Nearest));
            }
        }
        SavePicture(picture, path);
    }

    /// <summary>Writes texts over a shot, in the text boxes' monospace font (12 pixels) and colour.</summary>
    /// <param name="path">The shot, rewritten.</param>
    /// <param name="edits">The texts.</param>
    private static void Retext(string path, Retyped[] edits)
    {
        using var picture = LoadBitmap(path);
        using (var canvas = new SKCanvas(picture))
        using (var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans Mono"), 12))
        using (var ink = new SKPaint { Color = new SKColor(238, 242, 248), IsAntialias = true })
        {
            foreach (var edit in edits)
            {
                using var fill = new SKPaint { Color = picture.GetPixel(edit.Cover.Right - 1, edit.Cover.Top + 1) };
                canvas.DrawRect(edit.Cover, fill);
                canvas.DrawText(edit.Text, edit.X, edit.Baseline, SKTextAlign.Left, font, ink);
            }
        }
        SavePicture(picture, path);
    }

    /// <summary>
    /// concept-area-zones.jpg's drawing over the shot (the 5 x 5 area straight down, about 1.9 pixels a metre): the
    /// middle zone outlined, the area's outermost metre as a bright band (the locked edge), a dashed line 12 m inside
    /// (where Follow grows the area), and their names.
    /// </summary>
    /// <param name="path">The shot, rewritten.</param>
    private static void AnnotateAreaZones(string path)
    {
        // The area's corners on the shot (the ground's edge), and 12 m (23 pixels) inside them.
        SKPoint[] edge = [new(409, 146), new(1011, 158), new(1027, 762), new(405, 762)];
        SKPoint[] follow = [new(432, 169), new(988, 181), new(1004, 739), new(428, 739)];
        using var picture = LoadBitmap(path);
        using (var canvas = new SKCanvas(picture))
        {
            using var band = new SKPaint { Color = new SKColor(255, 214, 64), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            using var dashes = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true, PathEffect = SKPathEffect.CreateDash([10, 7], 0) };
            using var zone = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
            canvas.DrawPoints(SKPointMode.Polygon, [.. edge, edge[0]], band);
            canvas.DrawPoints(SKPointMode.Polygon, [.. follow, follow[0]], dashes);
            canvas.DrawRect(new SKRect(658, 389, 780, 512), zone);
            canvas.DrawLine(1014, 260, 1090, 260, band);
            canvas.DrawLine(992, 420, 1092, 420, zone);
            Tag(canvas, "zone, 64 m", 719, 456);
            Tag(canvas, "the area: 5 × 5 zones, 320 m", 716, 122);
            Tag(canvas, "locked edge", 1150, 266);
            Tag(canvas, "Follow grows the area past here", 1240, 426);
        }
        SavePicture(picture, path);
    }

    /// <summary>A name written on a picture: white on a dark rounded box, centred on the point.</summary>
    /// <param name="canvas">The picture's canvas.</param>
    /// <param name="text">The name.</param>
    /// <param name="x">Its middle, from the left.</param>
    /// <param name="y">Its baseline, from the top.</param>
    private static void Tag(SKCanvas canvas, string text, float x, float y)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 17);
        using var box = new SKPaint { Color = new SKColor(20, 24, 30, 220), IsAntialias = true };
        using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
        float width = font.MeasureText(text);
        canvas.DrawRoundRect(new SKRect(x - (width / 2) - 8, y - 17, x + (width / 2) + 8, y + 7), 6, 6, box);
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, ink);
    }
}
