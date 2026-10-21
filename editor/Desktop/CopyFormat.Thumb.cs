using SkiaSharp;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A small picture of a copy (CopyFormat.Thumb.cs).
/// </summary>
public static partial class CopyFormat
{
    /// <summary>
    /// A small picture of a copy seen from above (north up): the ground shaded by height, objects as dots
    /// coloured by kind (the web editor's drawThumb), as a PNG data URL.
    /// </summary>
    /// <param name="c">The copy.</param>
    /// <param name="size">The picture's width and height in pixels.</param>
    public static string Thumb(CopyData c, int size = 160)
    {
        using var bmp = new SKBitmap(size, size);
        using var g = new SKCanvas(bmp);
        g.Clear(new SKColor(0x1e, 0x23, 0x2b));
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        void Grow(float x, float z) { x0 = MathF.Min(x0, x); x1 = MathF.Max(x1, x); z0 = MathF.Min(z0, z); z1 = MathF.Max(z1, z); }
        foreach (var p in c.Poly)
        {
            Grow(p.X, p.Y);
        }

        foreach (var o in c.Objects)
        {
            Grow(o.Dx, o.Dz);
        }

        bool ground = c.Rel.Any(v => !float.IsNaN(v));
        if (ground)
        {
            Grow(-(c.W - 1) / 2f, -(c.H - 1) / 2f);
            Grow((c.W - 1) / 2f, (c.H - 1) / 2f);
        }
        if (x0 <= x1)
        {
            float span = MathF.Max(4, MathF.Max(x1 - x0, z1 - z0)) * 1.1f, mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
            float Sx(float x) => (x - mx) / span * size + size / 2f;
            float Sy(float z) => size / 2f - (z - mz) / span * size;
            if (ground)
            {
                float lo = c.Rel.Where(v => !float.IsNaN(v)).Min(), hi = c.Rel.Where(v => !float.IsNaN(v)).Max(), cell = MathF.Max(1, size / span);
                using var paint = new SKPaint();
                for (int iz = 0; iz < c.H; iz++)
                {
                    for (int ix = 0; ix < c.W; ix++)
                    {
                        float v = c.Rel[iz * c.W + ix];
                        if (float.IsNaN(v))
                        {
                            continue;
                        }
                        float t = hi > lo ? (v - lo) / (hi - lo) : 0.5f;
                        bool painted = c.Pnt[(iz * c.W + ix) * 4] >= 0;
                        paint.Color = painted ? new SKColor((byte)(150 + 40 * t), (byte)(120 + 30 * t), (byte)(80 + 20 * t)) : new SKColor((byte)(70 + 110 * t), (byte)(110 + 70 * t), (byte)(55 + 60 * t));
                        g.DrawRect(Sx(ix - (c.W - 1) / 2f) - cell / 2, Sy(iz - (c.H - 1) / 2f) - cell / 2, cell + 0.5f, cell + 0.5f, paint);
                    }
                }
            }
            if (c.Poly.Count > 0)
            {
                using var line = new SKPaint { Color = new SKColor(95, 212, 255, 204), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                using var path = new SKPath();
                path.MoveTo(Sx(c.Poly[0].X), Sy(c.Poly[0].Y));
                foreach (var p in c.Poly.Skip(1))
                {
                    path.LineTo(Sx(p.X), Sy(p.Y));
                }
                path.Close();
                g.DrawPath(path, line);
            }
            float r = Math.Clamp(size / span * 0.6f, 1.5f, 4);
            using var dot = new SKPaint { IsAntialias = true };
            foreach (var o in c.Objects)
            {
                dot.Color = ObjectKinds.Of(o.Name, false) switch
                {
                    ObjectKind.Ruins => SKColor.Parse("#a08cc8"),
                    ObjectKind.Trees => SKColor.Parse("#2f6b2a"),
                    ObjectKind.Rocks => SKColor.Parse("#9a9a9a"),
                    ObjectKind.Ore => SKColor.Parse("#e0803a"),
                    ObjectKind.Bushes => SKColor.Parse("#7cbf5a"),
                    ObjectKind.Pickables => SKColor.Parse("#e8d24a"),
                    _ => SKColor.Parse("#e6e9ee"),
                };
                g.DrawCircle(Sx(o.Dx), Sy(o.Dz), r, dot);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }
}
