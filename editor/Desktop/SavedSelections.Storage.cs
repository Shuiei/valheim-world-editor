using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The file: read whole (a broken one kept aside), and written through a temporary file.
public static partial class SavedSelections
{
    /// <summary>
    /// Every world's saved selections, by world name; empty when there is no file. One that is not valid JSON is first
    /// copied aside as selections.json.bad (the next save writes over it) and counts as empty. Null when the file
    /// could not be read, or kept aside: it must not be written over.
    /// </summary>
    private static Dictionary<string, List<Saved>>? LoadAll()
    {
        string text;
        try
        {
            // Read at once: File.Exists says no for a file there that cannot be read, which must not count as none.
            text = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // None yet: brief.
            Log.CaughtBrief(ex, "reading selections.json (none yet)");
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading selections.json (not saving over it)");
            return null;
        }
        try
        {
            var all = JsonSerializer.Deserialize<Dictionary<string, List<Saved>>>(text);
            // A hand-edited file may hold nulls: those entries are left out.
            return all == null ? [] : all.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value.Where(s => s?.Name != null && s.Items != null).ToList());
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            Log.Caught(ex, "parsing selections.json (kept as selections.json.bad, starting from none)");
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
                return [];
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Log.Caught(copy, "keeping the unreadable selections.json as selections.json.bad (not saving over it)");
                return null;
            }
        }
    }

    /// <summary>
    /// Writes every world's saved selections through a temporary file, so a write cut short leaves the old file whole;
    /// false when the file could not be written.
    /// </summary>
    private static bool SaveAll(Dictionary<string, List<Saved>> all)
    {
        try
        {
            SafeFile.WriteAllText(FilePath, JsonSerializer.Serialize(all));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing selections.json");
            return false;
        }
    }
}
