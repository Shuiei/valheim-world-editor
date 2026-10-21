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
    /// <summary>The mesh a renderer draws: a skinned mesh's, or a mesh filter's on a mesh renderer; null for others.</summary>
    /// <param name="r">The renderer.</param>
    private static Mesh MeshOf(Renderer r)
    {
        return r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
            : r is MeshRenderer && r.GetComponent<MeshFilter>() is { } filter && filter ? filter.sharedMesh : null;
    }

    /// <summary>Adds a part's material fields (name, main texture, colour) to its JSON, when it has a material.</summary>
    /// <param name="part">The part's JSON so far.</param>
    /// <param name="mat">The part's material; null when it has none.</param>
    private static void AppendMaterial(StringBuilder part, Material mat)
    {
        if (!mat)
        {
            return;
        }
        part.Append(",\"mat\":").Append(Json(mat.name));
        if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") is { } tex && tex)
        {
            part.Append(",\"tex\":").Append(Json(tex.name));
        }
        if (mat.HasProperty("_Color") && mat.GetColor("_Color") is var colour && Finite(colour.r) && Finite(colour.g) && Finite(colour.b) && Finite(colour.a))
        {
            part.Append($",\"col\":[{R6(colour.r)},{R6(colour.g)},{R6(colour.b)},{R6(colour.a)}]");
        }
    }
}
