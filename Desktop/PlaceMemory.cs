using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the Place tool remembers between runs (the web editor keeps it in the browser), in place.json in the data
/// folder: your own presets, favourite and recently placed kinds, the chosen kinds and their weights.
/// </summary>
public sealed class PlaceMemory
{
    /// <summary>Writes place.json indented, to read by hand.</summary>
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Your own presets (the built-in ones are not stored).</summary>
    public List<PlaceTool.Preset> Presets { get; set; } = [];
    /// <summary>Favourite kinds, by prefab name.</summary>
    public List<string> Favourites { get; set; } = [];
    /// <summary>Recently placed kinds, by prefab name, newest first (eight at most).</summary>
    public List<string> Recent { get; set; } = [];
    /// <summary>The kinds chosen for placing last time, by prefab name.</summary>
    public List<string> Chosen { get; set; } = [];
    /// <summary>Each chosen kind's weight in the mix, by prefab name.</summary>
    public Dictionary<string, int> Weights { get; set; } = [];
    /// <summary>The builder chosen for each world, by world name (Built by, in the View panel).</summary>
    public Dictionary<string, long> Builders { get; set; } = [];

    /// <summary>place.json in the data folder.</summary>
    private static string FilePath => Path.Combine(AppSettings.DataDir, "place.json");
    /// <summary>Where to keep the file instead of the data folder; tests keep theirs elsewhere.</summary>
    internal static string? PathOverride { get; set; }
    /// <summary>The file in use: the override, or place.json in the data folder.</summary>
    private static string Where => PathOverride ?? FilePath;

    /// <summary>Reads the file; a missing or unreadable file gives an empty memory.</summary>
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

    /// <summary>Writes the file (errors are ignored: the memory is a convenience).</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Where)!);
            File.WriteAllText(Where, JsonSerializer.Serialize(this, Indented));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Notes kinds just placed: the newest first, eight at most. Saves.</summary>
    public void NoteRecent(IEnumerable<string> names)
    {
        Recent = [.. names.Concat(Recent).Distinct().Take(8)];
        Save();
    }

    /// <summary>Makes a kind a favourite, or no longer one. Saves.</summary>
    public void ToggleFavourite(string name)
    {
        if (!Favourites.Remove(name))
        {
            Favourites.Add(name);
        }
        Save();
    }
}
