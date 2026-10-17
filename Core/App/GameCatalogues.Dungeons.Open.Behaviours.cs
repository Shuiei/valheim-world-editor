namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class Open
        {
            /// <summary>
            /// These GameObjects' trees read raw, in the order of their files, a level at a time (a level's
            /// GameObjects with the next level's Transforms); then the MonoBehaviours that matter where the
            /// rooms are active (on a root: its openings), the ZNetViews raw, the others whole.
            /// </summary>
            /// <param name="roots">The roots' GameObjects, the first one the room or dungeon itself.</param>
            public void ReadTrees(List<long> roots)
            {
                ReadTreeLevels(roots);
                LinkParents();
                var behaviours = CollectActiveBehaviours(roots);
                var raw = ReadBehaviours(behaviours);
                VerifyRawReads(raw);
                Check(roots[0]);
                Check(Transform(Node(roots[0])?.Tr ?? 0)?.Children.FirstOrDefault() ?? 0);
                VerifyEnabled(behaviours);
            }

            /// <summary>
            /// The MonoBehaviours that matter in the active part of each root's tree, in the order of the walk.
            /// </summary>
            /// <param name="roots">The roots' GameObjects.</param>
            private List<(string Kind, long Pid)> CollectActiveBehaviours(List<long> roots)
            {
                var behaviours = new List<(string Kind, long Pid)>();
                foreach (long root in roots)
                {
                    CollectActive(root, true, behaviours);
                }
                return behaviours;
            }

            /// <summary>
            /// Adds the MonoBehaviours of one GameObject that is active (a root's openings only), then its children's.
            /// </summary>
            /// <param name="go">The GameObject.</param>
            /// <param name="root">Whether it is the root.</param>
            /// <param name="behaviours">The behaviours found so far.</param>
            private void CollectActive(long go, bool root, List<(string Kind, long Pid)> behaviours)
            {
                if (Node(go) is not { } g || !root && !g.Active)
                {
                    return;
                }
                behaviours.AddRange(root ? g.Behaviours.Where(x => x.Kind == "connection") : g.Behaviours);
                foreach (long child in Transform(g.Tr)?.Children ?? [])
                {
                    CollectActive(child, false, behaviours);
                }
            }

            /// <summary>
            /// Reads the ZNetViews and the RandomSpawns raw, and the other behaviours whole.
            /// </summary>
            /// <param name="behaviours">The behaviours that matter.</param>
            /// <returns>The RandomSpawns read raw.</returns>
            private List<long> ReadBehaviours(List<(string Kind, long Pid)> behaviours)
            {
                Layout.Sweep(B, At(behaviours.Where(x => x.Kind == "netview").Select(x => x.Pid)), Take);
                var raw = behaviours.Where(x => x.Kind == "spawn" && Plan(x.Pid) != null).Select(x => x.Pid).ToList();
                Layout.Sweep(B, At(raw), (f, pid, _, d) =>
                {
                    var (tree, last) = Plan(pid)!.Value;
                    _spawns[pid] = Raw.Fields(tree, last, SpawnFields, d);
                });
                B.ReadInOrder(behaviours.Where(x => x.Kind != "netview" && !_spawns.ContainsKey(x.Pid)).Select(x => x.Pid));
                return raw;
            }
        }
    }
}
