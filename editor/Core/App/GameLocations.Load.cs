// Loading the game's location rules: from the cache when the game and the editor are unchanged, else read from the game (see GameLocations).

using System.Text.Json;
using ValheimGen;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// The rules from the game (from the cache when the game and the editor are unchanged), or null when
    /// they cannot be read (whatever goes wrong: the page then shows no bosses or traders).
    /// </summary>
    /// <param name="bundles">The game's bundles.</param>
    /// <param name="cacheFile">Where the rules are kept; null: not kept nor read from a file.</param>
    public static SeedLocations.RuleSet? Load(GameBundles bundles, string? cacheFile)
    {
        try
        {
            var manifest = Manifest.Read(bundles.Folder);
            var scanned = MainSceneBundles(bundles, manifest);
            if (scanned.Count == 0)
            {
                return null;
            }
            // The logs are read only when they changed since the cache was made (the order they gave is kept with it).
            string logs = LogsStamp();
            string[]? kept = cacheFile != null ? CacheLines(cacheFile) : null;
            var (order, from) = KeptOrder(kept?[0], logs) is { } known ? (known, "the game's log, unchanged since it was last read") : ListOrder();
            string stamp = CacheVersion + " " + OrderText(order) + " " + JsonSerializer.Serialize(scanned.Select(b => new FileInfo(bundles.PathOf(b)!)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
            if (kept is { } k && k[1] == stamp && Rules(k[2]) is { } cached)
            {
                if (k[0] != OrderLine(logs, order))
                {
                    // The game ran since, giving the same order: the logs as they are now are kept with it.
                    GameData.KeepCache(cacheFile!, OrderLine(logs, order) + "\n" + stamp + "\n" + k[2]);
                }
                Console.WriteLine($"Location rules: {cached.Locations.Count} kinds, from the cache.");
                return cached;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var rules = Read(bundles, scanned, manifest, order);
            if (rules != null)
            {
                Console.WriteLine($"Location rules: {rules.Locations.Count} kinds and {rules.AltBiomes.Count} alt biomes read from the game ({scanned.Count} bundles) in {watch.ElapsedMilliseconds} ms; the lists' order from {from}.");
            }
            if (rules != null && cacheFile != null)
            {
                GameData.KeepCache(cacheFile, OrderLine(logs, order) + "\n" + stamp + "\n" + rules.ToJson());
            }
            return rules;
        }
        catch (Exception e)
        {
            // Anything in the game's files can fail the reading: the page goes on without bosses or traders.
            Log.Caught(e, "reading the game's location rules (no bosses or traders on the New world page)");
            return null;
        }
    }
}
