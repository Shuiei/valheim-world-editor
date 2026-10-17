using System.Globalization;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Heightmaps (WorldPainter's heightmap import and export): the ground of an area as a 16-bit grayscale
/// PNG, north at the top, one pixel per metre, to change in an image editor or a terrain tool and bring
/// back. The lowest and highest heights are written into the picture, so an export comes back exactly.
/// </summary>
public static class Heightmaps
{
    /// <summary>PNG text keys of the lowest and highest heights (metres) and of the exported area.</summary>
    public const string MinKey = "vwe-min", MaxKey = "vwe-max", AreaKey = "vwe-area";

    /// <summary>
    /// The heights as a 16-bit grayscale PNG, lowest black and highest white, with the range and area in
    /// its text chunks.
    /// </summary>
    /// <param name="w">Width in points.</param>
    /// <param name="h">Height in points.</param>
    /// <param name="heights">Heights in metres, w × h, rows from south to north.</param>
    /// <param name="area">Text saying which area this is, kept in the picture.</param>
    /// <param name="min">The lowest height written.</param>
    /// <param name="max">
    /// The highest height written: at least 0.01 m above <paramref name="min"/>, even on flat ground.
    /// </param>
    /// <returns>The PNG file's bytes.</returns>
    public static byte[] Encode(int w, int h, float[] heights, string area, out float min, out float max)
    {
        float lo = heights.Min(), hi = heights.Max();
        if (hi - lo < 0.01f)
        {
            hi = lo + 0.01f;
        }
        ushort[] px = new ushort[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Picture rows go from north (top) to south.
                px[y * w + x] = (ushort)Math.Round((heights[(h - 1 - y) * w + x] - lo) / (hi - lo) * 65535f);
            }
        }
        min = lo;
        max = hi;
        return Png.WriteGray16(w, h, px, new Dictionary<string, string>
        {
            [MinKey] = lo.ToString("R", CultureInfo.InvariantCulture),
            [MaxKey] = hi.ToString("R", CultureInfo.InvariantCulture),
            // A PNG note holds Latin-1 only: a world's name outside it keeps its place as '?'.
            [AreaKey] = new string([.. area.Select(c => c is '\0' or > '\u00ff' ? '?' : c)]),
            ["Software"] = "Valheim World Editor",
        });
    }

    /// <summary>
    /// The picture resampled (between the four nearest pixels) to w x h points, rows from south to north.
    /// </summary>
    public static float[] Resample(Png.Image img, int w, int h)
    {
        float[] o = new float[w * h];
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = w > 1 ? x / (float)(w - 1) * (img.Width - 1) : 0, fy = h > 1 ? (h - 1 - z) / (float)(h - 1) * (img.Height - 1) : 0;
                int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(img.Width - 1, x0 + 1), y1 = Math.Min(img.Height - 1, y0 + 1);
                float tx = fx - x0, ty = fy - y0;
                float At(int a, int b) => img.Values[b * img.Width + a];
                o[z * w + x] = (At(x0, y0) * (1 - tx) + At(x1, y0) * tx) * (1 - ty) + (At(x0, y1) * (1 - tx) + At(x1, y1) * tx) * ty;
            }
        }
        return o;
    }

    /// <summary>
    /// The farthest from 0 a height written in a picture may be (metres): far past any world's ground, which the
    /// editor's export never passes; the Area panel's boxes take it.
    /// </summary>
    internal const float MostHeight = 100_000f;

    /// <summary>A heightmap picture as read back.</summary>
    /// <param name="Width">The picture's width in pixels.</param>
    /// <param name="Height">The picture's height in pixels.</param>
    /// <param name="Min">The lowest height written in it (metres), null when the editor did not export it.</param>
    /// <param name="Max">The highest height written in it (metres), null when the editor did not export it.</param>
    /// <param name="Area">The area written in it, if any.</param>
    /// <param name="Values">Brightness 0..1 resampled to the requested points, rows from south to north.</param>
    public sealed record Picture(int Width, int Height, float? Min, float? Max, string? Area, float[] Values);

    /// <summary>
    /// A picture's brightness (0..1) resampled to w × h points, rows from south to north, with the
    /// heights written in it when the editor exported it (only numbers within <see cref="MostHeight"/> of 0, and both
    /// left out when the highest is not above the lowest). Throws InvalidDataException or IOException.
    /// </summary>
    public static Picture Read(byte[] bytes, int w, int h)
    {
        Png.Image img = Png.Read(bytes);
        float? Num(string key) => img.Text.TryGetValue(key, out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && MathF.Abs(v) <= MostHeight ? v : null;
        float? min = Num(MinKey), max = Num(MaxKey);
        if (min is float lo && max is float hi && hi <= lo)
        {
            (min, max) = (null, null);
        }
        float[] values = [.. Resample(img, w, h).Select(v => MathF.Round(v * 100000f) / 100000f)];
        return new Picture(img.Width, img.Height, min, max, img.Text.GetValueOrDefault(AreaKey), values);
    }
}
