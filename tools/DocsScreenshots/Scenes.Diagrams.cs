using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The diagrams drawn without the app (Program.cs starts no editor for them), in the app's dark colours (editor/Desktop/Ui.cs).
/// </summary>
public sealed partial class Scenes
{
    /// <summary>The app's colours (Ui.cs): background, panel, text, muted text, accent, warning, ok.</summary>
    private static readonly SKColor Bg = SKColor.Parse("#14171c"), Panel2 = SKColor.Parse("#1e232b"),
        Ink = SKColor.Parse("#e6e9ee"), Muted = SKColor.Parse("#8e97a6"), Accent = SKColor.Parse("#e0a64b"), Warn = SKColor.Parse("#e0604b"),
        Ok = SKColor.Parse("#7bc47f");

    /// <summary>A ground disc (editor/Core/Editing/Uplift.cs): flat radius, blend reach, grid spacing (metres).</summary>
    private const double DiscFlat = 8, DiscReach = 12.96, DiscSpacing = 6;

    /// <summary>A grid point needs a disc when the ground asked for is further than this from the generated ground (Uplift.Need).</summary>
    private const double DiscNeed = 5;

    /// <summary>How far ordinary ground edits go from the ground under them (the game's limit, metres).</summary>
    private const double EditLimit = 8;

    /// <summary>The cross-section's ends (metres) and the hill's height above the generated ground.</summary>
    private const double SectionFrom = -64, SectionTo = 64, HillHeight = 30;

    /// <summary>The picture's scale (pixels per metre, the same both ways), left edge and generated ground line (pixels).</summary>
    private const float PerMetre = 10.5f, SectionLeft = 48, GroundLine = 690;

    /// <summary>Points along the cross-section, every quarter metre.</summary>
    private const double SectionStep = 0.25;

