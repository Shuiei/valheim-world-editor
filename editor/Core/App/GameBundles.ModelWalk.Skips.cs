using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The model walk's scan that marks the parts to leave out: worn, broken and lower-detail ones.
/// </summary>
public sealed partial class GameBundles
{
    internal sealed partial class ModelWalk
    {
        internal void Scan(long goPid)
        {
            if (_owner.Read(_bundle, goPid) is not { } go)
            {
                return;
            }
            foreach (var (t, _, f) in _owner.Components(_bundle, go))
            {
                if (t == (int)AssetClassID.MonoBehaviour)
                {
                    foreach (string k in new[] { "m_worn", "m_broken" })
                    {
                        if (Has(f, k) && f[k]["m_FileID"].AsInt == 0 && f[k]["m_PathID"].AsLong != 0)
                        {
                            _skipGo.Add(f[k]["m_PathID"].AsLong);
                        }
                    }
                    if (Has(f, "m_new"))
                    {
                        long nr = f["m_new.m_PathID"].AsLong;
                        foreach (string k in new[] { "m_worn", "m_broken" })
                        {
                            if (Has(f, k) && f[k]["m_PathID"].AsLong == nr)
                            {
                                _skipGo.Remove(nr);
                            }
                        }
                    }
                }
                else if (t == (int)AssetClassID.LODGroup)
                {
                    var lods = f["m_LODs.Array"].Children;
                    for (int li = 1; li < lods.Count; li++)
                    {
                        foreach (var r in lods[li]["renderers.Array"].Children)
                        {
                            if (r["renderer.m_PathID"].AsLong != 0)
                            {
                                _lodSkip.Add(r["renderer.m_PathID"].AsLong);
                            }
                        }
                    }
                    if (lods.Count > 0)
                    {
                        foreach (var r in lods[0]["renderers.Array"].Children)
                        {
                            _lodSkip.Remove(r["renderer.m_PathID"].AsLong);
                        }
                    }
                }
                else if (t == (int)AssetClassID.Transform)
                {
                    foreach (long c in ChildObjects(f))
                    {
                        Scan(c);
                    }
                }
            }
        }
    }
}
