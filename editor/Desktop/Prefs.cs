using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the editor remembers between runs, as the web editor kept it in the browser: the View panel's switches, the
/// map's place, the brush's shape and falloff, the Area action, the Shape preset (and a formula of one's own), Select's
/// options, which right-hand panel is open, the clipboard. In prefs.json in the data folder; a missing or broken file
/// means the defaults. Each value has its own name, so remembering another control is one Bind line where it is made.
/// Saved a moment after the last change (a dragged map writes once), and on Flush.
/// </summary>
public sealed partial class Prefs
{
    /// <summary>The file, or null when kept in memory only.</summary>
    private readonly string? _path;
    /// <summary>The values by name, as JSON.</summary>
    private readonly Dictionary<string, JsonNode?> _values;
    /// <summary>Guards the values and the timer: the save timer writes from another thread.</summary>
    private readonly Lock _lock = new();
    /// <summary>Writes the file 300 ms after the last change.</summary>
    private Timer? _timer;

    /// <summary>Prefs over the given values, written to the file (null: never written).</summary>
    private Prefs(string? path, Dictionary<string, JsonNode?> values)
    {
        _path = path;
        _values = values;
    }
    /// <summary>Remembered only while the program runs (the tests' windows: never written, never shared).</summary>
    public static Prefs InMemory() => new(null, []);

    /// <summary>Whether a value is remembered under this name.</summary>
    public bool Has(string key)
    {
        lock (_lock)
        {
            return _values.ContainsKey(key);
        }
    }

    /// <summary>
    /// The value remembered under this name, when there is one of that type (false when missing, null or not that
    /// type).
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        lock (_lock)
        {
            value = default!;
            if (!_values.TryGetValue(key, out var node) || node == null)
            {
                return false;
            }
            try
            {
                if (node.Deserialize<T>() is T v)
                {
                    value = v;
                    return true;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NotSupportedException)
            {
                Log.CaughtBrief(ex, $"reading the remembered preference {key} (it is not used)");
            }
            return false;
        }
    }

    /// <summary>The value remembered under this name, or the fallback.</summary>
    public T Get<T>(string key, T fallback) => TryGet(key, out T v) ? v : fallback;

    /// <summary>Remembers a value under this name; the file is written a moment later.</summary>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _values[key] = JsonSerializer.SerializeToNode(value);
            Later();
        }
    }

    /// <summary>Forgets the value of this name.</summary>
    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_values.Remove(key))
            {
                Later();
            }
        }
    }
}
