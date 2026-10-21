using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class Stamps
{
    /// <summary>
    /// The loaded stamps kept from earlier runs; empty when there are none or the file cannot be read. Entries
    /// without weights or of the wrong size are left out.
    /// </summary>
    public static List<Stamp> LoadKept()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }
            // (Entries without their weights, as a file changed by hand can have, are left out.)
            return [.. (JsonSerializer.Deserialize<List<Kept?>>(File.ReadAllText(FilePath)) ?? [])
                .Where(k => k is { Name: not null, Weights: not null }).Select(k => k!)
                .Select(k => new Stamp(k.Name, k.Label, [.. Convert.FromBase64String(k.Weights).Select(b => b / 255f)], Loaded: true))
                .Where(s => s.Data.Length == Size * Size)];
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading stamps.json (starting with none)");
            return [];
        }
    }

    /// <summary>
    /// Writes the loaded stamps to stamps.json, replacing it whole (a write cut short leaves the old file); a failure
    /// to write is logged, and the stamps stay loaded for this run.
    /// </summary>
    public static void SaveKept(IEnumerable<Stamp> loaded)
    {
        try
        {
            var list = loaded.Select(s => new Kept(s.Name, s.Label, Convert.ToBase64String(s.Data.Select(v => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255)).ToArray()))).ToList();
            SafeFile.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing stamps.json");
        }
    }
}
