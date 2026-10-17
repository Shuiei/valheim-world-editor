using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The TerrainModifiers of locations and networked prefabs (active and enabled ones, in the prefab's
    /// frame), as TerrainModifiers.Use takes them: read at the editor's start (GameData).
    /// </summary>
    private sealed partial class ModifierScan : IStartScan
    {
        /// <summary>
        /// The entries by prefab name: {"location": bool, "modifiers": [...]}.
        /// </summary>
        public Dict Entries { get; } = new();

        /// <summary>
        /// The "terrain-modifiers" JSON.
        /// </summary>
        public string Json() => CatalogueJson.Write(Entries, new CatalogueJson.Style(",", Indent: 0));

        /// <summary>
        /// From each TerrainModifier (told by its script, its fields the only ones read whole) up its parents
        /// to the prefab, rather than down every prefab's objects (some locations have tens of thousands).
        /// A prefab is kept when it is a location or networked; its modifiers in the order of a walk down
        /// it: by their place under the prefab (the child indices), then on their object. Inactive or
        /// disabled ones are left out.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var found = FoundIn(b, game);
            if (found.Count == 0)
            {
                return;
            }
            AddEntries(b, game, found);
        }

        /// <summary>
        /// The bundle's active, enabled TerrainModifiers, by the root of their prefab: each with its path of
        /// child indices under the root, its place among its object's components and its entry.
        /// </summary>
        private static Dictionary<long, List<(List<int> Path, int Index, Dict Entry)>> FoundIn(Bundle b, Game game)
        {
            var found = new Dictionary<long, List<(List<int> Path, int Index, Dict Entry)>>();
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId != MonoBehaviourType || game.ScriptClass(b, f, info) != "TerrainModifier" || b.Field(f, info.PathId) is not { } m
                    || Up(b, m["m_GameObject.m_PathID"].AsLong) is not var (root, chain, active, index))
                {
                    continue;
                }
                if (!active || Num(Get(m, "m_Enabled", 1L)) != 1)
                {
                    continue;
                }
                // The object's frame, from the prefab's down (chain: the objects' Transforms below it).
                var (p, r, s) = (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0));
                foreach (var tr in chain)
                {
                    (p, r, s) = ChildFrame(tr, p, r, s);
                }
                var g = b.Field(m["m_GameObject.m_PathID"].AsLong)!;
                int place = g["m_Component.Array"].Children.FindIndex(c => c["component.m_PathID"].AsLong == info.PathId);
                (found.TryGetValue(root, out var list) ? list : found[root] = []).Add((index, place, Entry(m, chain.Count == 0, p)));
            }
            return found;
        }

        /// <summary>
        /// Adds the entry of each prefab (a location or networked one) that has modifiers found: its modifiers
        /// sorted by their walk down the prefab; the first of a name is kept.
        /// </summary>
        private void AddEntries(Bundle b, Game game, Dictionary<long, List<(List<int> Path, int Index, Dict Entry)>> found)
        {
            foreach (var (f, info) in b.Objects)
            {
                if (!found.TryGetValue(info.PathId, out var mods) || b.Field(f, info.PathId) is not { } go)
                {
                    continue;
                }
                var kinds = b.ComponentIds(go).Where(c => c.Type == MonoBehaviourType).Select(c => game.ScriptClass(b, c.Pid)).ToHashSet();
                if (!kinds.Contains("Location") && !kinds.Contains("ZNetView"))
                {
                    continue;
                }
                mods.Sort((x, y) => Order(x.Path, y.Path) is var c and not 0 ? c : x.Index.CompareTo(y.Index));
                // The first of a name, as every scan takes it.
                Entries.TryAdd(go["m_Name"].AsString, new Dict { ["location"] = kinds.Contains("Location"), ["modifiers"] = mods.Select(x => (object?)x.Entry).ToList() });
            }
        }
    }
}
