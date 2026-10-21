namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's four pictures in one (screenshot with views 4): a point from above and from three sides, with their names.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>Four views of a point (the area's middle when none is given), as one picture with their names.</summary>
    private async Task<ModelContextProtocol.Protocol.CallToolResult> FourViews(float? x, float? z, float distance, float? height)
    {
        var cams = new (string Name, float Yaw, float Pitch)[] { ("from above (north up)", 0, 89), ("from the south-west", 225, 35), ("from the east", 90, 35), ("from the north-west", 315, 35) };
        var pictures = new List<SkiaSharp.SKBitmap>();
        try
        {
            foreach (var (name, yaw, pitch) in cams)
            {
                string path = Path.Combine(Path.GetTempPath(), $"vwe-claude-{Guid.NewGuid():N}.png");
                try
                {
                    string? problem = await WithCamera(async () =>
                    {
                        if (_w.Session?.Scene is not { } scene || _w.MapShown)
                        {
                            return "Four views need an area or the Workshop open in 3D.";
                        }
                        float cx = x ?? scene.Cx, cz = z ?? scene.Cz;
                        await ShootView(path, () => _w.View.Orbit(cx, cz, yaw, pitch, distance, height));
                        return null;
                    });
                    if (problem != null)
                    {
                        return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = problem } } };
                    }
                    var bmp = SkiaSharp.SKBitmap.Decode(await File.ReadAllBytesAsync(path));
                    pictures.Add(bmp);
                }
                finally
                {
                    File.Delete(path);
                }
            }
            int w = 640, h = (int)(640f * pictures[0].Height / pictures[0].Width);
            using var sheet = new SkiaSharp.SKBitmap(w * 2, h * 2);
            using (var canvas = new SkiaSharp.SKCanvas(sheet))
            {
                using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 20);
                using var back = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 0, 0, 160) };
                using var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White, IsAntialias = true };
                for (int i = 0; i < 4; i++)
                {
                    var r = SkiaSharp.SKRect.Create(i % 2 * w, i / 2 * h, w, h);
                    canvas.DrawBitmap(pictures[i], r);
                    canvas.DrawRect(r.Left, r.Top, w, 30, back);
                    canvas.DrawText(cams[i].Name, r.Left + 8, r.Top + 22, SkiaSharp.SKTextAlign.Left, font, ink);
                }
            }
            using var image = SkiaSharp.SKImage.FromBitmap(sheet);
            return new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray(), "image/jpeg") } };
        }
        finally
        {
            foreach (var p in pictures)
            {
                p.Dispose();
            }
        }
    }
}
