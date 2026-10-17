// The location rules' cache file: its parts, and the rules read back from their JSON (see GameLocations).

using System.Text.Json;
using ValheimGen;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// The cache file's three parts: the logs and order line (<see cref="OrderLine"/>), the stamp of what the rules
    /// were read from, and the rules; null when there is no such file or it cannot be read (read again).
    /// </summary>
    private static string[]? CacheLines(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            string[] parts = File.ReadAllText(file).Split('\n', 3);
            return parts.Length == 3 ? parts : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "reading the kept location rules (read from the game again)");
            return null;
        }
    }

    /// <summary>Cached rules from their JSON; null when it cannot be read (read again).</summary>
    private static SeedLocations.RuleSet? Rules(string json)
    {
        try
        {
            return SeedLocations.RuleSet.FromJson(json);
        }
        catch (JsonException e)
        {
            Log.Caught(e, "reading the kept location rules' JSON (read from the game again)");
            return null;
        }
    }
}
