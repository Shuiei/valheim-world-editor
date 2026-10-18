using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The file: where it is, read at start, written later and on Flush.
public sealed partial class Prefs
{
    /// <summary>Tests: another file, never the user's.</summary>
    internal static string? PathOverride { get; set; }

    /// <summary>prefs.json in the data folder, or the tests' file.</summary>
    public static string DefaultPath => PathOverride ?? Path.Combine(AppSettings.DataDir, "prefs.json");
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
            Log.Caught(ex, "reading the remembered preferences (starting from none)");
        }
        return new Prefs(path, values);
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
            Log.Caught(ex, "writing the remembered preferences");
        }
    }
}
