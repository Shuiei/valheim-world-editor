using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the editor remembers between runs, as the web editor kept it in the browser: the View panel's switches, the
/// map's place, the brush's shape and falloff, the Area action, the Shape preset (and a formula of one's own), Select's
/// options, which right-hand panel is open, the clipboard. In prefs.json in the data folder; a missing or broken file
/// means the defaults. Each value has its own name, so remembering another control is one Bind line where it is made.
/// Saved a moment after the last change (a dragged map writes once), and on Flush.
/// </summary>
public sealed class Prefs
{
    /// <summary>The file, or null when kept in memory only.</summary>
    private readonly string? _path;
    /// <summary>The values by name, as JSON.</summary>
    private readonly Dictionary<string, JsonNode?> _values;
    /// <summary>Guards the values and the timer: the save timer writes from another thread.</summary>
    private readonly Lock _lock = new();
    /// <summary>Writes the file 300 ms after the last change.</summary>
    private Timer? _timer;

    /// <summary>Tests: another file, never the user's.</summary>
    internal static string? PathOverride { get; set; }

    /// <summary>prefs.json in the data folder, or the tests' file.</summary>
    public static string DefaultPath => PathOverride ?? Path.Combine(AppSettings.DataDir, "prefs.json");

    /// <summary>Prefs over the given values, written to the file (null: never written).</summary>
    private Prefs(string? path, Dictionary<string, JsonNode?> values)
    {
        _path = path;
        _values = values;
    }

    /// <summary>The remembered values (from DefaultPath unless another file is given).</summary>
    public static Prefs Load(string? path = null)
    {
        path ??= DefaultPath;
        var values = new Dictionary<string, JsonNode?>();
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o)
            {
                foreach (var (k, v) in o)
                {
                    values[k] = v?.DeepClone();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new Prefs(path, values);
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

    /// <summary>Writes the file 300 ms from now (again from now when called again), unless kept in memory.</summary>
    private void Later()
    {
        if (_path == null)
        {
            return;
        }
        _timer ??= new Timer(_ => Flush());
        _timer.Change(300, Timeout.Infinite);
    }

    /// <summary>
    /// Writes the file now, sorted by name, through a temporary file (a file that cannot be written is left as it was:
    /// the values stay in memory).
    /// </summary>
    public void Flush()
    {
        if (_path == null)
        {
            return;
        }
        string text;
        lock (_lock)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            var o = new JsonObject();
            foreach (var (k, v) in _values.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                o[k] = v?.DeepClone();
            }
            text = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        try
        {
            SafeFile.WriteAllText(_path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A switch (CheckBox, ToggleButton): set from what is remembered, remembered when changed.</summary>
    public void Bind(ToggleButton box, string key)
    {
        if (TryGet(key, out bool on))
        {
            box.IsChecked = on;
        }
        box.IsCheckedChanged += (_, _) => Set(key, box.IsChecked == true);
    }

    /// <summary>
    /// A list of texts (ComboBox): the entry remembered by its text, so a list that changes order or grows keeps the
    /// choice; an entry no longer there leaves the list as it is.
    /// </summary>
    public void Bind(ComboBox box, string key)
    {
        Restore(box, key);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is string text)
            {
                Set(key, text);
            }
        };
    }

    /// <summary>
    /// Chooses the remembered entry, when the list has it (for lists filled again later); true when it did.
    /// </summary>
    public bool Restore(ComboBox box, string key)
    {
        if (TryGet(key, out string? text) && text != null && box.ItemsSource is IEnumerable<object> items)
        {
            int i = items.Select(o => o as string).ToList().IndexOf(text);
            if (i >= 0)
            {
                box.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }
}
