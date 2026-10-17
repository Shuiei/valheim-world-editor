using ValheimGen;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// Finds ZoneSystem's locations, the LocationLists named by the order and the alt biomes in these bundles and makes
    /// the rule set; null (and a log line) when one is missing.
    /// </summary>
    private static SeedLocations.RuleSet? Read(GameBundles bundles, List<string> scanned, Manifest manifest, (int Locations, int Vegetation)[] order)
    {
        var scene = FindScene(bundles, scanned, order);
        // Each pair the order names as many times as the scene has lists of it, at least.
        int found = order.CountBy(k => k).Sum(g => Math.Min(g.Value, scene.Lists.TryGetValue(g.Key, out var l) ? l.Count : 0));
        if (scene.Zone == null || scene.Alts == null || found < order.Length)
        {
            Console.WriteLine($"The game's location rules were not all found (ZoneSystem: {scene.Zone != null}, alt biomes: {scene.Alts != null}, lists: {found}/{order.Length}); a game update changed them (start the game once to refresh its log)? No bosses or traders on the New world page.");
            return null;
        }
        return RuleSetOf(scene, manifest, order);
    }

    /// <summary>
    /// The rule set of a scene that has all its parts: the ZoneSystem's locations, then each LocationList in the order,
    /// then the alt biomes and what they add.
    /// </summary>
    /// <param name="scene">The scene's parts, all found.</param>
    /// <param name="manifest">The manifest, for the prefabs' names.</param>
    /// <param name="order">The lists' order.</param>
    internal static SeedLocations.RuleSet RuleSetOf(SceneFields scene, Manifest manifest, (int Locations, int Vegetation)[] order)
    {
        var rules = new SeedLocations.RuleSet();
        rules.Locations.AddRange(Items(scene.Zone!).Select(l => Location(l, manifest, null)));
        // A pair named twice takes its lists one after the other.
        var taken = new Dictionary<(int, int), int>();
        foreach (var key in order)
        {
            int n = taken.GetValueOrDefault(key);
            taken[key] = n + 1;
            rules.Locations.AddRange(Items(scene.Lists[key][n]).Select(l => Location(l, manifest, null)));
        }
        foreach (var a in Items(scene.Alts!))
        {
            string name = a["m_name"].AsString;
            rules.AltBiomes.Add(new SeedLocations.AltBiomeRule { Name = name, BlockLocationNames = [.. Items(a["m_blockLocationNames"]).Select(s => s.AsString)] });
            rules.Locations.AddRange(Items(a["m_addLocations"]).Select(l => Location(l, manifest, name)));
        }
        return rules;
    }
}
