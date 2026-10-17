// Finding the main scene's ZoneSystem, LocationLists and alt biomes in the game's bundles (see GameLocations).

using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// The main scene's bundle and the bundles it needs, those the folder has (the SoftRef manifest lists
    /// them): where ZoneSystem and the LocationLists are (also the vegetation rules, GameCatalogues).
    /// </summary>
    internal static List<string> MainSceneBundles(GameBundles bundles) => MainSceneBundles(bundles, Manifest.Read(bundles.Folder));

    /// <summary>
    /// The main scene's bundles of the manifest that the folder has.
    /// </summary>
    private static List<string> MainSceneBundles(GameBundles bundles, Manifest manifest) => [.. manifest.SceneBundles("main").Where(bundles.HasBundle)];

    /// <summary>
    /// What a scan of the main scene found: ZoneSystem's locations, the alt biomes, and every LocationList by its counts.
    /// </summary>
    /// <param name="Zone">ZoneSystem's locations, or null when none was found.</param>
    /// <param name="Alts">The alt biomes list, or null when none was found.</param>
    /// <param name="Lists">Every list of a pair, in the scene's order, by (locations, vegetations).</param>
    private sealed record SceneFields(AssetTypeValueField? Zone, AssetTypeValueField? Alts, Dictionary<(int, int), List<AssetTypeValueField>> Lists);

    /// <summary>
    /// Scans the scene's bundles for ZoneSystem's locations, the alt biomes and the LocationLists that the order names.
    /// </summary>
    /// <param name="bundles">The game's bundles.</param>
    /// <param name="scanned">The scene's bundles, the main one first among the ones it needs.</param>
    /// <param name="order">The lists' order: the lists it names are kept.</param>
    private static SceneFields FindScene(GameBundles bundles, List<string> scanned, (int Locations, int Vegetation)[] order)
    {
        AssetTypeValueField? zone = null, alts = null;
        // Every list of a pair, in the scene's order: two lists of the same counts are two lists (the second was taken
        // for the first).
        var lists = new Dictionary<(int, int), List<AssetTypeValueField>>();
        foreach (string bundle in scanned)
        {
            foreach (var f in bundles.MonoBehaviours(bundle))
            {
                var locations = f["m_locations"];
                if (!locations.IsDummy && !f["m_zoneSize"].IsDummy)
                {
                    // Some bundles hold another ZoneSystem too (fewer locations): the main one has most.
                    if (zone == null || Count(locations) > Count(zone))
                    {
                        zone = locations;
                    }
                }
                else if (!locations.IsDummy && !f["m_sortOrder"].IsDummy)
                {
                    var key = (Count(locations), Count(f["m_vegetation"]));
                    if (order.Contains(key))
                    {
                        (lists.TryGetValue(key, out var same) ? same : lists[key] = []).Add(locations);
                    }
                }
                else if (!f["m_alts"].IsDummy && alts == null)
                {
                    alts = f["m_alts"];
                }
            }
        }
        return new SceneFields(zone, alts, lists);
    }
}
