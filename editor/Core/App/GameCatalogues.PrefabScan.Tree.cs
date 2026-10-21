namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// Reads the bundle's trees for <c>Read</c>: the networked objects, the scripts with a role and the tree
        /// around them, in sweeps of the bundle. Returns the items' GameObjects.
        /// </summary>
        private List<(Bundle.Inner File, long Go)> ReadTrees(Bundle b)
        {
            void TakeIn(Bundle.Inner f, long pid, int type, Layout.Bytes d) => Take(b, f, pid, type, d);
            Layout.Parse take = TakeIn;
            if (_bigBundle)
            {
                Layout.Sweep(b, b.Objects.Where(o => o.Info.TypeId is GameObjectType or TransformType || (_bundleRoles.GetValueOrDefault(o.Info.PathId) & (OnRoot | Anywhere)) != 0).Select(o => (o.File, o.Info.PathId)), take);
                return ItemGameObjects();
            }
            Layout.Sweep(b, _nets.Concat(_scripts.Select(s => (s.File, s.Pid))), take);
            var gos = _netGos.Concat(_said.Select(s => (s.Key.Item1, s.Value.Go))).ToList();
            Layout.Sweep(b, gos, take);
            Layout.Sweep(b, TransformsOf(gos), take);
            var items = ItemGameObjects();
            var children = ChildrenOf(items).ToList();
            Layout.Sweep(b, children, take);
            Layout.Sweep(b, children.Where(_tree.Transforms.ContainsKey).Select(c => (c.Item1, _tree.Transforms[c].Go)), take);
            return items;
        }

        /// <summary>
        /// Takes one object read raw in a sweep: a tree object goes to the tree; a script is read whole when a prefab
        /// root or a script read anywhere needs it, and a networked script's GameObject is kept.
        /// </summary>
        private void Take(Bundle b, Bundle.Inner f, long pid, int type, Layout.Bytes d)
        {
            if (type != MonoBehaviourType)
            {
                _tree.Add(f, pid, type, d);
                return;
            }
            var r = _bundleRoles[pid];
            if (_bigBundle && (r & OnRoot) != 0)
            {
                b.Field(f, pid);
            }
            if ((r & Anywhere) != 0 && b.Read(f, pid) is { } s)
            {
                _said[(f, pid)] = Say(f, r, s);
            }
            if ((r & Role.Net) != 0 && d.I32(0) == 0)
            {
                _netGos.Add((f, d.I64(4)));
            }
        }

        /// <summary>
        /// The Transforms of these GameObjects (those in the tree).
        /// </summary>
        private IEnumerable<(Bundle.Inner, long)> TransformsOf(IEnumerable<(Bundle.Inner File, long Go)> gos) =>
            gos.Where(_tree.GameObjects.ContainsKey).SelectMany(g => _tree.GameObjects[g].Components.Where(c => Bundle.TypeOf(g.File, c.Pid) == TransformType).Select(c => (g.File, c.Pid)));

        /// <summary>
        /// The child Transforms of these GameObjects' Transforms (those in the tree).
        /// </summary>
        private IEnumerable<(Bundle.Inner, long)> ChildrenOf(IEnumerable<(Bundle.Inner File, long Go)> gos) =>
            TransformsOf(gos).Where(_tree.Transforms.ContainsKey).SelectMany(t => _tree.Transforms[t].Children.Select(c => (t.Item1, c)));

        /// <summary>
        /// The items' GameObjects: those of the scripts that say an item, once each.
        /// </summary>
        private List<(Bundle.Inner File, long Go)> ItemGameObjects() =>
            [.. _scripts.Where(s => _said.TryGetValue((s.File, s.Pid), out var x) && x.Item != null).Select(s => (s.File, _said[(s.File, s.Pid)].Go)).Distinct()];
    }
}
