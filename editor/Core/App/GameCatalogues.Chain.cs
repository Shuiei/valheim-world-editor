using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// Walks up a prefab, for the scans that go from a script up to its prefab rather than down every prefab's objects.
    /// </summary>
    private static class Chain
    {
        /// <summary>
        /// From an object up to its prefab root: the root, the Transforms from below the root down to the
        /// object, whether all of them are active (the root too), and the child index at each step. Null
        /// when something on the way cannot be read.
        /// </summary>
        public static (long Root, List<AssetTypeValueField> Chain, bool Active, List<int> Index)? Up(Bundle b, long goPid)
        {
            var chain = new List<AssetTypeValueField>();
            var index = new List<int>();
            bool active = true;
            for (int guard = 0; guard < 1000; guard++)
            {
                if (b.Field(goPid) is not { } go)
                {
                    return null;
                }
                active &= Active(go);
                AssetTypeValueField? tr = null;
                long trPid = 0;
                foreach (var (t, pid) in b.ComponentIds(go))
                {
                    if (t == TransformType && b.Field(pid) is { } f)
                    {
                        (tr, trPid) = (f, pid);
                    }
                }
                if (tr == null)
                {
                    return null;
                }
                long father = tr["m_Father.m_PathID"].AsLong;
                if (father == 0)
                {
                    chain.Reverse();
                    index.Reverse();
                    return (goPid, chain, active, index);
                }
                if (b.Field(father) is not { } parent)
                {
                    return null;
                }
                chain.Add(tr);
                index.Add(parent["m_Children.Array"].Children.FindIndex(c => c["m_PathID"].AsLong == trPid));
                goPid = parent["m_GameObject.m_PathID"].AsLong;
            }
            return null;
        }

        /// <summary>
        /// The order of two walks down a prefab (their child indices, the shorter first when one is the start of the other).
        /// </summary>
        public static int Order(List<int> a, List<int> c)
        {
            for (int i = 0; i < Math.Min(a.Count, c.Count); i++)
            {
                if (a[i] != c[i])
                {
                    return a[i].CompareTo(c[i]);
                }
            }
            return a.Count.CompareTo(c.Count);
        }
    }
}
