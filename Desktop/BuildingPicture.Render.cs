using System.Numerics;
using SkiaSharp;

namespace TerrainEditor.Desktop;

public static partial class BuildingPicture
{
    /// <summary>
    /// Draws the triangles at twice the size, seen from the south-east, orthographic and fitted to the picture, with
    /// simple lighting, then halves it into a PNG.
    /// </summary>
    private static byte[] Render(List<Tri> tris, int size)
    {
        int n = size * 2;
        byte[] rgba = new byte[n * n * 4];
        if (tris.Count > 0)
        {
            // Seen from the south-east, 35° above (view space), orthographic.
            var eye = Vector3.Normalize(new Vector3(1f, 0.8f, 1.2f));
            var view = Matrix4x4.CreateLookAt(eye * 1000f, Vector3.Zero, Vector3.UnitY);
            var light = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f));
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            var cam = new (Vector3 A, Vector3 B, Vector3 C)[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                cam[i] = (Vector3.Transform(t.A, view), Vector3.Transform(t.B, view), Vector3.Transform(t.C, view));
                foreach (var v in new[] { cam[i].A, cam[i].B, cam[i].C })
                {
                    x0 = MathF.Min(x0, v.X); x1 = MathF.Max(x1, v.X); y0 = MathF.Min(y0, v.Y); y1 = MathF.Max(y1, v.Y);
                }
            }
            float scale = n * 0.92f / MathF.Max(MathF.Max(x1 - x0, y1 - y0), 0.5f), mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            Vector3 Screen(Vector3 c) => new(n / 2f + (c.X - mx) * scale, n / 2f - (c.Y - my) * scale, -c.Z);
            float[] depth = new float[n * n];
            Array.Fill(depth, float.MaxValue);
            // Opaque triangles first, then the see-through ones over them.
            foreach (int i in Enumerable.Range(0, tris.Count).OrderBy(i => tris[i].Alpha > 0))
            {
                var t = tris[i];
                var normal = Vector3.Cross(t.B - t.A, t.C - t.A);
                if (normal.LengthSquared() < 1e-12f)
                {
                    continue;
                }
                normal = Vector3.Normalize(normal);
                // Either side lit as its side facing the eye.
                if (Vector3.Dot(normal, eye) < 0)
                {
                    normal = -normal;
                }
                float shade = 0.62f + 0.68f * MathF.Max(0, Vector3.Dot(normal, light));
                Fill(Screen(cam[i].A), Screen(cam[i].B), Screen(cam[i].C), t, shade, n, depth, rgba);
            }
        }
        using var big = new SKBitmap(new SKImageInfo(n, n, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, big.GetPixels(), rgba.Length);
        using var small = big.Resize(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(small);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
