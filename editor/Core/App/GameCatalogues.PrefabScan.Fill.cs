namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// Fills in the names the saplings grow into ("grows") and the creatures the spawn points and nests name
        /// (their key), from the names read in the other bundles; only the names read are kept.
        /// </summary>
        private void FillPlantsAndSpawns()
        {
            foreach (var (owner, refs) in _plants)
            {
                var grown = refs.Where(_names.ContainsKey).Select(r => _names[r]).Where(_prefabs.ContainsKey).Distinct().Order(StringComparer.Ordinal).ToList();
                if (_prefabs.TryGetValue(owner, out var entry) && grown.Count > 0)
                {
                    entry["grows"] = grown;
                }
            }
            foreach (var (owner, (key, refs)) in _spawns)
            {
                var creatures = refs.Where(_names.ContainsKey).Select(r => _names[r]).Distinct().ToList();
                if (_prefabs.TryGetValue(owner, out var entry) && creatures.Count > 0)
                {
                    entry[key] = creatures;
                }
            }
        }

        /// <summary>
        /// Adds each item's entry to its prefab.
        /// </summary>
        private void FillItems()
        {
            foreach (var (n, e) in _items)
            {
                if (_prefabs.TryGetValue(n, out var entry))
                {
                    foreach (var (k, v) in e)
                    {
                        entry[k] = v;
                    }
                }
            }
        }

        /// <summary>
        /// Adds each item stand's types and the item names it takes ("isi") and refuses ("isu") to its prefab.
        /// </summary>
        private void FillStands()
        {
            foreach (var (n, (types, take, refuse)) in _stands)
            {
                if (!_prefabs.TryGetValue(n, out var entry))
                {
                    continue;
                }
                entry["is"] = types;
                if (take.Count > 0)
                {
                    entry["isi"] = OwnersOf(take);
                }
                if (refuse.Count > 0)
                {
                    entry["isu"] = OwnersOf(refuse);
                }
            }
        }

        /// <summary>
        /// Adds each armour stand's slots to its prefab.
        /// </summary>
        private void FillArmour()
        {
            foreach (var (n, slots) in _armour)
            {
                if (_prefabs.TryGetValue(n, out var entry))
                {
                    entry["as"] = slots;
                }
            }
        }

        /// <summary>
        /// The distinct item names of these items' references (those that are known), in ordinal order.
        /// </summary>
        private List<string> OwnersOf(List<Ref> refs) => [.. refs.Where(_owners.ContainsKey).Select(r => _owners[r]).Distinct().Order(StringComparer.Ordinal)];
    }
}
