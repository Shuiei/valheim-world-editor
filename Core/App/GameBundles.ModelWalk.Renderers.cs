using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The model walk's renderers: the submeshes of a renderer placed as the model's parts.
/// </summary>
public sealed partial class GameBundles
{
    internal sealed partial class ModelWalk
    {
        /// <summary>
        /// Places the submeshes of an object's renderer (its mesh filter's mesh, or a skinned renderer's) as parts at
        /// its transform, one per material, when the renderer is on and not left out for a lower level of detail.
        /// </summary>
        /// <param name="cs">The object's components as (type, path id, fields).</param>
        /// <param name="w">The object's transform in the model.</param>
        private void AddRendererParts(List<(int Type, long Pid, AssetTypeValueField F)> cs, float[] w)
        {
            var mf = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshFilter);
            var mr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshRenderer);
            var smr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.SkinnedMeshRenderer);
            AssetTypeValueField? meshRef = mf.F?["m_Mesh"];
            if (smr.F != null && mr.F == null)
            {
                mr = smr;
                meshRef = smr.F["m_Mesh"];
            }
            if (meshRef != null && mr.F != null && mr.F["m_Enabled"].AsBool && !_lodSkip.Contains(mr.Pid) && _owner.Resolve(_bundle, meshRef) is Where mw)
            {
                int subs = _owner.SubmeshCount(mw);
                var mats = mr.F["m_Materials.Array"].Children;
                for (int si = 0; si < mats.Count && si < subs; si++)
                {
                    Parts.Add(new Part(mw, si, _owner.Resolve(_bundle, mats[si]), w));
                }
            }
        }
    }
}
