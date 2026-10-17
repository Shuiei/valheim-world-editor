using Dict = TerrainEditor.App.CatalogueJson.Dict;
using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class CostScan
    {
        /// <summary>
        /// Adds one piece's entry: English name "n", resources "r" ([item, amount, recover]) and crafting station
        /// "st" when it has one; each resource item's English name goes into the items.
        /// </summary>
        private void Add(Bundle b, string name, AssetTypeValueField piece)
        {
            var res = new List<object?>();
            foreach (var r in piece["m_resources.Array"].Children)
            {
                var (item, drop) = Owner(b, r["m_resItem"]);
                if (string.IsNullOrEmpty(item) || Num(Get(r, "m_amount", 0L)) <= 0)
                {
                    continue;
                }
                if (!_items.ContainsKey(item))
                {
                    var shared = drop!["m_itemData"]["m_shared"];
                    _items[item] = En(Has(shared, "m_name") ? shared["m_name"].AsString : item);
                }
                res.Add(new List<object?> { item, Py(r["m_amount"]), Get(r, "m_recover", 1L) });
            }
            var entry = new Dict { ["n"] = En(Has(piece, "m_name") ? piece["m_name"].AsString : name), ["r"] = res };
            if (Owner(b, piece["m_craftingStation"]).Component is { } station && station.Children.Count > 0)
            {
                entry["st"] = En(Has(station, "m_name") ? station["m_name"].AsString : "");
            }
            _pieces[name] = entry;
        }
    }
}
