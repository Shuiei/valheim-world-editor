using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
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
    /// <summary>A $token in the game's language (English on a server), as the game shows it; plain text as it is.</summary>
    private static string English(string token) => Localization.instance != null ? Localization.instance.Localize(token) : token;

    /// <summary>A coordinate for JSON, to the millimetre as the editor's own reading rounds it.</summary>
    private static string R(float v) => Math.Round(v, 3).ToString("R", CultureInfo.InvariantCulture);
}
