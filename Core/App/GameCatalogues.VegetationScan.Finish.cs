namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class VegetationScan
    {
        /// <summary>
        /// All rules in the game's order (ZoneSystem's, the lists', the alt biomes'), with "prefab" set; rules whose prefab
        /// has no name are left out.
        /// </summary>
        public List<object?> Finish(Game game)
        {
            var all = (_zone ?? []).Concat(_lists.OrderBy(kv => kv.Key.Order).ThenBy(kv => kv.Key.First, StringComparer.Ordinal).ThenBy(kv => kv.Key.Count).SelectMany(kv => kv.Value)).Concat(_alts ?? []).ToList();
            var names = new Dictionary<Ref, string>();
            game.Visit(all.Select(r => r.Prefab), (b, f, pid) =>
            {
                if (b.Field(f, pid) is { } go && Has(go, "m_Name"))
                {
                    names[new Ref(f.Cab, pid)] = go["m_Name"].AsString;
                }
            });
            var rules = new List<object?>();
            foreach (var (rule, prefab) in all)
            {
                string? name = names.GetValueOrDefault(prefab);
                rule["prefab"] = name;
                if (!string.IsNullOrEmpty(name))
                {
                    rules.Add(rule);
                }
            }
            return rules;
        }
    }
}
