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

    /// <summary>
    /// Whether the file could not be read (or, broken, kept aside) when this was loaded: what is in it is not
    /// written over.
    /// </summary>
    private bool _unread;

    /// <summary>
    /// Reads the file. A missing one gives an empty memory; a broken one is first copied aside as place.json.bad (the
    /// next save writes over it), then gives an empty memory; one that cannot be read (or kept aside) gives an empty
    /// memory that is never saved over it. Nulls a hand-edited file holds are taken as empty, and presets without a
    /// name or kinds are left out.
    /// </summary>
    public static PlaceMemory Load()
    {
        string text;
        try
        {
            // Read at once: File.Exists says no for a file there that cannot be read, which must not count as none.
            text = File.ReadAllText(Where);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Place memory: {Where} could not be read ({ex.Message}); starting empty, not saving over it.");
            return new() { _unread = true };
        }
        try
        {
            return (JsonSerializer.Deserialize<PlaceMemory>(text) ?? new()).Mended();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Place memory: {Where} is not readable ({ex.Message}); kept as {Where}.bad, starting empty.");
            try
            {
                File.Copy(Where, Where + ".bad", overwrite: true);
                return new();
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Place memory: {Where} could not be kept as {Where}.bad ({copy.Message}); not saving over it.");
                return new() { _unread = true };
            }
        }
    }

    /// <summary>This memory with the nulls of a hand-edited file taken as empty, and its presets without a name or kinds left out.</summary>
    private PlaceMemory Mended()
    {
        // The JSON may hold null where the types say there is none.
        Presets = [.. (Presets ?? []).Where(p => p?.Name != null && p.Kinds != null)];
        Favourites = [.. (Favourites ?? []).Where(n => n != null)];
        Recent = [.. (Recent ?? []).Where(n => n != null)];
        Chosen = [.. (Chosen ?? []).Where(n => n != null)];
        Weights ??= [];
        Builders ??= [];
        return this;
    }

    /// <summary>
    /// Writes the file through a temporary one, so a write cut short leaves the old file whole. A failure is logged,
    /// not shown: the memory is a convenience, and stays as it is in this run. Nothing is written over a file that
    /// could not be read.
    /// </summary>
    public void Save()
    {
        if (_unread)
        {
            Console.WriteLine($"Place memory: not saved, {Where} could not be read when the editor started.");
            return;
        }
        try
        {
            SafeFile.WriteAllText(Where, JsonSerializer.Serialize(this, Indented));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Place memory: {Where} could not be written ({ex.Message}).");
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
