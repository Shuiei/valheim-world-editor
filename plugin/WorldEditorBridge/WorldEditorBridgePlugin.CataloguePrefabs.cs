using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Every prefab the game knows: ZNetScene's (its list and its table, as some mods fill only one) and
    /// ObjectDB's items, each once, by name.
    /// </summary>
    private static List<GameObject> AllPrefabs()
    {
        Dictionary<string, GameObject> byName = [];
        void Add(GameObject go)
        {
            if (go && !byName.ContainsKey(go.name))
            {
                byName[go.name] = go;
            }
        }
        foreach (GameObject go in ZNetScene.instance.m_prefabs)
        {
            Add(go);
        }
        if (NamedPrefabs?.GetValue(ZNetScene.instance) is Dictionary<int, GameObject> named)
        {
            foreach (GameObject go in named.Values)
            {
                Add(go);
            }
        }
        if (ObjectDB.instance)
        {
            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                Add(go);
            }
        }
        return [.. byName.Values];
    }
    /// <summary>
    /// A prefab's entry as the editor's prefabs data has it: persistent "p", distant "d", object type "t", creature "c", item "i" and its type "it", container grid "cw"/"ch",
    /// ward radius "ward", build range "build".
    /// </summary>
    private static string PrefabEntry(GameObject go)
    {
        StringBuilder sb = new("{");
        ZNetView view = go.GetComponent<ZNetView>();
        sb.Append($"\"p\":{(view.m_persistent ? 1 : 0)},\"d\":{(view.m_distant ? 1 : 0)},\"t\":{(int)view.m_type}");
        if (go.GetComponent<Character>())
        {
            sb.Append(",\"c\":1");
        }
        if (go.GetComponent<ItemDrop>() is { } drop && drop)
        {
            sb.Append($",\"i\":1,\"it\":{(int)(drop.m_itemData?.m_shared?.m_itemType ?? 0)}");
        }
        if (go.GetComponent<Container>() is { } box && box)
        {
            sb.Append($",\"cw\":{box.m_width},\"ch\":{box.m_height}");
        }
        if (go.GetComponent<PrivateArea>() is { } ward && ward)
        {
            sb.Append($",\"ward\":{F(ward.m_radius)}");
        }
        if (go.GetComponent<CraftingStation>() is { } station && station)
        {
            sb.Append($",\"build\":{F(station.m_rangeBuild)}");
        }
        return sb.Append('}').ToString();
    }
}
