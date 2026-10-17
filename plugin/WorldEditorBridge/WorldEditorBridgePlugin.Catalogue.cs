using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WorldEditorBridge;

/// <summary>
/// GET /catalogue: every prefab the running game knows, as the editor reads the game's own prefabs from its files,
/// so the editor learns the prefabs mods add (it keeps only those its own reading of the game does not have).
/// </summary>
public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>Format version of /catalogue's JSON ("version").</summary>
    private const int CatalogueVersion = 1;

    /// <summary>Jötunn's plugin id: when it is installed, it tells which mod made each prefab.</summary>
    private const string JotunnGuid = "com.jotunn.jotunn";

    /// <summary>ZNetScene's private table of every prefab by name hash (some mods add theirs only there).</summary>
    private static readonly FieldInfo NamedPrefabs = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// The game's build tools' piece tables, as the editor names the tools; a mod's tool is named after its item
    /// ("odin_hammer", "BFP_FineHammer").
    /// </summary>
    private static readonly Dictionary<string, string> ToolTables = new()
    {
        ["_HammerPieceTable"] = "hammer",
        ["_HoePieceTable"] = "hoe",
        ["_CultivatorPieceTable"] = "cultivator",
        ["_FeasterPieceTable"] = "feaster",
    };

    /// <summary>
    /// Asks Jötunn, when installed, to note which mod adds each prefab (it must be asked before the game's start
    /// screen). Nothing without Jötunn.
    /// </summary>
    private void EnableModQuery()
    {
        try
        {
            var query = Type.GetType("Jotunn.Utils.ModQuery, Jotunn");
            query?.GetMethod("Enable", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Caught(ex, "asking Jötunn to note which mod adds each prefab (no mod names)");
        }
    }

    /// <summary>
    /// The mod that added each prefab, by prefab name, as Jötunn noted it; empty without Jötunn.
    /// </summary>
    private Dictionary<string, string> PrefabMods()
    {
        Dictionary<string, string> mods = [];
        // A mod's name once per plugin (a manifest read), not once per prefab: a mod adds thousands.
        Dictionary<string, string> names = [];
        try
        {
            var query = Type.GetType("Jotunn.Utils.ModQuery, Jotunn");
            if (query?.GetMethod("GetPrefabs", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null) is not IEnumerable prefabs)
            {
                return mods;
            }
            foreach (object p in prefabs)
            {
                Type t = p.GetType();
                if (t.GetProperty("Prefab")?.GetValue(p) is GameObject go && go && t.GetProperty("SourceMod")?.GetValue(p) is BepInEx.BepInPlugin mod)
                {
                    if (!names.TryGetValue(mod.GUID, out string name))
                    {
                        names[mod.GUID] = name = ModName(mod);
                    }
                    mods[go.name] = name;
                }
            }
        }
        catch (Exception ex)
        {
            Caught(ex, "asking Jötunn which mod adds each prefab (no mod names)");
        }
        return mods;
    }

    /// <summary>
    /// A mod's name as players know it: the package it was installed from (the folder it is in under plugins, as mod
    /// managers make them: its manifest.json's name, else the folder's name without its "Author-"), else the
    /// plugin's own name. A package can hold several plugins, and Jötunn names the one that added a prefab
    /// (ValheimRAFT's are added by its "Inventory Patch Detector").
    /// </summary>
    private string ModName(BepInEx.BepInPlugin plugin)
    {
        if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(plugin.GUID, out BepInEx.PluginInfo info) || string.IsNullOrEmpty(info.Location))
        {
            return plugin.Name;
        }
        string plugins = Path.GetFullPath(BepInEx.Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string dll = Path.GetFullPath(info.Location);
        if (!dll.StartsWith(plugins, StringComparison.OrdinalIgnoreCase))
        {
            return plugin.Name;
        }
        string[] parts = dll.Substring(plugins.Length).Split(Path.DirectorySeparatorChar);
        if (parts.Length < 2)
        {
            return plugin.Name;
        }
        string folder = parts[0];
        try
        {
            string manifest = Path.Combine(plugins + folder, "manifest.json");
            if (File.Exists(manifest)
                && Regex.Match(File.ReadAllText(manifest), "\"name\"\\s*:\\s*\"([^\"]+)\"") is { Success: true } m)
            {
                return m.Groups[1].Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable manifest: the folder's name.
            Caught(ex, $"reading the manifest.json of {folder} (its folder's name is used)", brief: true);
        }
        int dash = folder.IndexOf('-');
        return dash > 0 && dash < folder.Length - 1 ? folder.Substring(dash + 1) : folder;
    }

    /// <summary>
    /// GET /catalogue, as JSON (each entry in the format of the editor's game data of that kind):
    /// <code>
    ///   "version"   the format version
    ///   "prefabs"   name: save flags and what the editor knows of the kind ("p", "d", "t", "c", "i", "it", "cw"...)
    ///   "pieces"    name: build menu category, comfort, footprint box, snap points and build tool
    ///   "cost"      "pieces": name: English name, resources and crafting station; "items": item: English name
    ///   "mods"      name: the mod that added it (Jötunn's knowledge; only with Jötunn)
    /// </code>
    /// Every prefab with a ZNetView (what can be saved) is listed, vanilla ones too: the editor tells them apart.
    /// </summary>
    private byte[] Catalogue()
    {
        List<GameObject> all = AllPrefabs();
        Dictionary<string, string> tools = PieceTools();
        Dictionary<string, string> items = [];
        StringBuilder prefabs = new(), pieces = new(), costs = new();
        foreach (GameObject go in all)
        {
            // Without a ZNetView a prefab is never saved: nothing for the editor.
            if (!go.GetComponent<ZNetView>())
            {
                continue;
            }
            Comma(prefabs).Append(Json(go.name)).Append(':').Append(PrefabEntry(go));
            if (go.GetComponent<Piece>() is { } piece && piece)
            {
                Comma(pieces).Append(Json(go.name)).Append(':').Append(PieceEntry(go, piece, tools.TryGetValue(go.name, out string tool) ? tool : null));
                Comma(costs).Append(Json(go.name)).Append(':').Append(CostEntry(piece, items));
            }
        }
        StringBuilder mods = new();
        foreach (KeyValuePair<string, string> mod in PrefabMods())
        {
            Comma(mods).Append(Json(mod.Key)).Append(':').Append(Json(mod.Value));
        }
        StringBuilder names = new();
        foreach (KeyValuePair<string, string> item in items)
        {
            Comma(names).Append(Json(item.Key)).Append(':').Append(Json(item.Value));
        }
        string json = $"{{\"version\":{CatalogueVersion},\"prefabs\":{{{prefabs}}},\"pieces\":{{{pieces}}},\"cost\":{{\"pieces\":{{{costs}}},\"items\":{{{names}}}}},\"mods\":{{{mods}}}}}";
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>A comma before every entry but the first.</summary>
    private static StringBuilder Comma(StringBuilder sb) => sb.Length > 0 ? sb.Append(',') : sb;

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

    /// <summary>
    /// A piece's entry as the editor's pieces data has it: category "cat", comfort, the box around its active solid
    /// box colliders "box" (min x, max x, min z, max z, min y, max y), its snap points "snap" (children tagged or named
    /// snappoint, inactive ones too), all in the piece's own frame, and its build tool.
    /// </summary>
    private static string PieceEntry(GameObject go, Piece piece, string tool)
    {
        Transform root = go.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        StringBuilder sb = new($"{{\"cat\":{(int)piece.m_category},\"comfort\":{piece.m_comfort}");
        List<Vector3> snaps = [];
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && (t.CompareTag("snappoint") || t.name.IndexOf("snappoint", StringComparison.OrdinalIgnoreCase) >= 0)
                && toRoot.MultiplyPoint3x4(t.position) is var p && Finite(p))
            {
                snaps.Add(p);
            }
        }
        if (snaps.Count > 0)
        {
            sb.Append(",\"snap\":[").Append(string.Join(",", snaps.Select(p => $"[{R(p.x)},{R(p.y)},{R(p.z)}]"))).Append(']');
        }
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (BoxCollider c in go.GetComponentsInChildren<BoxCollider>(true))
        {
            if (c.isTrigger || !c.enabled || !ActiveUnder(c.transform, root))
            {
                continue;
            }
            Matrix4x4 m = toRoot * c.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c.center + Vector3.Scale(c.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        // A degenerate transform (zero scale) gives no box: NaN or infinity is not JSON.
        if (min.x <= max.x && Finite(min) && Finite(max))
        {
            sb.Append($",\"box\":[{R(min.x)},{R(max.x)},{R(min.z)},{R(max.z)},{R(min.y)},{R(max.y)}]");
        }
        if (tool != null)
        {
            sb.Append(",\"tool\":").Append(Json(tool));
        }
        return sb.Append('}').ToString();
    }

    /// <summary>Whether a point's coordinates are all numbers (not NaN or infinite), as JSON needs.</summary>
    private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);

    /// <summary>Whether a number is one (not NaN or infinite), as JSON needs.</summary>
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    /// <summary>Whether a child and every parent up to the root is active (the root's own state does not count).</summary>
    private static bool ActiveUnder(Transform t, Transform root)
    {
        for (; t && t != root; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
            {
                return false;
            }
        }
        return true;
    }

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

    /// <summary>A $token in the game's language (English on a server), as the game shows it; plain text as it is.</summary>
    private static string English(string token) => Localization.instance != null ? Localization.instance.Localize(token) : token;

    /// <summary>A coordinate for JSON, to the millimetre as the editor's own reading rounds it.</summary>
    private static string R(float v) => Math.Round(v, 3).ToString("R", CultureInfo.InvariantCulture);
}
