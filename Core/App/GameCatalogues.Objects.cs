using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TerrainEditor.Save;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

// What the editor reads of the game's other objects at its start (GameData): the prefabs, the
// vegetation rules, the terrain modifiers, the names of object data and the cave rocks.
public static partial class GameCatalogues
{
    /// <summary>
    /// Unity's class id of a LODGroup.
    /// </summary>
    private const int LodGroupType = (int)AssetClassID.LODGroup;

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
    private sealed class PrefabScan : IStartScan
    {
        /// <summary>
        /// What a script is, by its fields.
        /// </summary>
        [Flags]
        private enum Role
        {
            /// <summary>
            /// None that matters.
            /// </summary>
            None = 0,
            /// <summary>
            /// A ZNetView (m_persistent, m_distant, m_type).
            /// </summary>
            Net = 1,
            /// <summary>
            /// A Character (m_runSpeed).
            /// </summary>
            Creature = 2,
            /// <summary>
            /// Has item data (m_itemData): marks the prefab as an item.
            /// </summary>
            Item = 4,
            /// <summary>
            /// A Plant (m_grownPrefabs, m_growRadius).
            /// </summary>
            Plant = 8,
            /// <summary>
            /// A Container (m_width, m_height, m_privacy).
            /// </summary>
            Container = 16,
            /// <summary>
            /// A PrivateArea ward (m_radius, m_areaMarker, m_enabledEffect).
            /// </summary>
            Ward = 32,
            /// <summary>
            /// A CraftingStation (m_rangeBuild).
            /// </summary>
            Station = 64,
            /// <summary>
            /// An ItemDrop (m_itemData with m_autoPickup): read for what stands hold.
            /// </summary>
            Drop = 128,
            /// <summary>
            /// An ItemStand (m_supportedTypes, m_attachOther).
            /// </summary>
            Stand = 256,
            /// <summary>
            /// An ArmorStand (m_slots, m_poseCount).
            /// </summary>
            Armour = 512,
            /// <summary>
            /// A CreatureSpawner (m_creaturePrefab, m_respawnTimeMinuts): an invisible spawn point.
            /// </summary>
            Spawner = 1024,
            /// <summary>
            /// A TriggerSpawner (m_creaturePrefabs, m_spawnChance): an invisible spawn point an event wakes.
            /// </summary>
            Trigger = 2048,
            /// <summary>
            /// A SpawnArea (m_prefabs, m_spawnIntervalSec): a nest or pile that spawns creatures around it.
            /// </summary>
            Nest = 4096,
        }

        /// <summary>
        /// Read whole on a prefab's root.
        /// </summary>
        private const Role OnRoot = Role.Net | Role.Plant | Role.Container | Role.Ward | Role.Station | Role.Spawner | Role.Trigger | Role.Nest;

        /// <summary>
        /// Read whole wherever they are.
        /// </summary>
        private const Role Anywhere = Role.Plant | Role.Drop | Role.Stand | Role.Armour;

