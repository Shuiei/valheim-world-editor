namespace TerrainEditor.Desktop;

/// <summary>
/// The area's map drawn from its data (DrawMap): heights in colour, paint, water, objects as dots, and a labelled grid.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The area's map, north up: heights in colour with hill shading, paint, water, objects as dots by kind, and a grid
    /// labelled in world metres, as a JPEG.
    /// </summary>
    /// <param name="w">The grid's width (points, one per metre).</param>
    /// <param name="h">The grid's height.</param>
    /// <param name="cx">World x of the grid's middle.</param>
    /// <param name="cz">World z of the grid's middle.</param>
    /// <param name="water">The water level, in metres.</param>
    /// <param name="heights">The ground heights, row by row from the south.</param>
    /// <param name="paint">Dirt, cultivated and paved weights per point (three each), null: none.</param>
    /// <param name="things">The objects: world x, z and kind.</param>
    /// <param name="grid">The grid lines' spacing, in metres.</param>
    internal static byte[] DrawMap(int w, int h, float cx, float cz, float water, float[] heights, float[]? paint, List<(float X, float Z, string Kind)> things, float grid)
    {
        const int Margin = 40;
        float k = MathF.Min(12, 760f / MathF.Max(w, h));
        int pw = (int)(w * k), ph = (int)(h * k);
        float lo = heights.Min(), hi = MathF.Max(heights.Max(), lo + 1);
        using var ground = new SkiaSharp.SKBitmap(w, h);
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                float y = heights[j * w + i];
                // Hill shading: lit from the north-west.
                float dx = heights[j * w + Math.Min(w - 1, i + 1)] - heights[j * w + Math.Max(0, i - 1)];
                float dz = heights[Math.Min(h - 1, j + 1) * w + i] - heights[Math.Max(0, j - 1) * w + i];
                float light = Math.Clamp(0.8f + (dz - dx) * 0.12f, 0.45f, 1.2f);
                SkiaSharp.SKColor c;
                if (y < water)
                {
                    float d = Math.Clamp((water - y) / 20, 0, 1);
                    c = new SkiaSharp.SKColor((byte)(40 - 20 * d), (byte)(110 - 50 * d), (byte)(170 - 40 * d));
                }
                else
                {
                    float t = Math.Clamp((y - lo) / (hi - lo), 0, 1);
                    var (r, g, b) = t < 0.5f ? (90 + 120 * t, 140 + 40 * t, 70 + 20 * t) : (150 + 200 * (t - 0.5f), 160 + 160 * (t - 0.5f), 80 + 300 * (t - 0.5f));
                    if (paint != null)
                    {
                        // Painted ground over it: dirt brown, cultivated dark, paved grey.
                        int q = (j * w + i) * 3;
                        float dirt = Math.Clamp(paint[q], 0, 1), tilled = Math.Clamp(paint[q + 1], 0, 1), paved = Math.Clamp(paint[q + 2], 0, 1);
                        (r, g, b) = (r + (125 - r) * dirt, g + (95 - g) * dirt, b + (60 - b) * dirt);
                        (r, g, b) = (r + (85 - r) * tilled, g + (60 - g) * tilled, b + (40 - b) * tilled);
                        (r, g, b) = (r + (160 - r) * paved, g + (158 - g) * paved, b + (150 - b) * paved);
                    }
                    c = new SkiaSharp.SKColor((byte)Math.Clamp(r * light, 0, 255), (byte)Math.Clamp(g * light, 0, 255), (byte)Math.Clamp(b * light, 0, 255));
                }
                // North up: the last row of the grid (largest z) at the top.
                ground.SetPixel(i, h - 1 - j, c);
            }
        }
        using var sheet = new SkiaSharp.SKBitmap(pw + 2 * Margin, ph + 2 * Margin);
        using (var canvas = new SkiaSharp.SKCanvas(sheet))
        {
            canvas.Clear(new SkiaSharp.SKColor(24, 28, 34));
            using (var groundImage = SkiaSharp.SKImage.FromBitmap(ground))
            {
                canvas.DrawImage(groundImage, SkiaSharp.SKRect.Create(Margin, Margin, pw, ph), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear));
            }
            float minX = cx - (w - 1) / 2f, minZ = cz - (h - 1) / 2f;
            float Px(float x) => Margin + (x - minX) * k;
            float Py(float z) => Margin + ph - (z - minZ) * k;
            using var dot = new SkiaSharp.SKPaint { IsAntialias = true };
            foreach (var (x, z, kind) in things)
            {
                dot.Color = kind switch { "Trees" => new SkiaSharp.SKColor(30, 90, 30), "Rocks" or "Ore" => new SkiaSharp.SKColor(130, 130, 130), "Buildings" => new SkiaSharp.SKColor(240, 150, 40), _ => SkiaSharp.SKColors.White };
                canvas.DrawCircle(Px(x), Py(z), kind == "Buildings" ? 2.2f : 1.8f, dot);
            }
            using var line = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(255, 255, 255, 70), StrokeWidth = 1 };
            using var ink = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(220, 220, 220), IsAntialias = true };
            using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 11);
            for (float gx = MathF.Ceiling(minX / grid) * grid; gx <= minX + w - 1; gx += grid)
            {
                canvas.DrawLine(Px(gx), Margin, Px(gx), Margin + ph, line);
                canvas.DrawText($"{gx:0}", Px(gx), Margin + ph + 14, SkiaSharp.SKTextAlign.Center, font, ink);
            }
            for (float gz = MathF.Ceiling(minZ / grid) * grid; gz <= minZ + h - 1; gz += grid)
            {
                canvas.DrawLine(Margin, Py(gz), Margin + pw, Py(gz), line);
                canvas.DrawText($"{gz:0}", Margin - 4, Py(gz) + 4, SkiaSharp.SKTextAlign.Right, font, ink);
            }
            canvas.DrawText("north up; x left to right, z bottom to top; paint: paved grey, dirt brown, cultivated dark", Margin, Margin - 12, SkiaSharp.SKTextAlign.Left, font, ink);
        }
        using var image = SkiaSharp.SKImage.FromBitmap(sheet);
        return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85).ToArray();
    }
}
