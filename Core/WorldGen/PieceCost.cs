using System.Text.Json;

namespace TerrainEditor.Terrain;

/// <summary>
/// What build pieces cost in game, for the blueprint library: each piece's English name, its resources
/// (item and amount, as the hammer takes them) and the crafting station that must be near to build it,
/// read from the player's own Valheim (GameData; the tests use tests/fixtures/piece-cost.json).
/// </summary>
public static partial class PieceCost
{
    /// <summary>
    /// One piece's recipe.
    /// </summary>
    /// <param name="Name">The piece's English name.</param>
    /// <param name="Station">The crafting station that must be near to build it; null when none.</param>
    /// <param name="Resources">Item prefab names and amounts, as the hammer takes them.</param>
    public sealed record Info(string Name, string? Station, (string Item, int Amount)[] Resources);

    /// <summary>
    /// None until Use: no world or Workshop opens before then (GameData).
    /// </summary>
    private static volatile Data _catalogue = new([], []);

    /// <summary>
    /// What Use read, swapped in whole so readers never see half of it.
    /// </summary>
    /// <param name="Pieces">Recipes by piece prefab name.</param>
    /// <param name="Items">English item names by item prefab name.</param>
    private sealed record Data(Dictionary<string, Info> Pieces, Dictionary<string, string> Items);

    /// <summary>
    /// Takes the pieces and items, as GameCatalogues' CostScan writes them.
    /// </summary>
    public static void Use(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var pieces = new Dictionary<string, Info>();
        foreach (JsonProperty p in doc.RootElement.GetProperty("pieces").EnumerateObject())
        {
            var res = p.Value.GetProperty("r").EnumerateArray().Select(r => (r[0].GetString()!, r[1].GetInt32())).ToArray();
            pieces[p.Name] = new Info(p.Value.GetProperty("n").GetString()!, p.Value.TryGetProperty("st", out var st) ? st.GetString() : null, res);
        }
        var items = doc.RootElement.GetProperty("items").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        _catalogue = new Data(pieces, items);
    }

    /// <summary>
    /// A piece's recipe by prefab name; null for pieces the game has no recipe for.
    /// </summary>
    public static Info? Get(string prefab) => _catalogue.Pieces.GetValueOrDefault(prefab);

    /// <summary>
    /// An item's English name (Wood, Fine wood, Bronze nails), else its prefab name.
    /// </summary>
    public static string ItemName(string item) => _catalogue.Items.GetValueOrDefault(item, item);

    /// <summary>
    /// The cost of so many of each piece kind (prefab name → count).
    /// </summary>
    public static Total Of(IEnumerable<KeyValuePair<string, int>> kinds)
    {
        var amounts = new Dictionary<string, int>();
        var stations = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new Dictionary<string, int>();
        foreach (var (prefab, count) in kinds)
        {
            if (Get(prefab) is not { } info)
            {
                unknown[prefab] = unknown.GetValueOrDefault(prefab) + count;
                continue;
            }
            if (info.Station is { Length: > 0 } st)
            {
                stations.Add(st);
            }
            foreach (var (item, amount) in info.Resources)
            {
                amounts[item] = amounts.GetValueOrDefault(item) + amount * count;
            }
        }
        var materials = amounts.Select(a => (a.Key, ItemName(a.Key), a.Value)).OrderByDescending(m => m.Value).ThenBy(m => m.Item2, StringComparer.OrdinalIgnoreCase).ToList();
        return new Total(materials, [.. stations], unknown);
    }

    /// <summary>
    /// The cost of a list of pieces by prefab name, one entry per piece.
    /// </summary>
    public static Total Of(IEnumerable<string> prefabs) => Of(prefabs.GroupBy(p => p).Select(g => KeyValuePair.Create(g.Key, g.Count())));
}
