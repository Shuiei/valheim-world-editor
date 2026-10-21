using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// The listing of <see cref="BlueprintStore"/>: its blueprints read for the list, without loading their copies.
/// </summary>
public sealed partial class BlueprintStore
{
    /// <summary>
    /// The blueprints in the folder, by name (case ignored). Files that are not blueprints or cannot be read are left
    /// out.
    /// </summary>
    public List<Summary> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        List<Summary> list = [];
        foreach (string file in System.IO.Directory.GetFiles(Directory, "*.json"))
        {
            try
            {
                // Read without making a node per value: a copy's ground and objects can hold hundreds of thousands.
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("clip", out JsonElement clip) || clip.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                string id = Path.GetFileNameWithoutExtension(file);
                list.Add(new Summary(id, Text(root, "name") ?? id,
                    DateTime.TryParse(Text(root, "created"), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : File.GetLastWriteTime(file),
                    Text(root, "world"), Number(clip, "w"), Number(clip, "h"), clip.TryGetProperty("objects", out JsonElement o) && o.ValueKind == JsonValueKind.Array ? o.GetArrayLength() : 0,
                    HasGround(clip), Text(root, "thumb"), Text(root, "source")));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
            {
                // Not a blueprint, a half-written file or one that cannot be read: left out of the list.
                Log.Caught(ex, "listing a blueprint (left out of the list)");
            }
        }
        return [.. list.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
