using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
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
}
