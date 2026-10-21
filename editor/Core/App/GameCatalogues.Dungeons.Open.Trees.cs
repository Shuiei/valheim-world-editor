namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class Open
        {
            /// <summary>
            /// Keeps what the scan needs of one object read raw. A GameObject has its components first (a count,
            /// then each a file id and a path id), then its layer, name (aligned to 4 bytes), tag (2 bytes) and
            /// m_IsActive. A Transform has its GameObject, rotation (x, y, z, w), position and scale, then its
            /// children. A MonoBehaviour has its GameObject, then m_Enabled.
            /// </summary>
            private void Take(Bundle.Inner f, long pid, int type, Layout.Bytes d)
            {
                if (type == GameObjectType)
                {
                    long[] comps = new long[d.I32(0)];
                    for (int i = 0; i < comps.Length; i++)
                    {
                        comps[i] = d.I64(8 + 12 * i);
                    }
                    int name = 8 + 12 * comps.Length;
                    _gos[pid] = (d.Text(name), comps, d.U8(name + 4 + ((d.I32(name) + 3) & ~3) + 2) != 0);
                }
                else if (type == TransformType)
                {
                    long[] children = d.ChildIds(52);
                    _transforms[pid] = (d.I64(4), children, (d.F32(28), d.F32(32), d.F32(36)), new Quat(d.F32(12), d.F32(16), d.F32(20), d.F32(24)));
                }
                else if (type == MonoBehaviourType)
                {
                    _enabled[pid] = d.U8(12) != 0;
                }
            }

            /// <summary>
            /// Each path id with the file in the bundle that has it (those not there left out).
            /// </summary>
            private IEnumerable<(Bundle.Inner, long)> At(IEnumerable<long> pids)
            {
                foreach (long pid in pids)
                {
                    if (B.FileWith(pid) is { } f)
                    {
                        yield return (f, pid);
                    }
                }
            }

            /// <summary>
            /// The trees read raw, a level at a time: the roots' GameObjects, their Transforms, then each level's
            /// GameObjects with the next level's Transforms, until no Transform has children.
            /// </summary>
            /// <param name="roots">The roots' GameObjects.</param>
            private void ReadTreeLevels(List<long> roots)
            {
                Layout.Sweep(B, At(roots), Take);
                var transforms = roots.Select(g => Node(g)?.Tr ?? 0).Where(t => t != 0).ToList();
                Layout.Sweep(B, At(transforms), Take);
                while (transforms.Count > 0)
                {
                    var nodes = transforms.Where(_transforms.ContainsKey).Select(t => _transforms[t]).ToList();
                    transforms = [.. nodes.SelectMany(n => n.Children)];
                    Layout.Sweep(B, At(nodes.Select(n => n.Go).Concat(transforms)), Take);
                }
            }

            /// <summary>
            /// Records each GameObject as the parent of its children's GameObjects, for those whose trees were read.
            /// </summary>
            private void LinkParents()
            {
                foreach (var (tr, t) in _transforms)
                {
                    if (_gos.ContainsKey(t.Go))
                    {
                        foreach (long child in Transform(tr)!.Value.Children)
                        {
                            _parents[child] = t.Go;
                        }
                    }
                }
            }
        }
    }
}
