using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The prefabs, read at the editor's start (GameData): every prefab with a ZNetView on its root (a
    /// network object the save can hold), with what a new object of it needs in the save: persistent /
    /// distant flags and object type ("p", "d", "t"). Creatures ("c") and item drops ("i") are marked, so
    /// the editor can leave them out of what it offers to place. Saplings (Plant) also get their grow
    /// radius ("gr", the free space they need to grow), whether they need cultivated ground ("cult") and
    /// what they grow into ("grows": Plant's m_grownPrefabs). Containers get their grid size ("cw" x
    /// "ch"), wards their protected radius ("ward"), crafting stations the radius they allow building in
    /// ("build").
    /// What stands hold: items get their type ("it"), their attach override ("ao", when set), their
    /// number of variants ("var", when they have some), whether they have an "attach" child ("at": without
    /// one no item stand takes them) or an "attach_skin" one ("ats": armour stands take either, or a chest
    /// or legs piece without), and for items that wear, their durability when new ("dur") and what each
    /// quality level adds ("durl"). Item stands get the types they take ("is") and the items they take or
    /// refuse whatever their type ("isi", "isu"); armour stands the types each slot takes ("as", in slot
    /// order: the save keeps slot i's item under "&lt;i&gt;_item").
    /// Scripts are told by their fields in the type tree (nothing of them read); the prefabs are found
    /// from the ZNetViews up to their GameObjects through the bundle's tree read raw (Layout.Tree), kept to
    /// the roots; only the scripts needed are read whole. What saplings grow into and what item stands
    /// take are named in other bundles: found among what the reading met (no bundle opened again).
    /// </summary>
    private sealed partial class PrefabScan : IStartScan
    {
        /// <summary>
        /// The prefab entries, by name.
        /// </summary>
        private readonly Dictionary<string, Dict> _prefabs = [];
        /// <summary>
        /// Each script's role, by where it is.
        /// </summary>
        private readonly Dictionary<Ref, Role> _roles = [];
        /// <summary>
        /// Every networked root's name by where it is: what saplings name (without opening their bundles).
        /// </summary>
        private readonly Dictionary<Ref, string> _names = [];
        /// <summary>
        /// Saplings: the grown prefabs they name.
        /// </summary>
        private readonly Dictionary<string, List<Ref>> _plants = [];
        /// <summary>
        /// Items by their ItemDrop component (what item stands name).
        /// </summary>
        private readonly Dictionary<Ref, string> _owners = [];
        /// <summary>
        /// The items' entries, by prefab name, added to their prefabs in <c>Json</c>.
        /// </summary>
        private readonly Dictionary<string, Dict> _items = [];
        /// <summary>
        /// What item stands take, by prefab name: types, items taken, items refused.
        /// </summary>
        private readonly Dictionary<string, (List<long> Types, List<Ref> Take, List<Ref> Refuse)> _stands = [];
        /// <summary>
        /// What each armour stand slot takes, by prefab name.
        /// </summary>
        private readonly Dictionary<string, List<object?>> _armour = [];
        /// <summary>
        /// Spawn points and nests: the creatures they name, by prefab name, and the key they are written under
        /// ("cs" for spawn points, "sa" for nests).
        /// </summary>
        private readonly Dictionary<string, (string Key, List<Ref> Creatures)> _spawns = [];

        /// <summary>
        /// The role of each script of the bundle being read, by its file and script index (reset per bundle).
        /// </summary>
        private readonly Dictionary<(Bundle.Inner, ushort), Role> _scriptRoles = [];
        /// <summary>
        /// The role of each script object of the bundle being read, by path id (reset per bundle).
        /// </summary>
        private readonly Dictionary<long, Role> _bundleRoles = [];
        /// <summary>
        /// The networked scripts of the bundle being read (reset per bundle).
        /// </summary>
        private readonly List<(Bundle.Inner, long)> _nets = [];
        /// <summary>
        /// The scripts of the bundle being read that have a role read anywhere (reset per bundle).
        /// </summary>
        private readonly List<(Bundle.Inner File, long Pid, Role Role)> _scripts = [];
        /// <summary>
        /// The GameObjects of the networked scripts found in the sweeps of the bundle being read (reset per bundle).
        /// </summary>
        private readonly List<(Bundle.Inner, long)> _netGos = [];
        /// <summary>
        /// The path ids of the prefab roots of the bundle being read (reset per bundle).
        /// </summary>
        private readonly HashSet<long> _roots = [];
        /// <summary>
        /// What each script read anywhere says, by file and path id (reset per bundle).
        /// </summary>
        private readonly Dictionary<(Bundle.Inner, long), Said> _said = [];
        /// <summary>
        /// The tree of the bundle being read, read raw (reset per bundle).
        /// </summary>
        private Layout.Tree _tree = new();
        /// <summary>
        /// Whether the bundle being read is too large for the block cache, so its whole tree is read in one sweep.
        /// </summary>
        private bool _bigBundle;

        /// <summary>
        /// Reads the bundle's prefabs (networked roots) and what its saplings, items and stands say; see the class
        /// summary.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            if (!ClassifyScripts(b))
            {
                return;
            }
            // The tree around them read raw, a sweep of the bundle at a time (it is read in blocks, in the
            // order of its files), with the scripts read anywhere (what they say taken, they are not kept):
            // the ZNetViews' and the scripts' GameObjects, their Transforms, the items' children; then the
            // prefabs' scripts. A bundle the block cache cannot hold (the largest, which has most prefabs)
            // has its whole tree and every script a prefab's root may need read in one sweep instead: each
            // sweep would read all of it again.
            _bigBundle = b.Files.Sum(f => f.Assets.file.Header.FileSize) > BlockCache.Bytes;
            _tree = new Layout.Tree();
            _netGos.Clear();
            _said.Clear();
            var items = ReadTrees(b);
            var rootGos = FindRoots();
            CheckRoots(b, rootGos);
            ReadPrefabs(b, rootGos);
            KeepScripts(items);
        }

        /// <summary>
        /// The "prefabs" JSON (keys sorted), with what needs names from other bundles (the grown prefabs, the
        /// items stands name) filled in from what was read of them.
        /// </summary>
        public string Json()
        {
            FillPlantsAndSpawns();
            FillItems();
            FillStands();
            FillArmour();
            var d = new Dict();
            foreach (var (n, e) in _prefabs)
            {
                d[n] = e;
            }
            return CatalogueJson.Write(d, new CatalogueJson.Style(",", ":", SortKeys: true));
        }
    }
}