        /// <summary>
        /// A script's roles, from its class's field names.
        /// </summary>
        private static Role RoleOf(IReadOnlySet<string> fields)
        {
            bool All(params string[] names) => names.All(fields.Contains);
            var r = Role.None;
            r |= All("m_persistent", "m_distant", "m_type") ? Role.Net : 0;
            r |= All("m_runSpeed") ? Role.Creature : 0;
            r |= All("m_itemData") ? Role.Item : 0;
            r |= All("m_grownPrefabs", "m_growRadius") ? Role.Plant : 0;
            r |= All("m_width", "m_height", "m_privacy") ? Role.Container : 0;
            r |= All("m_radius", "m_areaMarker", "m_enabledEffect") ? Role.Ward : 0;
            r |= All("m_rangeBuild") ? Role.Station : 0;
            r |= All("m_itemData", "m_autoPickup") ? Role.Drop : 0;
            r |= All("m_supportedTypes", "m_attachOther") ? Role.Stand : 0;
            r |= All("m_slots", "m_poseCount") ? Role.Armour : 0;
            r |= All("m_creaturePrefab", "m_respawnTimeMinuts") ? Role.Spawner : 0;
            r |= All("m_creaturePrefabs", "m_spawnChance") ? Role.Trigger : 0;
            r |= All("m_prefabs", "m_spawnIntervalSec") ? Role.Nest : 0;
            return r;
        }

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
        /// Reads the bundle's prefabs (networked roots) and what its saplings, items and stands say; see the class
        /// summary.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            // Each script's role (once per script, from its class's fields).
            var byScript = new Dictionary<(Bundle.Inner, ushort), Role>();
            Role RoleIn(Bundle.Inner f, AssetFileInfo info)
            {
                ushort index = info.GetScriptIndex(f.Assets.file);
                if (!byScript.TryGetValue((f, index), out var r))
                {
                    var script = index < f.Assets.file.Metadata.ScriptTypes.Count ? f.Assets.file.Metadata.ScriptTypes[index] : null;
                    var at = script != null && f.FileOf(script.FileId) is string cab ? new Ref(cab, script.PathId) : (Ref?)null;
                    if (at == null || !_roles.TryGetValue(at.Value, out r))
                    {
                        r = RoleOf(b.FieldNames(f, info));
                        if (at != null)
                        {
                            _roles[at.Value] = r;
                        }
                    }
                    byScript[(f, index)] = r;
                }
                return r;
            }
            var nets = new List<(Bundle.Inner, long)>();
            var scripts = new List<(Bundle.Inner File, long Pid, Role Role)>();
            var role = new Dictionary<long, Role>();
            foreach (var (f, info) in b.Objects)
            {
                var r = info.TypeId == MonoBehaviourType ? RoleIn(f, info) : Role.None;
                if (r == Role.None)
                {
                    continue;
                }
                role[info.PathId] = r;
                if ((r & Role.Net) != 0)
                {
                    nets.Add((f, info.PathId));
                }
                if ((r & Anywhere) != 0)
                {
                    scripts.Add((f, info.PathId, r));
                }
            }
            if (role.Count == 0)
            {
                return;
            }
            // The tree around them read raw, a sweep of the bundle at a time (it is read in blocks, in the
            // order of its files), with the scripts read anywhere (what they say taken, they are not kept):
            // the ZNetViews' and the scripts' GameObjects, their Transforms, the items' children; then the
            // prefabs' scripts. A bundle the block cache cannot hold (the largest, which has most prefabs)
            // has its whole tree and every script a prefab's root may need read in one sweep instead: each
            // sweep would read all of it again.
            bool all = b.Files.Sum(f => f.Assets.file.Header.FileSize) > BlockCache.Bytes;
            var tree = new Layout.Tree();
            var owners = new List<(Bundle.Inner, long)>();
            var said = new Dictionary<(Bundle.Inner, long), Said>();
            void Take(Bundle.Inner f, long pid, int type, Layout.Bytes d)
            {
                if (type != MonoBehaviourType)
                {
                    tree.Add(f, pid, type, d);
                    return;
                }
                var r = role[pid];
                if (all && (r & OnRoot) != 0)
                {
                    b.Field(f, pid);
                }
                if ((r & Anywhere) != 0 && b.Read(f, pid) is { } s)
                {
                    said[(f, pid)] = Say(f, r, s);
                }
                if ((r & Role.Net) != 0 && d.I32(0) == 0)
                {
                    owners.Add((f, d.I64(4)));
                }
            }
            var whole = scripts.Select(s => (s.File, s.Pid));
            List<(Bundle.Inner File, long Go)> items;
            IEnumerable<(Bundle.Inner, long)> TransformsOf(IEnumerable<(Bundle.Inner File, long Go)> gos) =>
                gos.Where(tree.GameObjects.ContainsKey).SelectMany(g => tree.GameObjects[g].Components.Where(c => Bundle.TypeOf(g.File, c.Pid) == TransformType).Select(c => (g.File, c.Pid)));
            IEnumerable<(Bundle.Inner, long)> ChildrenOf(IEnumerable<(Bundle.Inner File, long Go)> gos) =>
                TransformsOf(gos).Where(tree.Transforms.ContainsKey).SelectMany(t => tree.Transforms[t].Children.Select(c => (t.Item1, c)));
            if (all)
            {
                Layout.Sweep(b, b.Objects.Where(o => o.Info.TypeId is GameObjectType or TransformType || (role.GetValueOrDefault(o.Info.PathId) & (OnRoot | Anywhere)) != 0).Select(o => (o.File, o.Info.PathId)), Take);
                items = Items();
            }
            else
            {
                Layout.Sweep(b, nets.Concat(whole), Take);
                var gos = owners.Concat(said.Select(s => (s.Key.Item1, s.Value.Go))).ToList();
                Layout.Sweep(b, gos, Take);
                Layout.Sweep(b, TransformsOf(gos), Take);
                items = Items();
                var children = ChildrenOf(items).ToList();
                Layout.Sweep(b, children, Take);
                Layout.Sweep(b, children.Where(tree.Transforms.ContainsKey).Select(c => (c.Item1, tree.Transforms[c].Go)), Take);
            }
            // The items' GameObjects.
            List<(Bundle.Inner, long)> Items() => [.. scripts.Where(s => said.TryGetValue((s.File, s.Pid), out var x) && x.Item != null).Select(s => (s.File, said[(s.File, s.Pid)].Go)).Distinct()];
            // The prefabs: networked roots.
            var roots = new HashSet<long>();
            var rootGos = new List<(Bundle.Inner File, long Go)>();
            foreach (var ((f, go), (_, comps)) in tree.GameObjects)
            {
                if (comps.Any(c => (role.GetValueOrDefault(c.Pid) & Role.Net) != 0)
                    && comps.FirstOrDefault(c => c.File == 0 && Bundle.TypeOf(f, c.Pid) == TransformType) is (_, long tr and not 0) && tree.Transforms.TryGetValue((f, tr), out var node) && node.Root)
                {
                    roots.Add(go);
                    rootGos.Add((f, go));
                }
            }
            if (rootGos.Count > 0)
            {
                // One with children, when there is one: more of the tree checked.
                var check = rootGos.FirstOrDefault(g => TransformsOf([g]).Any(t => tree.Transforms.TryGetValue(t, out var n) && n.Children.Length > 0), rootGos[0]);
                Layout.Check(b, check.File, check.Go, tree);
            }
            // The prefabs' scripts read whole (in one more sweep, when not read yet).
            b.ReadInOrder(rootGos.SelectMany(g => tree.GameObjects[g].Components.Where(c => (role.GetValueOrDefault(c.Pid) & OnRoot) != 0).Select(c => c.Pid)));
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId == GameObjectType && roots.Contains(info.PathId))
                {
                    ReadPrefab(b, f, new Ref(f.Cab, info.PathId), tree.GameObjects[(f, info.PathId)], role);
                }
            }
            // Each item's children's names.
            var kids = items.ToDictionary(i => i, i => ChildrenOf([i]).Where(tree.Transforms.ContainsKey).Select(c => (c.Item1, tree.Transforms[c].Go))
                .Where(tree.GameObjects.ContainsKey).Select(g => tree.GameObjects[g].Name).ToHashSet());
            foreach (var (f, pid, _) in scripts)
            {
                if (said.TryGetValue((f, pid), out var s) && tree.GameObjects.TryGetValue((f, s.Go), out var go))
                {
                    Keep(f, pid, go.Name, s, kids.GetValueOrDefault((f, s.Go)) ?? []);
                }
            }
        }

        /// <summary>
        /// Adds the entry of a networked root (its name and components), when its ZNetView can be read; the first
        /// root of a name is kept. A spawn point (CreatureSpawner) also gets its respawn time in minutes ("rt", 0
        /// when it spawns once), its level range ("lv": min, max) and when it spawns ("day", "night": 0 when it
        /// does not); a spawn point an event wakes (TriggerSpawner) is marked ("trig"); a nest (SpawnArea) gets
        /// the radius it spawns in ("sr") and how many it keeps around ("max"). The creatures they name are filled
        /// in by <c>Json</c>.
        /// </summary>
        private void ReadPrefab(Bundle b, Bundle.Inner f, Ref at, (string Name, (int File, long Pid)[] Components) go, Dictionary<long, Role> roles)
        {
            (string Key, List<Ref> Creatures)? spawns = null;
            bool creature = false, item = false;
            AssetTypeValueField? znv = null, plant = null;
            var extra = new Dict();
            foreach (var (_, pid) in go.Components)
            {
                var r = b.TypeOf(pid) == MonoBehaviourType ? roles.GetValueOrDefault(pid) : Role.None;
                creature |= (r & Role.Creature) != 0;
                item |= (r & Role.Item) != 0;
                if ((r & OnRoot) == 0 || b.Field(pid) is not { } c)
                {
                    continue;
                }
                if ((r & Role.Net) != 0)
                {
                    znv = c;
                }
                if ((r & Role.Plant) != 0)
                {
                    plant = c;
                }
                if ((r & Role.Container) != 0)
                {
                    extra["cw"] = Int(Py(c["m_width"]));
                    extra["ch"] = Int(Py(c["m_height"]));
                }
                if ((r & Role.Ward) != 0)
                {
                    extra["ward"] = R(Num(Py(c["m_radius"])), 2);
                }
                if ((r & Role.Station) != 0)
                {
                    extra["build"] = R(Num(Py(c["m_rangeBuild"])), 2);
                }
                List<Ref> Refs(IEnumerable<AssetTypeValueField> pptrs) => [.. pptrs.Select(p => Bundle.RefOf(f, p)).OfType<Ref>()];
                if ((r & Role.Spawner) != 0)
                {
                    extra["rt"] = R(Num(Py(c["m_respawnTimeMinuts"])), 2);
                    extra["lv"] = new List<object?> { Int(Get(c, "m_minLevel", 1L)), Int(Get(c, "m_maxLevel", 1L)) };
                    extra["day"] = Int(Get(c, "m_spawnAtDay", 1L));
                    extra["night"] = Int(Get(c, "m_spawnAtNight", 1L));
                    spawns = ("cs", Refs([c["m_creaturePrefab"]]));
                }
                else if ((r & Role.Trigger) != 0)
                {
                    extra["trig"] = 1L;
                    extra["lv"] = new List<object?> { Int(Get(c, "m_minLevel", 1L)), Int(Get(c, "m_maxLevel", 1L)) };
                    spawns = ("cs", Refs(c["m_creaturePrefabs"]["Array"].Children));
                }
                else if ((r & Role.Nest) != 0)
                {
                    extra["sr"] = R(Num(Get(c, "m_spawnRadius", 0L)), 2);
                    extra["max"] = Int(Get(c, "m_maxNear", 0L));
                    spawns = ("sa", Refs(c["m_prefabs"]["Array"].Children.Select(d => d["m_prefab"])));
                }
            }
            if (znv == null)
            {
                return;
            }
            string name = go.Name;
            _names[at] = name;
            if (_prefabs.ContainsKey(name))
            {
                return;
            }
            var entry = new Dict { ["p"] = Int(Py(znv["m_persistent"])), ["d"] = Int(Py(znv["m_distant"])), ["t"] = Int(Py(znv["m_type"])) };
            if (creature)
            {
                entry["c"] = 1L;
            }
            if (item)
            {
                entry["i"] = 1L;
            }
            if (plant != null)
            {
                entry["gr"] = R(Num(Py(plant["m_growRadius"])), 3);
                entry["cult"] = Int(Get(plant, "m_needCultivatedGround", 0L));
            }
            foreach (var (k, v) in extra)
            {
                entry[k] = v;
            }
            if (spawns != null)
            {
                _spawns[name] = spawns.Value;
            }
            _prefabs[name] = entry;
        }

        /// <summary>
        /// What a script read anywhere says, taken as it is read (the script itself is not kept: items'
        /// are large): its GameObject, what a sapling grows into, an item's entry (without "at", "ats"),
        /// what an item stand takes (types, items taken and refused), an armour stand's slots.
        /// </summary>
        /// <param name="Go">The script's GameObject.</param>
        /// <param name="Grows">A sapling's grown prefabs.</param>
        /// <param name="Item">An item's entry.</param>
        /// <param name="Stand">An item stand's types, items taken and items refused.</param>
        /// <param name="Slots">An armour stand's slots, each the types it takes.</param>
        private sealed record Said(long Go, List<Ref>? Grows, Dict? Item, (List<long> Types, List<Ref> Take, List<Ref> Refuse)? Stand, List<object?>? Slots);

        /// <summary>
        /// What a script says, by its roles (see <c>Said</c>).
        /// </summary>
        private static Said Say(Bundle.Inner f, Role r, AssetTypeValueField d)
        {
            static List<long> Types(AssetTypeValueField list) => [.. list["Array"].Children.Select(t => Int(Py(t))).Distinct().Order()];
            List<Ref> Refs(string name) => Has(d, name) ? [.. d[name]["Array"].Children.Select(p => Bundle.RefOf(f, p)).OfType<Ref>()] : [];
            Dict? item = null;
            if ((r & Role.Drop) != 0)
            {
                var sh = d["m_itemData"]["m_shared"];
                item = new Dict { ["it"] = Int(Py(sh["m_itemType"])) };
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
            }
            return new Said(
                d["m_GameObject.m_PathID"].AsLong,
                (r & Role.Plant) != 0 ? Refs("m_grownPrefabs") : null,
                item,
                item == null && (r & Role.Stand) != 0 ? (Types(d["m_supportedTypes"]), Refs("m_supportedItems"), Refs("m_unsupportedItems")) : null,
                item == null && (r & Role.Stand) == 0 && (r & Role.Armour) != 0 ? [.. d["m_slots.Array"].Children.Select(s => (object?)Types(s["m_supportedTypes"]))] : null);
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
        /// The "prefabs" JSON (keys sorted), with what needs names from other bundles (the grown prefabs, the
        /// items stands name) filled in from what was read of them.
        /// </summary>
        public string Json()
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
            List<string> Owners(List<Ref> refs) => [.. refs.Where(_owners.ContainsKey).Select(r => _owners[r]).Distinct().Order(StringComparer.Ordinal)];
            foreach (var (n, (types, take, refuse)) in _stands)
            {
                if (!_prefabs.TryGetValue(n, out var entry))
                {
                    continue;
                }
                entry["is"] = types;
                if (take.Count > 0)
                {
                    entry["isi"] = Owners(take);
                }
                if (refuse.Count > 0)
                {
                    entry["isu"] = Owners(refuse);
                }
            }
            foreach (var (n, slots) in _armour)
            {
                if (_prefabs.TryGetValue(n, out var entry))
                {
                    entry["as"] = slots;
                }
            }
            var d = new Dict();
            foreach (var (n, e) in _prefabs)
            {
                d[n] = e;
            }
            return CatalogueJson.Write(d, new CatalogueJson.Style(",", ":", SortKeys: true));
        }
    }

    /// <summary>
    /// The game's vegetation rules, ZoneSystem's m_vegetation then the LocationLists' (by m_sortOrder), as
    /// the game merges them; "Regrow nature" places by these rules. Read at the editor's start (GameData):
    /// only the main scene's bundles, where ZoneSystem and the lists are (as for GameLocations), then the
    /// prefabs' and scripts' bundles. Alt biomes' extra vegetation (Deep North parts the editor's generator
    /// does not model) is left out. "draws":
    /// the random draws the prefab's components make in Awake when the game creates it (they shift the
    /// generator's sequence for the rest of the rule): LodFadeInOut (with a LODGroup: the camera is more
    /// than 20 m away while zones generate around a player), MineRock5, Floating, LightFlicker, Fish one
    /// each, RandomFlyingBird two, Pickable one when it respawns (m_respawnTimeMinutes &gt; 0). Checked
    /// against a played world: with these, the generated spots match the saved ones. Components of
    /// inactive children do not wake.
    /// </summary>
    public static string VegetationJson(GameBundles bundles)
    {
        using var game = new Game(bundles.Folder);
        // For the prefabs, in bundles not opened yet.
        game.IndexCabs();
        var scan = new VegetationScan();
        foreach (string name in GameLocations.MainSceneBundles(bundles).Order(StringComparer.Ordinal))
        {
            using var b = game.Open(name) ?? throw new InvalidDataException($"{name} cannot be read");
            scan.Read(b);
        }
        if (!scan.Found)
        {
            throw new InvalidDataException("the main scene's ZoneSystem was not found");
        }
        return CatalogueJson.Write(scan.Finish(game), new CatalogueJson.Style(",", Indent: 0));
    }

    /// <summary>
    /// Gathers the vegetation rules of the main scene's bundles, then names their prefabs and counts their draws.
    /// </summary>
    private sealed class VegetationScan
    {
        /// <summary>
        /// The VegetationSpawner fields kept, written without their "m_".
        /// </summary>
        private static readonly string[] Keep = ["m_name", "m_enable", "m_min", "m_max", "m_forcePlacement", "m_scaleMin", "m_scaleMax", "m_randTilt", "m_chanceToUseGroundTilt", "m_biome", "m_biomeArea", "m_blockCheck", "m_snapToStaticSolid", "m_minAltitude", "m_maxAltitude", "m_minVegetation", "m_maxVegetation", "m_surroundCheckVegetation", "m_surroundCheckDistance", "m_surroundCheckLayers", "m_surroundBetterThanAverage", "m_minOceanDepth", "m_maxOceanDepth", "m_minTilt", "m_maxTilt", "m_terrainDeltaRadius", "m_maxTerrainDelta", "m_minTerrainDelta", "m_snapToWater", "m_groundOffset", "m_groupSizeMin", "m_groupSizeMax", "m_groupRadius", "m_minDistanceFromCenter", "m_maxDistanceFromCenter", "m_inForest", "m_forestTresholdMin", "m_forestTresholdMax"];

        /// <summary>
        /// Random draws made in Awake by these scripts (LodFadeInOut only with a LODGroup, Pickable only when it
        /// respawns: in <c>Finish</c>).
        /// </summary>
        private static readonly Dictionary<string, int> Draws = new() { ["LodFadeInOut"] = 1, ["MineRock5"] = 1, ["Floating"] = 1, ["LightFlicker"] = 1, ["Fish"] = 1, ["RandomFlyingBird"] = 2 };

        /// <summary>
        /// ZoneSystem's own rules with their prefabs, null until found.
        /// </summary>
        private List<(Dict Rule, Ref Prefab)>? _zone;
        /// <summary>
        /// The LocationLists, each once (some bundles repeat them): by (sort order, first rule's name, rule count).
        /// </summary>
        private readonly Dictionary<(long Order, string First, int Count), List<(Dict Rule, Ref Prefab)>> _lists = [];

        /// <summary>
        /// Whether ZoneSystem was found.
        /// </summary>
        public bool Found => _zone != null;

        /// <summary>
        /// Takes ZoneSystem's rules and each LocationList's from one bundle.
        /// </summary>
        public void Read(Bundle b)
        {
            // The scripts with vegetation, told by their fields in the type tree (once per script): only
            // their objects are read.
            var byScript = new Dictionary<(Bundle.Inner, ushort), bool>();
            bool HasVegetation(Bundle.Inner f, AssetFileInfo info)
            {
                ushort index = info.GetScriptIndex(f.Assets.file);
                if (!byScript.TryGetValue((f, index), out bool has))
                {
                    byScript[(f, index)] = has = b.FieldNames(f, info).Contains("m_vegetation");
                }
                return has;
            }
            foreach (var f in b.Files)
            {
                foreach (var info in f.Assets.file.AssetInfos)
                {
                    if (info.TypeId != MonoBehaviourType || !HasVegetation(f, info) || b.Field(f, info.PathId) is not { } d)
                    {
                        continue;
                    }
                    var vegs = d["m_vegetation.Array"].Children;
                    if (Has(d, "m_zoneSize"))
                    {
                        _zone ??= Rules(f, vegs);
                    }
                    else if (Has(d, "m_sortOrder") && vegs.Count > 0)
                    {
                        _lists.TryAdd((Int(Py(d["m_sortOrder"])), vegs[0]["m_name"].AsString, vegs.Count), Rules(f, vegs));
                    }
                }
            }
        }

        /// <summary>
        /// The rules of a vegetation list (fields of <c>Keep</c>) with their prefabs.
        /// </summary>
        private static List<(Dict, Ref)> Rules(Bundle.Inner f, List<AssetTypeValueField> vegs) => [.. vegs.Select(v =>
        {
            var rule = new Dict();
            foreach (string k in Keep.Where(k => Has(v, k)))
            {
                rule[k[2..]] = Py(v[k]);
            }
            var p = v["m_prefab"];
            return (rule, new Ref(f.FileOf(p["m_FileID"].AsInt) ?? "", p["m_PathID"].AsLong));
        })];

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
            // Their class names (MonoScripts are often in other bundles).
            var classes = new Dictionary<Ref, string>();
            game.Visit(scripts.Values.SelectMany(l => l.Select(s => s.Script)), (b, f, pid) =>
            {
                if (b.Field(f, pid) is { } ms && Has(ms, "m_ClassName"))
                {
                    classes[new Ref(f.Cab, pid)] = ms["m_ClassName"].AsString;
                }
            });
            var rules = new List<object?>();
            foreach (var (rule, prefab) in all)
            {
                string? name = names.GetValueOrDefault(prefab);
                rule["prefab"] = name;
                rule["draws"] = (long)(scripts.GetValueOrDefault(prefab) ?? []).Sum(s => classes.GetValueOrDefault(s.Script) switch
                {
                    "LodFadeInOut" => s.Lod ? 1 : 0,
                    "Pickable" => s.Respawns ? 1 : 0,
                    string c => Draws.GetValueOrDefault(c),
                    null => 0,
                });
                if (!string.IsNullOrEmpty(name))
                {
                    rules.Add(rule);
                }
            }
            return rules;
        }

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

    /// <summary>
    /// The TerrainModifiers of locations and networked prefabs (active and enabled ones, in the prefab's
    /// frame), as TerrainModifiers.Use takes them: read at the editor's start (GameData).
    /// </summary>
    private sealed class ModifierScan : IStartScan
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
            if (found.Count == 0)
            {
                return;
            }
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

            static int Order(List<int> a, List<int> c)
            {
                for (int i = 0; i < Math.Min(a.Count, c.Count); i++)
                {
                    if (a[i] != c[i])
                    {
                        return a[i].CompareTo(c[i]);
                    }
                }
                return a.Count.CompareTo(c.Count);
            }
        }

        /// <summary>
        /// From an object up to its prefab root: the root, the Transforms from below the root down to the
        /// object, whether all of them are active (the root too), and the child index at each step. Null
        /// when something on the way cannot be read.
        /// </summary>
        private static (long Root, List<AssetTypeValueField> Chain, bool Active, List<int> Index)? Up(Bundle b, long goPid)
        {
            var chain = new List<AssetTypeValueField>();
            var index = new List<int>();
            bool active = true;
            for (int guard = 0; guard < 1000; guard++)
            {
                if (b.Field(goPid) is not { } go)
                {
                    return null;
                }
                active &= Active(go);
                AssetTypeValueField? tr = null;
                long trPid = 0;
                foreach (var (t, pid) in b.ComponentIds(go))
                {
                    if (t == TransformType && b.Field(pid) is { } f)
                    {
                        (tr, trPid) = (f, pid);
                    }
                }
                if (tr == null)
                {
                    return null;
                }
                long father = tr["m_Father.m_PathID"].AsLong;
                if (father == 0)
                {
                    chain.Reverse();
                    index.Reverse();
                    return (goPid, chain, active, index);
                }
                if (b.Field(father) is not { } parent)
                {
                    return null;
                }
                chain.Add(tr);
                index.Add(parent["m_Children.Array"].Children.FindIndex(c => c["m_PathID"].AsLong == trPid));
                goPid = parent["m_GameObject.m_PathID"].AsLong;
            }
            return null;
        }

        /// <summary>
        /// One modifier as the catalogue has it (pos: where it is in the prefab's frame).
        /// </summary>
        private static Dict Entry(AssetTypeValueField m, bool atRoot, V3 p) => new()
        {
            ["pos"] = atRoot ? Origin() : [R(p.X, 4), R(p.Y, 4), R(p.Z, 4)],
            ["level"] = Py(m["m_level"]),
            ["levelOffset"] = Py(m["m_levelOffset"]),
            ["levelRadius"] = Py(m["m_levelRadius"]),
            ["square"] = Py(m["m_square"]),
            ["smooth"] = Py(m["m_smooth"]),
            ["smoothRadius"] = Py(m["m_smoothRadius"]),
            ["smoothPower"] = Py(m["m_smoothPower"]),
            ["paintCleared"] = Py(m["m_paintCleared"]),
            ["paintType"] = Py(m["m_paintType"]),
            ["paintRadius"] = Py(m["m_paintRadius"]),
            ["paintStrength"] = Get(m, "m_paintStrength", 1.0),
            ["paintHeightCheck"] = Py(m["m_paintHeightCheck"]),
            ["sortOrder"] = Py(m["m_sortOrder"]),
            ["player"] = Py(m["m_playerModifiction"]),
            ["useTerrainCompiler"] = Get(m, "m_useTerrainCompiler", 0L),
        };

    }

    /// <summary>
    /// The cave roof rocks' shapes, each prefab's mesh in its own frame (the model's scale applied: its
    /// rock is a child scaled 4 times), as vertices and triangles, for the Path tool's Cave (CaveRoof),
    /// read at the editor's start (GameData): only the bundle that has them is opened (the index tells it).
    /// </summary>
    public static string CaveRocksJson(GameBundles bundles) => CatalogueJson.Write(CaveRocks(bundles), new CatalogueJson.Style(",", ":"));

    /// <summary>
    /// The two cave roof rocks' shapes, by prefab name.
    /// </summary>
    private static Dict CaveRocks(GameBundles bundles)
    {
        var res = new Dict();
        using var game = new Game(bundles.Folder);
        // Each bundle opened once (both rocks are in the same one).
        var open = new Dictionary<string, Bundle>(StringComparer.Ordinal);
        try
        {
            foreach (var (prefab, mesh, scale) in new[] { ("rock4_forest", "Rock4", 4.0), ("rock3_mountain", "Rock3", 4.0) })
            {
                if (!bundles.Index.TryGetValue(prefab, out var at))
                {
                    throw new InvalidDataException($"{prefab} is not in the game's files");
                }
                if (!open.TryGetValue(at.Bundle, out var b))
                {
                    open[at.Bundle] = b = game.Open(at.Bundle) ?? throw new InvalidDataException($"{at.Bundle} cannot be read");
                }
                res[prefab] = RockShape(b, prefab, mesh, scale);
            }
        }
        finally
        {
            foreach (var b in open.Values)
            {
                b.Dispose();
            }
        }
        return res;
    }

    /// <summary>
    /// One rock's mesh (named mesh in its prefab's bundle), scaled, as cave-rocks has it.
    /// </summary>
    private static Dict RockShape(Bundle b, string prefab, string mesh, double scale)
    {
        // Told by its name, read alone first (the bundle has many meshes).
        var m = b.Objects.Where(o => o.Info.TypeId == MeshType && NameOf(o.File, o.Info) == mesh).Select(o => b.Field(o.File, o.Info.PathId)).FirstOrDefault(m => m != null);
        var (vertices, triangles) = (m == null ? null : MeshData(b, m)) ?? throw new InvalidDataException($"{prefab}'s mesh {mesh} cannot be read");
        // As written to an OBJ file and read back (UnityPy's export: 9 significant digits, x mirrored and
        // back, triangles wound the other way).
        var vs = new List<object?>();
        for (int i = 0; i + 2 < vertices.Length; i += 3)
        {
            vs.Add(new List<object?> { R(-CatalogueJson.Significant9(-(double)vertices[i]) * scale, 3), R(CatalogueJson.Significant9(vertices[i + 1]) * scale, 3), R(CatalogueJson.Significant9(vertices[i + 2]) * scale, 3) });
        }
        var tris = new List<object?>();
        foreach (int[] sub in triangles)
        {
            for (int k = 0; k + 2 < sub.Length; k += 3)
            {
                tris.Add((long)sub[k + 2]);
                tris.Add((long)sub[k + 1]);
                tris.Add((long)sub[k]);
            }
        }
        return new Dict { ["v"] = vs, ["t"] = tris };
    }

    /// <summary>
    /// A named object's name (m_Name, its first field), nothing else read.
    /// </summary>
    private static string? NameOf(Bundle.Inner f, AssetFileInfo info)
    {
        var file = f.Assets.file;
        file.Reader.Position = info.GetAbsoluteByteOffset(file);
        int n = file.Reader.ReadInt32();
        return n is >= 0 and <= 256 ? System.Text.Encoding.UTF8.GetString(file.Reader.ReadBytes(n)) : null;
    }

    /// <summary>
    /// What a data name looks like: a letter or "_" first, then letters, digits, "_" or spaces, 40 characters at most.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex KeyName = new(@"\A[A-Za-z_][A-Za-z0-9_ ]{0,39}\z");

    /// <summary>
    /// The names of the data an object (ZDO) can hold, by hash (the save keeps only the hash of each
    /// name), for the object inspector (ZdoKeys), read at the editor's start (GameData). Every string literal
    /// of the game's code (the user strings of assembly_valheim.dll) is a candidate, plus the names the game
    /// builds at run time ("pu_id" + i for ward permissions, "{index}_item" for armour stands...).
    /// </summary>
    public static string ZdoKeysJson(string valheim) => CatalogueJson.Write(ZdoKeyNames(valheim), new CatalogueJson.Style(",", ":", SortKeys: true));

    /// <summary>
    /// The candidate names (see <c>ZdoKeysJson</c>) by the text of their stable hash; the shortest name wins a shared
    /// hash.
    /// </summary>
    private static Dict ZdoKeyNames(string valheim)
    {
        string dll = GameLook.DataDir(valheim) is string data && Path.Combine(data, "Managed", "assembly_valheim.dll") is var path && File.Exists(path)
            ? path : throw new FileNotFoundException("assembly_valheim.dll is not in the game's folder");
        var names = KeyNamesIn(dll);
        // Built at run time by the game.
        for (int i = 0; i < 64; i++)
        {
            names.UnionWith([$"pu_id{i}", $"pu_name{i}", $"{i}_seed", $"data_{i}", $"data__{i}"]);
        }
        for (int i = 0; i < 16; i++)
        {
            foreach (string n in new[] { "item", "variant", "quality", "itemData", "data", "sockets" })
            {
                names.Add($"{i}_{n}");
            }
        }
        var table = new Dict();
        foreach (var (hash, n) in ByHash(names))
        {
            table[hash.ToString(System.Globalization.CultureInfo.InvariantCulture)] = n;
        }
        return table;
    }

    /// <summary>
    /// The string literals of a .NET assembly that look like data names (<c>KeyName</c>): the user strings of its
    /// metadata, read as bytes (the assembly is not loaded). Empty for a file that is not a .NET assembly.
    /// </summary>
    internal static HashSet<string> KeyNamesIn(string dll)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(dll));
        if (!pe.HasMetadata)
        {
            return names;
        }
        var md = pe.GetMetadataReader();
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
        {
            string s = md.GetUserString(h);
            if (KeyName.IsMatch(s))
            {
                names.Add(s);
            }
        }
        return names;
    }

    /// <summary>
    /// Names by their stable hash, with the "_u" and "_i" each name gets for a ZDOID value (kept as two longs); the
    /// shortest name wins a shared hash.
    /// </summary>
    internal static Dictionary<int, string> ByHash(IEnumerable<string> names)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (string n in names)
        {
            all.UnionWith([n, n + "_u", n + "_i"]);
        }
        var table = new Dictionary<int, string>();
        foreach (string n in all.OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal))
        {
            table.TryAdd(StableHash.Of(n), n);
        }
        return table;
    }
}
