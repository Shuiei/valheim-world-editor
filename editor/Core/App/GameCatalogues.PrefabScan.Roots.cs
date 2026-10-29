namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// The prefab roots of the tree: the GameObjects with a networked component whose Transform has no parent.
        /// Their path ids go to <c>_roots</c>; their GameObjects are returned.
        /// </summary>
        private List<(Bundle.Inner File, long Go)> FindRoots()
        {
            _roots.Clear();
            var rootGos = new List<(Bundle.Inner File, long Go)>();
            foreach (var ((f, go), (_, comps)) in _tree.GameObjects)
            {
                if (comps.Any(c => (_bundleRoles.GetValueOrDefault(c.Pid) & Role.Net) != 0)
                    && comps.FirstOrDefault(c => c.File == 0 && Bundle.TypeOf(f, c.Pid) == TransformType) is (_, long tr and not 0) && _tree.Transforms.TryGetValue((f, tr), out var node) && node.Root)
                {
                    _roots.Add(go);
                    rootGos.Add((f, go));
                }
            }
            return rootGos;
        }

        /// <summary>
        /// Checks the layout of one of the roots (one with children, when there is one) as the tree says.
        /// </summary>
        private void CheckRoots(Bundle b, List<(Bundle.Inner File, long Go)> rootGos)
        {
            if (rootGos.Count > 0)
            {
                // One with children, when there is one: more of the tree checked.
                var check = rootGos.FirstOrDefault(g => TransformsOf([g]).Any(t => _tree.Transforms.TryGetValue(t, out var n) && n.Children.Length > 0), rootGos[0]);
                Layout.Check(b, check.File, check.Go, _tree);
            }
        }

        /// <summary>
        /// Reads the prefabs' scripts whole (in one more sweep, when not read yet), then each prefab root's entry.
        /// </summary>
        private void ReadPrefabs(Bundle b, List<(Bundle.Inner File, long Go)> rootGos)
        {
            b.ReadInOrder(rootGos.SelectMany(g => _tree.GameObjects[g].Components.Where(c => (_bundleRoles.GetValueOrDefault(c.Pid) & OnRoot) != 0).Select(c => c.Pid)));
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId == GameObjectType && _roots.Contains(info.PathId))
                {
                    ReadPrefab(b, f, new Ref(f.Cab, info.PathId), _tree.GameObjects[(f, info.PathId)]);
                }
            }
        }
    }
}
