using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// A dungeon's door types: [prefab, opening type, chance] for each one whose prefab is in the same file.
        /// </summary>
        private static List<object?> Doors(Bundle b, AssetTypeValueField g)
        {
            var res = new List<object?>();
            if (!Has(g, "m_doorTypes"))
            {
                return res;
            }
            foreach (var d in g["m_doorTypes.Array"].Children)
            {
                var pptr = d["m_prefab"];
                long pid = pptr["m_PathID"].AsLong;
                if (pptr["m_FileID"].AsInt == 0 && pid != 0 && b.Field(pid) is { } go && Has(go, "m_Name"))
                {
                    res.Add(new List<object?> { go["m_Name"].AsString, Py(d["m_connectionType"]), Round(Get(d, "m_chance", 0L), 4) });
                }
            }
            return res;
        }
    }
}