    /// <summary>
    /// concept-ground-discs.jpg: a cross-section of a hill raised 30 m with No limit, as saved. The generated ground; the
    /// discs on their 6 m grid, only where the hill is more than 5 m up, each one's height the ground asked for over its
    /// share of the grid, so their flat tops make terraces under the surface (drawn simply: each point takes its nearest
    /// disc, the outer discs level 8 m out and blend to 13 m); one disc alone on the generated ground, as the game
    /// lays it; and the ordinary edits, within ±8 m of the disc ground, that make up the final surface.
    /// </summary>
    private void GroundDiscs()
    {
        double[] xs = [.. Enumerable.Range(0, (int)((SectionTo - SectionFrom) / SectionStep) + 1).Select(i => SectionFrom + i * SectionStep)];
        double[] generated = [.. xs.Select(Generated)];
        double[] wanted = [.. xs.Select(x => Generated(x) + Hill(x))];
        double[] centres = [.. Enumerable.Range(-10, 21).Select(i => i * DiscSpacing).Where(c => Hill(c) > DiscNeed)];
        double[] heights = [.. centres.Select(c => xs.Where(x => Math.Abs(x - c) <= DiscSpacing / 2).Average(x => Generated(x) + Hill(x)))];
        double[] discGround = [.. xs.Select(x => DiscGround(x, centres, heights))];
        int chosen = Array.IndexOf(centres, -24.0);
        double c = centres[chosen], h = heights[chosen];

        using var picture = new SKBitmap(1440, 900);
        using var canvas = new SKCanvas(picture);
        canvas.Clear(Bg);
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // The chosen disc's reach, behind everything: its flat circle and its blend on either side.
        float top = MeasureTop - 52;
        fill.Color = Warn.WithAlpha(24);
        canvas.DrawRect(new SKRect(Sx(c - DiscReach), top, Sx(c - DiscFlat), GroundLine + 30), fill);
        canvas.DrawRect(new SKRect(Sx(c + DiscFlat), top, Sx(c + DiscReach), GroundLine + 30), fill);
        fill.Color = Warn.WithAlpha(46);
        canvas.DrawRect(new SKRect(Sx(c - DiscFlat), top, Sx(c + DiscFlat), GroundLine + 30), fill);

        // The ground: everything under the final surface, the part the discs raise, and the edits between the disc
        // ground and the final surface.
        fill.Color = Panel2;
        Draw(canvas, Band(xs, wanted, null), fill, close: true);
        fill.Color = Accent.WithAlpha(36);
        Draw(canvas, Band(xs, discGround, generated), fill, close: true);
        fill.Color = Ok.WithAlpha(150);
        Draw(canvas, Band(xs, wanted, discGround), fill, close: true);

        // The ±8 m of ordinary edits around the disc ground, kept off the key below.
        using (var band = Stroke(Ok.WithAlpha(150), 1.5f, dashed: true))
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, 1440, 760));
            Draw(canvas, Line(xs, [.. discGround.Select(g => g + EditLimit)]), band);
            Draw(canvas, Line(xs, [.. discGround.Select(g => g - EditLimit)]), band);
            canvas.Restore();
        }

        // The lines: generated ground, disc ground, the chosen disc alone, the final surface.
        using (var seed = Stroke(Muted, 2f, dashed: true))
        {
            Draw(canvas, Line(xs, generated), seed);
        }
        using (var ground = Stroke(Accent, 3f))
        {
            Draw(canvas, Line(xs, discGround), ground);
        }
        double[] near = [.. xs.Where(x => Math.Abs(x - c) <= DiscReach)];
        using (var disc = Stroke(Warn, 3.5f))
        {
            Draw(canvas, Line(near, [.. near.Select(x => Alone(x, c, h))]), disc);
        }
        using (var surface = Stroke(Ink, 3.5f))
        {
            Draw(canvas, Line(xs, wanted), surface);
        }

        // Each disc's middle: a mark on its top.
        for (int i = 0; i < centres.Length; i++)
        {
            fill.Color = i == chosen ? Warn : Accent;
            Draw(canvas, Marker(Sx(centres[i]), Sy(heights[i])), fill, close: true);
        }

        DiscLabels(canvas, c, h, centres, heights);
        SavePicture(picture, Out("concept-ground-discs.jpg"));
    }

    /// <summary>Where the measures of the chosen disc go (pixels from the top).</summary>
    private const float MeasureTop = 222;

    /// <summary>The words on the diagram: title, measures, what each line is.</summary>
    private static void DiscLabels(SKCanvas canvas, double c, double cTop, double[] centres, double[] heights)
    {
        using var title = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 28);
        using var bold = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 19);
        using var body = new SKFont(SKTypeface.FromFamilyName("Noto Sans"), 18);
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText("How No limit ground is saved: ground discs", 48, 60, SKTextAlign.Left, title, ink);
        ink.Color = Muted;
        canvas.DrawText("A cross-section through a hill raised 30 m, heights and distances to the same scale.", 48, 92, SKTextAlign.Left, body, ink);

        // The chosen disc: its flat circle and its blend, measured from its middle, with guides down to it.
        float y1 = MeasureTop, y2 = MeasureTop + 34;
        Measure(canvas, Sx(c), Sx(c + DiscFlat), y1, "8 m", Warn, bold);
        Measure(canvas, Sx(c - DiscReach), Sx(c), y2, "13 m", Warn, bold);
        using (var guide = Stroke(Warn.WithAlpha(170), 1.3f, dashed: true))
        {
            canvas.DrawLine(Sx(c), y1, Sx(c), Sy(cTop) - 14, guide);
            canvas.DrawLine(Sx(c + DiscFlat), y1, Sx(c + DiscFlat), Sy(cTop), guide);
            canvas.DrawLine(Sx(c - DiscReach), y2, Sx(c - DiscReach), Sy(Generated(c - DiscReach)), guide);
        }
        ink.Color = Warn;
        canvas.DrawText("One disc, alone on the generated ground: it levels a flat", Sx(c - DiscReach), MeasureTop - 88, SKTextAlign.Left, bold, ink);
        canvas.DrawText("circle of 8 m radius to its height and blends out to 13 m", Sx(c - DiscReach), MeasureTop - 62, SKTextAlign.Left, bold, ink);

        // The spacing of two discs at the top.
        int a = Array.FindIndex(centres, x => x >= 0);
        Measure(canvas, Sx(centres[a]), Sx(centres[a + 1]), Sy(Math.Max(heights[a], heights[a + 1])) - 26, "6 m", Accent, bold);

        // The hill's height, right of it, with a guide from its top.
        double baseHeight = Generated(-2);
        float xh = Sx(50);
        using (var guide = Stroke(Ink.WithAlpha(150), 1.3f, dashed: true))
        {
            canvas.DrawLine(Sx(8), Sy(baseHeight + HillHeight), xh, Sy(baseHeight + HillHeight), guide);
        }
        Measure(canvas, Sy(baseHeight), Sy(baseHeight + HillHeight), xh, "30 m", Ink, bold, vertical: true);

        // A grid point at the hill's foot: no disc.
        ink.Color = Muted;
        canvas.DrawText("No disc where the hill is", 48, 470, SKTextAlign.Left, body, ink);
        canvas.DrawText("under 5 m up", 48, 494, SKTextAlign.Left, body, ink);
        using (var leader = Stroke(Muted, 1.3f))
        {
            canvas.DrawLine(110, 504, Sx(-42), Sy(Generated(-42) + Hill(-42)) - 6, leader);
        }
        fill.Color = Ink;
        canvas.DrawCircle(Sx(-42), Sy(Generated(-42) + Hill(-42)), 5, fill);

        // What each line or colour is.
        Key(canvas, 48, 792, Ink, "Your final surface", line: true, bold);
        Key(canvas, 48, 832, Accent, "Disc ground: the game counts it as generated", line: true, bold);
        Key(canvas, 48, 872, Muted, "Generated ground (from the seed)", line: true, bold, dashed: true);
        Key(canvas, 760, 792, Ok.WithAlpha(220), "Ordinary ground edits make up the rest", line: false, bold);
        Key(canvas, 760, 832, Ok, "\u00b18 m from the disc ground: as far as edits go", line: true, bold, dashed: true);
        Key(canvas, 760, 872, Accent, "A disc's middle, on a 6 m grid", line: false, bold, marker: true);
    }

    /// <summary>One line of the key: a sample of the line, mark or colour, then its words.</summary>
    private static void Key(SKCanvas canvas, float x, float y, SKColor color, string text, bool line, SKFont font, bool dashed = false, bool marker = false)
    {
        using var paint = Stroke(color, 3.5f, dashed);
        if (marker)
        {
            paint.Style = SKPaintStyle.Fill;
            Draw(canvas, Marker(x + 24, y - 6), paint, close: true);
        }
        else if (line)
        {
            canvas.DrawLine(x, y - 7, x + 48, y - 7, paint);
        }
        else
        {
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(new SKRect(x, y - 18, x + 48, y + 3), 4, 4, paint);
        }
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        canvas.DrawText(text, x + 64, y, SKTextAlign.Left, font, ink);
    }

    /// <summary>A measure: a line with ends between a and b (across, at y; or upright, at x), its value over it.</summary>
    private static void Measure(SKCanvas canvas, float a, float b, float at, string text, SKColor color, SKFont font, bool vertical = false)
    {
        using var pen = Stroke(color, 2f);
        using var ink = new SKPaint { IsAntialias = true, Color = color };
        if (vertical)
        {
            canvas.DrawLine(at, a, at, b, pen);
            canvas.DrawLine(at - 8, a, at + 8, a, pen);
            canvas.DrawLine(at - 8, b, at + 8, b, pen);
            canvas.DrawText(text, at + 14, (a + b) / 2 + 7, SKTextAlign.Left, font, ink);
            return;
        }
        canvas.DrawLine(a, at, b, at, pen);
        canvas.DrawLine(a, at - 8, a, at + 8, pen);
        canvas.DrawLine(b, at - 8, b, at + 8, pen);
        canvas.DrawText(text, (a + b) / 2, at - 9, SKTextAlign.Center, font, ink);
    }

    /// <summary>
    /// The ground the discs make at x, drawn simply: the nearest disc's height; past the outermost discs, flat out to 8 m
    /// and blended out to 13 m on the generated ground.
    /// </summary>
    private static double DiscGround(double x, double[] centres, double[] heights)
    {
        int nearest = Enumerable.Range(0, centres.Length).MinBy(k => Math.Abs(x - centres[k]));
        return Math.Abs(x - centres[nearest]) <= DiscSpacing / 2 ? heights[nearest] : Alone(x, centres[nearest], heights[nearest]);
    }

    /// <summary>
    /// The ground a disc alone makes at x on the generated ground, as the game lays it: its height over its flat circle,
    /// then the generated ground pulled towards it by a share falling with the cube of the distance, out to its reach.
    /// </summary>
    private static double Alone(double x, double centre, double height)
    {
        double d = Math.Abs(x - centre), g = Generated(x);
        return d <= DiscFlat ? height : d <= DiscReach ? g + (height - g) * (1 - Math.Pow(d / DiscReach, 3)) : g;
    }

    /// <summary>The generated ground: gentle rises and dips (metres).</summary>
    private static double Generated(double x) => 1.4 * Math.Sin(x / 13 + 0.7) + 0.7 * Math.Sin(x / 5.3);

    /// <summary>The hill raised with No limit, above the generated ground: a flat top, steep sides, rough faces.</summary>
    private static double Hill(double x)
    {
        double d = Math.Abs(x + 2);
        double s = d < 12 ? 1 : d > 48 ? 0 : 0.5 * (1 + Math.Cos(Math.PI * (d - 12) / 36));
        return s * (HillHeight + 1.5 * Math.Sin(x / 3.1) + 0.8 * Math.Sin(x / 1.7 + 1));
    }

    /// <summary>A height on the picture.</summary>
    private static float Sy(double h) => (float)(GroundLine - h * PerMetre);

    /// <summary>A distance along the cross-section on the picture.</summary>
    private static float Sx(double x) => (float)(SectionLeft + (x - SectionFrom) * PerMetre);

    /// <summary>A line through the heights.</summary>
    private static SKPoint[] Line(double[] xs, double[] hs) => [.. xs.Select((x, i) => new SKPoint(Sx(x), Sy(hs[i])))];

    /// <summary>The area between two lines of heights (null: down to the picture's bottom).</summary>
    private static SKPoint[] Band(double[] xs, double[] upper, double[]? lower) =>
        lower == null
            ? [.. Line(xs, upper), new SKPoint(Sx(xs[^1]), 900), new SKPoint(Sx(xs[0]), 900)]
            : [.. Line(xs, upper), .. Line(xs, lower).Reverse()];

    /// <summary>A disc's middle: a small triangle pointing down onto its top.</summary>
    private static SKPoint[] Marker(float x, float y) => [new(x, y - 1), new(x - 7, y - 13), new(x + 7, y - 13)];

    /// <summary>Points drawn joined with the paint: a line, or a closed shape (filled when the paint fills).</summary>
    private static void Draw(SKCanvas canvas, SKPoint[] points, SKPaint paint, bool close = false)
    {
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, close);
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <summary>A pen: colour, width, solid or dashed.</summary>
    private static SKPaint Stroke(SKColor color, float width, bool dashed = false) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        Color = color,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        PathEffect = dashed ? SKPathEffect.CreateDash([9, 7], 0) : null,
    };
}
