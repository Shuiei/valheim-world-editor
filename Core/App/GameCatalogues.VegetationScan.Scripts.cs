namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class VegetationScan
    {
        /// <summary>
        /// The scripts on a prefab's active objects (the root counts even inactive): script, LODGroup on the same
        /// object, respawns.
        /// </summary>
        private static List<(Ref, bool, bool)> ScriptsOf(Bundle b, Bundle.Inner f, long root)
        {
            var res = new List<(Ref, bool, bool)>();
            var stack = new Stack<long>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                long pid = stack.Pop();
                if (b.Field(f, pid) is not { } go || !Active(go) && pid != root)
                {
                    continue;
                }
                var comps = go["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong).Where(c => Bundle.TypeOf(f, c) >= 0).ToList();
                bool lod = comps.Any(c => Bundle.TypeOf(f, c) == LodGroupType);
                foreach (long c in comps)
                {
                    int t = Bundle.TypeOf(f, c);
                    if (t == TransformType && b.Field(f, c) is { } tr)
                    {
                        foreach (var ch in tr["m_Children.Array"].Children)
                        {
                            if (b.Field(f, ch["m_PathID"].AsLong) is { } ct)
                            {
                                stack.Push(ct["m_GameObject.m_PathID"].AsLong);
                            }
                        }
                    }
                    else if (t == MonoBehaviourType && b.Field(f, c) is { } mb && Bundle.RefOf(f, mb["m_Script"]) is Ref script)
                    {
                        res.Add((script, lod, Num(Get(mb, "m_respawnTimeMinutes", 0L)) > 0));
                    }
                }
            }
            return res;
        }
    }
}
