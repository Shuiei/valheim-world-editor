using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class Open
        {
            /// <summary>
            /// A GameObject's Transform and the MonoBehaviours that matter (with what they are), from its
            /// components (path ids).
            /// </summary>
            private (long Tr, List<(string Kind, long Pid)> Behaviours) Ids(IEnumerable<long> components)
            {
                long tr = 0;
                var behaviours = new List<(string, long)>();
                foreach (long pid in components)
                {
                    int t = B.TypeOf(pid);
                    if (t == TransformType)
                    {
                        tr = pid;
                    }
                    else if (t == MonoBehaviourType && KindOf(pid) is string k)
                    {
                        behaviours.Add((k, pid));
                    }
                }
                return (tr, behaviours);
            }

            /// <summary>
            /// A prefab root's Transform and MonoBehaviours that matter (those that can be read), read whole.
            /// </summary>
            public (AssetTypeValueField? Tr, List<(string Kind, AssetTypeValueField F)> Behaviours) Parts(AssetTypeValueField go)
            {
                var (tr, ids) = Ids(go["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong));
                var behaviours = new List<(string, AssetTypeValueField)>();
                foreach (var (k, pid) in ids)
                {
                    if (B.Field(pid) is { } f)
                    {
                        behaviours.Add((k, f));
                    }
                }
                return (tr == 0 ? null : B.Field(tr), behaviours);
            }

            /// <summary>
            /// A GameObject of the trees read: its name, whether it is active, its Transform (0: none) and
            /// the MonoBehaviours that matter (path ids); null when it was not read.
            /// </summary>
            public (string Name, bool Active, long Tr, List<(string Kind, long Pid)> Behaviours)? Node(long go)
            {
                if (_nodes.TryGetValue(go, out var node))
                {
                    return node;
                }
                if (!_gos.TryGetValue(go, out var g))
                {
                    return null;
                }
                var (tr, ids) = Ids(g.Components);
                return _nodes[go] = (g.Name, g.Active, tr, ids);
            }

            /// <summary>
            /// A Transform of the trees: its local position and rotation, and its children's GameObjects
            /// (children not read left out); null when it was not read.
            /// </summary>
            public (V3 Pos, Quat Rot, List<long> Children)? Transform(long tr)
            {
                if (_frames.TryGetValue(tr, out var frame))
                {
                    return frame;
                }
                if (!_transforms.TryGetValue(tr, out var t))
                {
                    return null;
                }
                var children = new List<long>();
                foreach (long c in t.Children)
                {
                    if (_transforms.TryGetValue(c, out var ct))
                    {
                        children.Add(ct.Go);
                    }
                }
                return _frames[tr] = (t.Pos, t.Rot, children);
            }
        }
    }
}
