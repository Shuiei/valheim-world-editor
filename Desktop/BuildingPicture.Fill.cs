using System.Numerics;

namespace TerrainEditor.Desktop;

public static partial class BuildingPicture
{
    /// <summary>
    /// Rasterises one triangle (screen space, z the depth) into the picture, with the depth test, the texture and
    /// its cutoff, times the shade; a see-through one blended over what is there, leaving the depth as it is.
    /// </summary>
    private static void Fill(Vector3 a, Vector3 b, Vector3 c, Tri t, float shade, int n, float[] depth, byte[] rgba)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 1e-6f)
        {
            return;
        }
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        var tex = t.Texture;
        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float wa = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / area;
                float wb = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / area;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }
                float z = wa * a.Z + wb * b.Z + wc * c.Z;
                int at = y * n + x;
                if (z >= depth[at])
                {
                    continue;
                }
                var colour = t.Colour;
                float alpha = t.Alpha;
                if (tex != null)
                {
                    var uv = wa * t.Ua + wb * t.Ub + wc * t.Uc;
                    float u = uv.X * t.UvTransform.X + t.UvTransform.Z, v = uv.Y * t.UvTransform.Y + t.UvTransform.W;
                    int tx = (int)((u - MathF.Floor(u)) * tex.Width) % tex.Width, ty = (int)((v - MathF.Floor(v)) * tex.Height) % tex.Height;
                    int ti = (ty * tex.Width + tx) * 4;
                    if (t.Cutoff > 0 && tex.Rgba[ti + 3] < t.Cutoff * 255)
                    {
                        continue;
                    }
                    colour = new Vector3(tex.Rgba[ti] * colour.X, tex.Rgba[ti + 1] * colour.Y, tex.Rgba[ti + 2] * colour.Z) / 255f;
                    alpha *= tex.Rgba[ti + 3] / 255f;
                }
                colour *= shade;
                if (t.Alpha > 0)
                {
                    // Over what is there (the background is clear): the alphas added up, the colours mixed by them.
                    float below = rgba[at * 4 + 3] / 255f * (1 - alpha), total = alpha + below;
                    if (total <= 0)
                    {
                        continue;
                    }
                    var under = new Vector3(rgba[at * 4], rgba[at * 4 + 1], rgba[at * 4 + 2]);
                    colour = (colour * alpha + under * below) / total;
                    SetColour(rgba, at, colour);
                    rgba[at * 4 + 3] = (byte)Math.Clamp(total * 255, 0, 255);
                    continue;
                }
                depth[at] = z;
                SetColour(rgba, at, colour);
                rgba[at * 4 + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Sets a pixel's red, green and blue (its alpha is left to the caller), each clamped to a byte.
    /// </summary>
    /// <param name="rgba">The picture's pixels, four bytes each.</param>
    /// <param name="at">The pixel's index.</param>
    /// <param name="colour">The colour to set.</param>
    private static void SetColour(byte[] rgba, int at, Vector3 colour)
    {
        rgba[at * 4] = (byte)Math.Clamp(colour.X, 0, 255);
        rgba[at * 4 + 1] = (byte)Math.Clamp(colour.Y, 0, 255);
        rgba[at * 4 + 2] = (byte)Math.Clamp(colour.Z, 0, 255);
    }
}
