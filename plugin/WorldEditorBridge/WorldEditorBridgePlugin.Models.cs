using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
    /// <summary>
    /// POST /models: the body holds prefab names, one per line; the answer is JSON, by prefab name (prefabs the game does
    /// not have are left out):
    /// <code>
    ///   "s"  the root's scale [x, y, z]
    ///   "p"  the parts: "mesh" its name, "v" its vertex count, "b" its bounds (centre and extent, x y z each: meshes of
    ///        the same name told apart, Unity's own primitives known), "sub" the submesh, "mat" the material's name, "tex" its
    ///        main texture's name (when it has one), "col" its colour [r, g, b, a], "m" the part's matrix in the root's
    ///        frame (16 numbers, row by row, the root's own position, rotation and scale left out)
    /// </code>
    /// As the editor reads the game's own models: active objects and enabled renderers only, the first level of detail,
    /// a piece's new look (not its worn or broken ones).
    /// </summary>
    private static byte[] Models(byte[] body)
    {
        Dictionary<string, GameObject> byName = [];
        foreach (GameObject go in AllPrefabs())
        {
            byName[go.name] = go;
        }
        StringBuilder sb = new("{");
        foreach (string name in Encoding.UTF8.GetString(body).Split('\n').Select(n => n.Trim()).Where(n => n.Length > 0).Distinct())
        {
            if (!byName.TryGetValue(name, out GameObject go))
            {
                continue;
            }
            if (sb.Length > 1)
            {
                sb.Append(',');
            }
            sb.Append(Json(name)).Append(':').Append(ModelOf(go));
        }
        return Encoding.UTF8.GetBytes(sb.Append('}').ToString());
    }

    /// <summary>One prefab's model (see <see cref="Models"/>).</summary>
    private static string ModelOf(GameObject go)
    {
        Transform root = go.transform;
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
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        List<string> parts = [];
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : r is MeshRenderer && r.GetComponent<MeshFilter>() is { } filter && filter ? filter.sharedMesh : null;
            if (!mesh || !r.enabled || lower.Contains(r) || !ActiveUnder(r.transform, root) || Skipped(r.transform, root, skip))
            {
                continue;
            }
            Matrix4x4 m = toRoot * r.transform.localToWorldMatrix;
            Bounds b = mesh.bounds;
            // A degenerate transform gives NaN or infinity, which is not JSON: such a part is left out.
            if (!Enumerable.Range(0, 16).All(i => Finite(m[i / 4, i % 4])) || !Finite(b.center) || !Finite(b.extents))
            {
                continue;
            }
            string matrix = string.Join(",", Enumerable.Range(0, 16).Select(i => R6(m[i / 4, i % 4])));
            Material[] mats = r.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
            {
                Material mat = mats[sub];
                StringBuilder part = new($"{{\"mesh\":{Json(mesh.name)},\"v\":{mesh.vertexCount},\"b\":[{R6(b.center.x)},{R6(b.center.y)},{R6(b.center.z)},{R6(b.extents.x)},{R6(b.extents.y)},{R6(b.extents.z)}],\"sub\":{sub}");
                if (mat)
                {
                    part.Append(",\"mat\":").Append(Json(mat.name));
                    if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") is { } tex && tex)
                    {
                        part.Append(",\"tex\":").Append(Json(tex.name));
                    }
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.GetColor("_Color");
                        part.Append($",\"col\":[{R6(c.r)},{R6(c.g)},{R6(c.b)},{R6(c.a)}]");
                    }
                }
                parts.Add(part.Append($",\"m\":[{matrix}]}}").ToString());
            }
        }
        Vector3 s = root.localScale;
        return $"{{\"s\":[{R6(s.x)},{R6(s.y)},{R6(s.z)}],\"p\":[{string.Join(",", parts)}]}}";
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

    /// <summary>A number for JSON to six decimals (a matrix's, a colour's).</summary>
    private static string R6(float v) => System.Math.Round(v, 6).ToString("R", CultureInfo.InvariantCulture);
}
