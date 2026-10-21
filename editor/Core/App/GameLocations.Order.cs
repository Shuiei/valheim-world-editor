// The lists' order as text, in a cache file, and as read back from it (see GameLocations).

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>The lists' order as text ("3/0,2/0,…"), as the cache keeps it.</summary>
    private static string OrderText((int Locations, int Vegetation)[] order) => string.Join(",", order.Select(o => $"{o.Locations}/{o.Vegetation}"));

    /// <summary>
    /// The cache file's first line: the game's logs as they were when the lists' order was taken from them
    /// (<see cref="LogsStamp"/>), and that order.
    /// </summary>
    internal static string OrderLine(string logs, (int Locations, int Vegetation)[] order) => $"{logs}\t{OrderText(order)}";

    /// <summary>
    /// The lists' order a cache file's first line keeps, when the game's logs are still as they were then (they need
    /// not be read again: tens of MB with mods); else null.
    /// </summary>
    internal static (int Locations, int Vegetation)[]? KeptOrder(string? line, string logs)
    {
        int tab = line?.LastIndexOf('\t') ?? -1;
        if (line == null || tab < 0 || line[..tab] != logs)
        {
            return null;
        }
        var order = new List<(int, int)>();
        foreach (string pair in line[(tab + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] n = pair.Split('/');
            if (n.Length != 2 || !int.TryParse(n[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int l)
                || !int.TryParse(n[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int v))
            {
                return null;
            }
            order.Add((l, v));
        }
        return order.Count > 0 ? [.. order] : null;
    }
}
