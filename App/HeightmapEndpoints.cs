using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

// Heightmaps (WorldPainter's heightmap import and export): the ground of an area as a 16-bit grayscale
// PNG, north at the top, one pixel per metre, to change in an image editor or a terrain tool (Gaea,
// World Machine...) and bring back. The lowest and highest heights are written into the picture.
public static class HeightmapEndpoints
{
	public const string MinKey = Heightmaps.MinKey, MaxKey = Heightmaps.MaxKey, AreaKey = Heightmaps.AreaKey;

	// The ground of zones x0..x1, z0..z1 (base terrain, location flattening and edits, as in game):
	// width and height in points (64 per zone + 1), rows from south to north.
	public static (int W, int H, float[] Heights) Heights(ValheimGen.TerrainService terrain, EditStore edits, int x0, int z0, int x1, int z1) => HeightGrid.Read(terrain, edits, x0, z0, x1, z1);

	public static byte[] Encode(int w, int h, float[] heights, string area, out float min, out float max) => Heightmaps.Encode(w, h, heights, area, out min, out max);

	public static float[] Resample(Png.Image img, int w, int h) => Heightmaps.Resample(img, w, h);

	public static void Map(WebApplication app, Func<WorldSave> world, ValheimGen.TerrainService terrain, EditStore edits)
	{
		static bool Valid(int x0, int z0, int x1, int z1) => x1 >= x0 && z1 >= z0 && x1 - x0 <= 8 && z1 - z0 <= 8;
		string Area(int x0, int z0, int x1, int z1) => $"{world().Name} zones {x0},{z0} to {x1},{z1}";

		// Downloaded by the browser; the app window writes the file instead (export below).
		app.MapGet("/api/heightmap.png", (int x0, int z0, int x1, int z1) =>
		{
			if (!Valid(x0, z0, x1, z1))
			{
				return Results.BadRequest("Region must be between 1x1 and 9x9 zones.");
			}
			var (w, h, heights) = Heights(terrain, edits, x0, z0, x1, z1);
			return Results.File(Encode(w, h, heights, Area(x0, z0, x1, z1), out _, out _), "image/png", $"{world().Name}_{x0}_{z0}_{x1}_{z1}.png");
		});
		app.MapPost("/api/heightmap/export", (int x0, int z0, int x1, int z1) =>
		{
			if (!Valid(x0, z0, x1, z1))
			{
				return Results.BadRequest("Region must be between 1x1 and 9x9 zones.");
			}
			var (w, h, heights) = Heights(terrain, edits, x0, z0, x1, z1);
			byte[] png = Encode(w, h, heights, Area(x0, z0, x1, z1), out float min, out float max);
			string dir = Path.Combine(AppSettings.DataDir, "heightmaps");
			Directory.CreateDirectory(dir);
			string safe = string.Concat(world().Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
			string path = Path.Combine(dir, $"{safe}_{x0}_{z0}_{x1}_{z1}.png");
			File.WriteAllBytes(path, png);
			return Results.Ok(new { path, min, max, width = w, height = h });
		});
		// A picture (the request body, or a file path chosen in the app window) as brightness values,
		// resampled to w x h points; with the heights written in it when it came from the editor.
		app.MapPost("/api/heightmap/decode", async (HttpRequest req, int w, int h, string? path) =>
		{
			if (w < 1 || h < 1 || w > 2048 || h > 2048)
			{
				return Results.BadRequest("Bad size.");
			}
			byte[] bytes;
			if (path != null)
			{
				if (!File.Exists(path))
				{
					return Results.BadRequest("That file does not exist.");
				}
				bytes = await File.ReadAllBytesAsync(path);
			}
			else
			{
				using MemoryStream ms = new();
				await req.Body.CopyToAsync(ms);
				bytes = ms.ToArray();
			}
			Png.Image img;
			try
			{
				img = Png.Read(bytes);
			}
			catch (Exception ex) when (ex is InvalidDataException or IOException)
			{
				return Results.BadRequest($"That picture cannot be used: {ex.Message}");
			}
			float? Num(string key) => img.Text.TryGetValue(key, out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
			float[] values = Resample(img, w, h).Select(v => MathF.Round(v * 100000f) / 100000f).ToArray();
			return Results.Ok(new { width = img.Width, height = img.Height, min = Num(MinKey), max = Num(MaxKey), area = img.Text.GetValueOrDefault(AreaKey), values });
		});
	}
}
