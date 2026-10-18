namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class VegetationScan
    {
        /// <summary>
        /// All rules in the game's order, with "prefab" and "draws" set; rules whose prefab has no name are left out.
        /// </summary>
        public List<object?> Finish(Game game)
        {
            var all = (_zone ?? []).Concat(_lists.OrderBy(kv => kv.Key.Order).ThenBy(kv => kv.Key.First, StringComparer.Ordinal).ThenBy(kv => kv.Key.Count).SelectMany(kv => kv.Value)).ToList();
            // The prefabs' names and the scripts on their active objects: (script, LODGroup on the same
            // object, respawns).
            var names = new Dictionary<Ref, string>();
            var scripts = new Dictionary<Ref, List<(Ref Script, bool Lod, bool Respawns)>>();
            game.Visit(all.Select(r => r.Prefab), (b, f, pid) =>
            {
                if (b.Field(f, pid) is { } go && Has(go, "m_Name"))
                {
                    names[new Ref(f.Cab, pid)] = go["m_Name"].AsString;
                    scripts[new Ref(f.Cab, pid)] = ScriptsOf(b, f, pid);
                }
            });
            var classes = ClassesOf(game, scripts);
            var rules = new List<object?>();
            foreach (var (rule, prefab) in all)
            {
                string? name = names.GetValueOrDefault(prefab);
                rule["prefab"] = name;
                rule["draws"] = DrawsOf(scripts.GetValueOrDefault(prefab) ?? [], classes);
                if (!string.IsNullOrEmpty(name))
                {
                    rules.Add(rule);
                }
            }
            return rules;
        }

        /// <summary>
        /// The class names of these scripts (MonoScripts are often in other bundles).
        /// </summary>
        private static Dictionary<Ref, string> ClassesOf(Game game, Dictionary<Ref, List<(Ref Script, bool Lod, bool Respawns)>> scripts)
        {
            var classes = new Dictionary<Ref, string>();
            game.Visit(scripts.Values.SelectMany(l => l.Select(s => s.Script)), (b, f, pid) =>
            {
                if (b.Field(f, pid) is { } ms && Has(ms, "m_ClassName"))
                {
                    classes[new Ref(f.Cab, pid)] = ms["m_ClassName"].AsString;
                }
            });
            return classes;
        }

        /// <summary>
        /// The random draws a prefab's scripts make in Awake (see <c>Draws</c>): a LodFadeInOut counts with a LODGroup,
        /// a Pickable only when it respawns.
        /// </summary>
        private static long DrawsOf(List<(Ref Script, bool Lod, bool Respawns)> list, Dictionary<Ref, string> classes) =>
            list.Sum(s => classes.GetValueOrDefault(s.Script) switch
            {
                "LodFadeInOut" => s.Lod ? 1 : 0,
                "Pickable" => s.Respawns ? 1 : 0,
                string c => Draws.GetValueOrDefault(c),
                null => 0,
            });
    }
}
