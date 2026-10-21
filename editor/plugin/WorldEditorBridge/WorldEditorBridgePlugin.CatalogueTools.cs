using System.Collections.Generic;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// The build tool whose menu has each piece, by piece name: the game's own tools as the editor names them,
    /// a mod's tool by its item's name.
    /// </summary>
    private static Dictionary<string, string> PieceTools()
    {
        Dictionary<string, string> tools = [];
        if (!ObjectDB.instance)
        {
            return tools;
        }
        foreach (GameObject go in ObjectDB.instance.m_items)
        {
            // Unity's own null check (a destroyed object is not null to ?.).
            ItemDrop drop = go ? go.GetComponent<ItemDrop>() : null;
            PieceTable table = drop ? drop.m_itemData?.m_shared?.m_buildPieces : null;
            if (!table)
            {
                continue;
            }
            string tool = ToolTables.TryGetValue(table.name, out string known) ? known : go.name;
            foreach (GameObject piece in table.m_pieces)
            {
                if (piece && !tools.ContainsKey(piece.name))
                {
                    tools[piece.name] = tool;
                }
            }
        }
        return tools;
    }
}
