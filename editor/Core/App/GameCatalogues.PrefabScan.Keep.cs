using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// An item's entry from its shared item data: its type ("it"), the largest stack ("ms", when it stacks), its
        /// attach override ("ao"), its variants ("var") and, for items that wear, its durability ("dur", "durl").
        /// </summary>
        private static Dict ItemOf(AssetTypeValueField sh)
        {
            var item = new Dict { ["it"] = Int(Py(sh["m_itemType"])) };
            // The largest stack (1 for items that do not stack: left out, as absent means 1).
            if (Int(Get(sh, "m_maxStackSize", 1L)) is > 1 and long maxStack)
            {
                item["ms"] = maxStack;
            }
            if (Int(Get(sh, "m_attachOverride", 0L)) != 0)
            {
                item["ao"] = Int(Py(sh["m_attachOverride"]));
            }
            if (Int(Get(sh, "m_variants", 0L)) > 0)
            {
                item["var"] = Int(Py(sh["m_variants"]));
            }
            if (Int(Get(sh, "m_useDurability", 0L)) != 0)
            {
                item["dur"] = R(Num(Py(sh["m_maxDurability"])), 2);
                if (Num(Get(sh, "m_durabilityPerLevel", 0L)) != 0)
                {
                    item["durl"] = R(Num(Py(sh["m_durabilityPerLevel"])), 2);
                }
            }
            return item;
        }

        /// <summary>
        /// A script's say kept under its GameObject's name (the first of each name for stands), with the
        /// names of an item's children: "attach" (item stands take no item without), "attach_skin" (armour
        /// stands take either, or a chest or legs piece without; ItemStand.GetAttachPrefab).
        /// </summary>
        private void Keep(Bundle.Inner f, long pid, string owner, Said said, HashSet<string> kids)
        {
            if (said.Grows != null)
            {
                _plants[owner] = said.Grows;
            }
            if (said.Item != null)
            {
                _owners[new Ref(f.Cab, pid)] = owner;
                if (_items.ContainsKey(owner))
                {
                    return;
                }
                if (kids.Contains("attach"))
                {
                    said.Item["at"] = 1L;
                }
                if (kids.Contains("attach_skin"))
                {
                    said.Item["ats"] = 1L;
                }
                _items[owner] = said.Item;
            }
            else if (said.Stand is { } stand)
            {
                _stands.TryAdd(owner, stand);
            }
            else if (said.Slots != null)
            {
                _armour.TryAdd(owner, said.Slots);
            }
        }

        /// <summary>
        /// Keeps what each script of the bundle says (see <c>Keep</c>), with the names of each item's children.
        /// </summary>
        private void KeepScripts(List<(Bundle.Inner File, long Go)> items)
        {
            var kids = items.ToDictionary(i => i, i => ChildrenOf([i]).Where(_tree.Transforms.ContainsKey).Select(c => (c.Item1, _tree.Transforms[c].Go))
                .Where(_tree.GameObjects.ContainsKey).Select(g => _tree.GameObjects[g].Name).ToHashSet());
            foreach (var (f, pid, _) in _scripts)
            {
                if (_said.TryGetValue((f, pid), out var s) && _tree.GameObjects.TryGetValue((f, s.Go), out var go))
                {
                    Keep(f, pid, go.Name, s, kids.GetValueOrDefault((f, s.Go)) ?? []);
                }
            }
        }
    }
}
