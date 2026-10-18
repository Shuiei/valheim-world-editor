using System.Text.Json;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the Place tool remembers between runs (the web editor keeps it in the browser), in place.json in the data
/// folder: your own presets, favourite and recently placed kinds, the chosen kinds and their weights.
/// </summary>
public sealed partial class PlaceMemory
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
    /// Writes the file through a temporary one, so a write cut short leaves the old file whole. A failure is logged,
    /// not shown: the memory is a convenience, and stays as it is in this run. Nothing is written over a file that
    /// could not be read.
    /// </summary>
    public void Save()
    {
        _later?.Stop();
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
            Log.Caught(ex, "writing place.json");
        }
    }

    /// <summary>The timer of a save asked for with <see cref="SaveLater"/>; null until the first one.</summary>
    private DispatcherTimer? _later;

    /// <summary>
    /// Saves 300 ms from now (again from now when asked again), on the UI thread: a slider dragged or kinds ticked in a
    /// row write the file once. <see cref="Flush"/> writes a save still waiting.
    /// </summary>
    public void SaveLater()
    {
        if (_later == null)
        {
            _later = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _later.Tick += (_, _) => Save();
        }
        _later.Stop();
        _later.Start();
    }

    /// <summary>Writes now a save asked for with <see cref="SaveLater"/> and still waiting (the editor is closing).</summary>
    public void Flush()
    {
        if (_later?.IsEnabled == true)
        {
            Save();
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
