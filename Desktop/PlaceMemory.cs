using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// What the Place tool remembers between runs (the web editor keeps it in the browser): your own
// presets, favourite and recently placed kinds, the chosen kinds and their weights.
public sealed class PlaceMemory
{
	public List<PlaceTool.Preset> Presets { get; set; } = new();
	public List<string> Favourites { get; set; } = new();
	public List<string> Recent { get; set; } = new();
	public List<string> Chosen { get; set; } = new();
	public Dictionary<string, int> Weights { get; set; } = new();

	private static string FilePath => Path.Combine(AppSettings.DataDir, "place.json");
	// Tests keep theirs elsewhere.
	internal static string? PathOverride { get; set; }
	private static string Where => PathOverride ?? FilePath;

	public static PlaceMemory Load()
	{
		try
		{
			return File.Exists(Where) ? JsonSerializer.Deserialize<PlaceMemory>(File.ReadAllText(Where)) ?? new() : new();
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			return new();
		}
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Where)!);
			File.WriteAllText(Where, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
		}
	}

	// Kinds placed: the newest first, eight at most.
	public void NoteRecent(IEnumerable<string> names)
	{
		Recent = names.Concat(Recent).Distinct().Take(8).ToList();
		Save();
	}

	public void ToggleFavourite(string name)
	{
		if (!Favourites.Remove(name))
		{
			Favourites.Add(name);
		}
		Save();
	}
}
