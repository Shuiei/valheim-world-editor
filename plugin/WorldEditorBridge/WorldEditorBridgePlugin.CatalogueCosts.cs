using System.Collections.Generic;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// A piece's cost as the editor's piece-cost data has it: English name "n", resources "r" ([item, amount,
    /// recover]) and crafting station "st"; each resource's English name goes into <paramref name="items"/>.
    /// </summary>
    private static string CostEntry(Piece piece, Dictionary<string, string> items)
    {
        List<string> res = [];
        foreach (Piece.Requirement r in piece.m_resources ?? [])
        {
            if (!r.m_resItem || r.m_amount <= 0)
            {
                continue;
            }
            string item = r.m_resItem.name;
            if (!items.ContainsKey(item))
            {
                items[item] = English(r.m_resItem.m_itemData?.m_shared?.m_name ?? item);
            }
            res.Add($"[{Json(item)},{r.m_amount},{(r.m_recover ? 1 : 0)}]");
        }
        string entry = $"{{\"n\":{Json(English(piece.m_name ?? piece.name))},\"r\":[{string.Join(",", res)}]";
        if (piece.m_craftingStation)
        {
            entry += ",\"st\":" + Json(English(piece.m_craftingStation.m_name ?? ""));
        }
        return entry + "}";
    }
}
