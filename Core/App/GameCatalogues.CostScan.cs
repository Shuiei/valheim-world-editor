// What build pieces cost, read at the editor's start: their resources, crafting stations and item names (see GameCatalogues).

using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// What build pieces cost, read at the editor's start (GameData): each piece's English name, the
    /// crafting station it needs nearby and its resources (item and amount, and whether it is given back),
    /// items by prefab name with their English names, for the blueprint library's material cost.
    /// </summary>
    private sealed partial class CostScan : IStartScan
    {
        /// <summary>
        /// The game's English texts, by token without "$".
        /// </summary>
        private readonly Dictionary<string, string> _english;
        /// <summary>
        /// The pieces' entries and the items' English names, by prefab name.
        /// </summary>
        private readonly Dict _pieces = new(), _items = new();

        /// <summary>
        /// A reader that names things from these English texts.
        /// </summary>
        public CostScan(Dictionary<string, string> english) => _english = english;

        /// <summary>
        /// The "piece-cost" JSON, non-ASCII written as it is.
        /// </summary>
        public string Json() => CatalogueJson.Write(new Dict { ["pieces"] = _pieces, ["items"] = _items }, new CatalogueJson.Style(",", ":", SortKeys: true, Ascii: false));

        /// <summary>
        /// A localisation token ("$item_wood") as its English text; the token itself when it has none, a plain text as
        /// it is.
        /// </summary>
        private string En(string? token)
        {
            string t = Localization.Strip(token ?? "");
            return t.StartsWith('$') ? _english.GetValueOrDefault(t[1..], t) : t;
        }

        /// <summary>
        /// Reads the cost of each piece in the bundle.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            foreach (var (_, _, go, piece) in PieceRoots(b, game))
            {
                Add(b, go["m_Name"].AsString, piece);
            }
        }

        /// <summary>
        /// The GameObject a component reference (same file only) is on: its name, and the component.
        /// </summary>
        private static (string? Name, AssetTypeValueField? Component) Owner(Bundle b, AssetTypeValueField pptr)
        {
            if (pptr["m_FileID"].AsInt != 0 || b.Field(pptr["m_PathID"].AsLong) is not { } c)
            {
                return (null, null);
            }
            return (b.Field(c["m_GameObject.m_PathID"].AsLong)?["m_Name"].AsString, c);
        }
    }
}
