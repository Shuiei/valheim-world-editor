namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class WakeScan
    {
        /// <summary>
        /// The GameObjects read of the bundle being read, raw (reset per bundle): name, whether active, components.
        /// </summary>
        private readonly Dictionary<(Bundle.Inner, long), (string Name, bool Active, (int File, long Pid)[] Components)> _gos = [];
        /// <summary>
        /// The Transforms read of the bundle being read, raw (reset per bundle): GameObject, parent (0: none, null: in
        /// another file), children.
        /// </summary>
        private readonly Dictionary<(Bundle.Inner, long), (long Go, long? Father, long[] Children)> _transforms = [];

        /// <summary>
        /// Reads raw the scripts' GameObjects and everything above them up to their prefabs' roots, a level at a time
        /// (each level one sweep of the bundle, in the order of its files); the scripts whose fields matter are read
        /// whole in the first sweep. A bundle the block cache cannot hold has its whole tree read in that one sweep
        /// instead: each sweep would read all of it again.
        /// </summary>
        private void ReadUp(Bundle b, List<(Bundle.Inner File, long Go)> gos, IEnumerable<(Bundle.Inner File, long Pid)> whole)
        {
            _gos.Clear();
            _transforms.Clear();
            if (b.Files.Sum(f => f.Assets.file.Header.FileSize) > BlockCache.Bytes)
            {
                Layout.Sweep(b, b.Objects.Where(o => o.Info.TypeId is GameObjectType or TransformType).Select(o => (o.File, o.Info.PathId)), Take, whole);
                return;
            }
            Layout.Sweep(b, gos, Take, whole);
            var level = gos.Where(_gos.ContainsKey).Select(g => TransformOf(g.File, _gos[g].Components) is long t ? (g.File, t) : default).Where(t => t.File != null).Distinct().ToList();
            Layout.Sweep(b, level, Take);
            while (level.Count > 0)
            {
                var fathers = level.Where(_transforms.ContainsKey).Select(t => (t.File, Father: _transforms[t].Father ?? 0)).Where(t => t.Father != 0 && !_transforms.ContainsKey(t)).Distinct().ToList();
                Layout.Sweep(b, fathers, Take);
                Layout.Sweep(b, fathers.Where(_transforms.ContainsKey).Select(t => (t.File, _transforms[t].Go)).Where(g => !_gos.ContainsKey(g)).ToList(), Take);
                level = fathers;
            }
        }

        /// <summary>Takes a GameObject or a Transform read raw (Layout.GameObjectOf, Layout.TransformOf).</summary>
        private void Take(Bundle.Inner f, long pid, int type, Layout.Bytes d)
        {
            if (type == GameObjectType)
            {
                _gos[(f, pid)] = Layout.GameObjectOf(d);
            }
            else if (type == TransformType)
            {
                _transforms[(f, pid)] = Layout.TransformOf(d);
            }
        }

        /// <summary>A GameObject's Transform among its components; null when it has none in its file.</summary>
        private static long? TransformOf(Bundle.Inner f, (int File, long Pid)[] comps) =>
            comps.Where(c => c.File == 0 && Bundle.TypeOf(f, c.Pid) == TransformType).Select(c => (long?)c.Pid).FirstOrDefault();

        /// <summary>
        /// From a GameObject up to its prefab's root, as read: the root, whether every object on the way is active (the
        /// root aside: a prefab wakes even when its root is not), and the child index at each step down. Null when
        /// something on the way was not read.
        /// </summary>
        private (long Root, bool Active, List<int> Path)? Up(Bundle.Inner f, long go)
        {
            bool active = true;
            var path = new List<int>();
            for (int guard = 0; guard < 1000; guard++)
            {
                if (!_gos.TryGetValue((f, go), out var g) || TransformOf(f, g.Components) is not long tr || !_transforms.TryGetValue((f, tr), out var t) || t.Father is not long father)
                {
                    return null;
                }
                if (father == 0)
                {
                    path.Reverse();
                    return (go, active, path);
                }
                if (!_transforms.TryGetValue((f, father), out var parent))
                {
                    return null;
                }
                active &= g.Active;
                path.Add(Array.IndexOf(parent.Children, tr));
                go = parent.Go;
            }
            return null;
        }
    }
}
