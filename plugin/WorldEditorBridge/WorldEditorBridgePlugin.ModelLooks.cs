using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldEditorBridge;

/// <summary>
/// POST /models: what the game draws of some prefabs, by the names of their meshes, materials and textures, so the
/// editor can find those in the game's and the mods' bundles. Mods make most of their prefabs while the game runs
/// (clones of the game's pieces, placeholders swapped for the game's meshes and materials): only the running game
/// knows what they look like.
/// </summary>
public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>The looks a piece leaves out: its worn and broken ones, but not the look it is new in.</summary>
    /// <param name="go">The prefab.</param>
    private static HashSet<GameObject> SkippedLooks(GameObject go)
    {
        HashSet<GameObject> skip = [];
        foreach (WearNTear wear in go.GetComponentsInChildren<WearNTear>(true))
        {
            foreach (GameObject look in new[] { wear.m_worn, wear.m_broken })
            {
                if (look && look != wear.m_new)
                {
                    skip.Add(look);
                }
            }
        }
        return skip;
    }

    /// <summary>The renderers of the prefab's lower levels of detail (not those of its first level).</summary>
    /// <param name="go">The prefab.</param>
    private static HashSet<Renderer> LowerLodRenderers(GameObject go)
    {
        HashSet<Renderer> lower = [];
        foreach (LODGroup group in go.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
            {
                lower.UnionWith(lods[i].renderers.Where(r => r));
            }
            if (lods.Length > 0)
            {
                lower.ExceptWith(lods[0].renderers.Where(r => r));
            }
        }
        return lower;
    }

    /// <summary>Whether an object is, or is under, one of the looks left out (up to the root).</summary>
    private static bool Skipped(Transform t, Transform root, HashSet<GameObject> skip)
    {
        for (; t && t != root; t = t.parent)
        {
            if (skip.Contains(t.gameObject))
            {
                return true;
            }
        }
        return false;
    }
}
