using System.Globalization;
using TerrainEditor.Save;

namespace TerrainEditor.App;

// Heightmaps (WorldPainter's heightmap import and export): the ground of an area as a 16-bit grayscale
// PNG, north at the top, one pixel per metre, to change in an image editor or a terrain tool and bring
// back. The lowest and highest heights are written into the picture, so an export comes back exactly.
public static class Heightmaps
{
	public const string MinKey = "vwe-min", MaxKey = "vwe-max", AreaKey = "vwe-area";

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
			[AreaKey] = area,
			["Software"] = "Valheim World Editor",
		});
	}

	// The picture resampled (between the four nearest pixels) to w x h points, rows from south to north.
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

	public sealed record Picture(int Width, int Height, float? Min, float? Max, string? Area, float[] Values);

	// A picture's brightness (0..1) resampled to w × h points, rows from south to north, with the
	// heights written in it when the editor exported it. Throws InvalidDataException or IOException.
	public static Picture Read(byte[] bytes, int w, int h)
	{
		Png.Image img = Png.Read(bytes);
		float? Num(string key) => img.Text.TryGetValue(key, out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
		float[] values = Resample(img, w, h).Select(v => MathF.Round(v * 100000f) / 100000f).ToArray();
		return new Picture(img.Width, img.Height, Num(MinKey), Num(MaxKey), img.Text.GetValueOrDefault(AreaKey), values);
	}
}
