using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The model walk's room pass: which networked, random and distant objects of a room are left out.
/// </summary>
public sealed partial class GameBundles
{
    internal sealed partial class ModelWalk
    {
        internal void RoomSkips(long goPid, bool isRoot)
        {
            if (_owner.Read(_bundle, goPid) is not { } go)
            {
                return;
            }
            AssetTypeValueField? tr = null;
            foreach (var (t, _, f) in _owner.Components(_bundle, go))
            {
                if (t == (int)AssetClassID.Transform)
                {
                    tr = f;
                }
                if (t != (int)AssetClassID.MonoBehaviour)
                {
                    continue;
                }
                if (!isRoot && Has(f, "m_persistent") && Has(f, "m_distant"))
                {
                    _skipGo.Add(goPid);
                }
                else if (Has(f, "m_chanceToSpawn") && Has(f, "m_OffObject"))
                {
                    long off = f["m_OffObject.m_PathID"].AsLong;
                    if (f["m_chanceToSpawn"].AsFloat >= 50)
                    {
                        if (off != 0)
                        {
                            _skipGo.Add(off);
                        }
                    }
                    else
                    {
                        _skipGo.Add(goPid);
                    }
                }
                else if (Has(f, "m_objects") && Has(f, "m_dungeonRequireTheme"))
                {
                    foreach (var e in f["m_objects.Array"].Children.Skip(1))
                    {
                        long c = e["m_object.m_PathID"].AsLong;
                        if (c != 0)
                        {
                            _skipGo.Add(c);
                        }
                    }
                }
            }
            if (tr != null)
            {
                foreach (long c in ChildObjects(tr))
                {
                    RoomSkips(c, false);
                }
            }
        }
    }
}
