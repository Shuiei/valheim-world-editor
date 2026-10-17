using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// What the editor's Dungeon mode needs of the game's dungeons, read at the editor's start (GameData)
    /// in the one pass over the bundles. A dungeon (DG_Cave, DG_SunkenCrypt...) is a DungeonGenerator
    /// object whose saved "roomData" lists rooms by the stable hash of their prefab name, with position
    /// and rotation; the game rebuilds them from that list. For each dungeon kind: its algorithm, room
    /// themes, the box its rooms must stay in (m_zoneSize, around the zone centre at the dungeon's
    /// height), its door types ([prefab, opening type, chance (0: the dungeon's doorChance)]) and baseSeed
    /// (its seed is added to each room's roll). For each room prefab (a root with a Room component):
    ///   theme, size, endCap, entrance, divider, endCapPrio, weight, perimeter, enabled;
    ///   openings (RoomConnection children): [type, entrance, allowDoor, onlyIfOtherAllowsDoor, x, y, z, qx,
    ///     qy, qz, qw] in the room's frame (the game joins two rooms where openings meet, the new room
    ///     turned so its opening faces the other);
    ///   contents: the networked objects (ZNetView children) the game makes when it first generates the
    ///     room: [prefab, x, y, z, qx, qy, qz, qw, node], and what decides whether each is there, in the
    ///     order the game rolls them: spawns (RandomSpawn: [node, chance %, required theme, off node, min
    ///     elevation, max elevation]) and picks (RandomObject: [node, required theme, [[choice node,
    ///     weight]...], min elevation, max elevation]), with nodes: the parent of every node that matters
    ///     (-1 the room).
    /// Only what is active and enabled in the prefab counts (as the game's GetEnabledComponentsInChildren).
    /// Found from the Room and DungeonGenerator scripts (told by their class) up to their prefab roots. A
    /// room's tree is read raw, level by level; of its scripts only those that matter (told once per
    /// script by their fields in the type tree) where it is active, the ZNetViews and RandomSpawns raw.
    /// The first room of a name is kept, the last dungeon (as the bundles come, in name order).
    /// A location prefab (a root with a Location script: the start temple, an altar, ruins, a camp...) is read
    /// like a room, for the zones a new world generates itself (WorldCreator): contents, spawns, picks and
    /// nodes, with the Location's clearArea, randomDamage (m_applyRandomDamage), interior (m_hasInterior:
    /// a dungeon's entrance) and customInterior (m_useCustomInteriorTransform: the game moves its interior to
    /// the zone's middle), its teleports (the Teleport doors into and out of its interior: [x, y, z, qx, qy,
    /// qz, qw] in the prefab's frame) and interiorAt (where its interior transform stands in the prefab, or
    /// null). The first of a name is kept.
    /// </summary>
    private sealed class DungeonScan : IStartScan
    {
        /// <summary>
        /// The dungeons, the rooms and the locations found, by prefab name.
        /// </summary>
        private readonly Dictionary<string, Dict> _dungeons = [], _rooms = [], _locations = [];

        /// <summary>
        /// The "dungeon-rooms" JSON: {"dungeons": ..., "rooms": ..., "locations": ...}, each sorted by name, compact.
        /// </summary>
        public string Json()
        {
            static Dict Sorted(Dictionary<string, Dict> d)
            {
                var res = new Dict();
                foreach (var (k, v) in d.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    res[k] = v;
                }
                return res;
            }
            return CatalogueJson.Write(new Dict { ["dungeons"] = Sorted(_dungeons), ["rooms"] = Sorted(_rooms), ["locations"] = Sorted(_locations) }, new CatalogueJson.Style(",", ":"));
        }

        /// <summary>
        /// What a script is to the scan, by its fields. Null: none that matters.
        /// </summary>
        private static string? Kind(HashSet<string> f) =>
            f.Contains("m_theme") && f.Contains("m_endCap") && f.Contains("m_size") ? "room"
            : f.Contains("m_entrance") && f.Contains("m_allowDoor") ? "connection"
            : f.Contains("m_persistent") && f.Contains("m_distant") ? "netview"
            : f.Contains("m_chanceToSpawn") && f.Contains("m_OffObject") ? "spawn"
            : f.Contains("m_objects") && f.Contains("m_dungeonRequireTheme") && !f.Contains("m_chanceToSpawn") ? "pick"
            : f.Contains("m_algorithm") && f.Contains("m_themes") && f.Contains("m_maxRooms") ? "generator"
            : f.Contains("m_exteriorRadius") && f.Contains("m_clearArea") ? "location"
            : f.Contains("m_targetPoint") && f.Contains("m_enterText") ? "teleport"
            : null;

        /// <summary>
        /// The bundle being read: what each of its scripts is (once per script), and the rooms' trees read
        /// raw (Layout.Sweep): each GameObject's name, components and m_IsActive, each Transform's
        /// GameObject, children and local position and rotation, each ZNetView's m_Enabled. By path id
        /// over the bundle, as Bundle.Field(pid) finds them.
        /// </summary>
        private sealed class Open(Bundle b)
        {
            /// <summary>
            /// The bundle.
            /// </summary>
            public Bundle B { get; } = b;

            /// <summary>
            /// What each script is (<c>Kind</c>), by its file and script index.
            /// </summary>
            private readonly Dictionary<(int File, ushort Script), string?> _kinds = [];
            /// <summary>
            /// The GameObjects read raw: name, component path ids, m_IsActive.
            /// </summary>
            private readonly Dictionary<long, (string Name, long[] Components, bool Active)> _gos = [];
            /// <summary>
            /// The Transforms read raw: GameObject, children (Transform path ids), local position and rotation.
            /// </summary>
            private readonly Dictionary<long, (long Go, long[] Children, V3 Pos, Quat Rot)> _transforms = [];
            /// <summary>
            /// Each ZNetView's m_Enabled, read raw.
            /// </summary>
            private readonly Dictionary<long, bool> _enabled = [];
            /// <summary>
            /// <c>Node</c>'s answers, by GameObject.
            /// </summary>
            private readonly Dictionary<long, (string Name, bool Active, long Tr, List<(string Kind, long Pid)> Behaviours)> _nodes = [];
            /// <summary>
            /// <c>Transform</c>'s answers, by Transform.
            /// </summary>
            private readonly Dictionary<long, (V3 Pos, Quat Rot, List<long> Children)> _frames = [];
            /// <summary>
            /// Each GameObject's parent, of those read.
            /// </summary>
            private readonly Dictionary<long, long> _parents = [];

            /// <summary>
            /// A GameObject's parent GameObject, false when it was not read.
            /// </summary>
            public bool ParentOf(long go, out long parent) => _parents.TryGetValue(go, out parent);

            /// <summary>
            /// A MonoBehaviour's script: its file (by place) and its index there.
            /// </summary>
            private (int File, ushort Script) ScriptOf(Bundle.Inner f, AssetFileInfo info) => (B.Files.FindIndex(x => ReferenceEquals(x, f)), info.GetScriptIndex(f.Assets.file));

            /// <summary>
            /// What a MonoBehaviour is to the scan (by its script's fields, told once per script); null: not a
            /// MonoBehaviour or none that matters.
            /// </summary>
            private string? KindOf(long pid)
            {
                if (B.FileWith(pid) is not { } f || f.Assets.file.GetAssetInfo(pid) is not { TypeId: MonoBehaviourType } info)
                {
                    return null;
                }
                var key = ScriptOf(f, info);
                if (!_kinds.TryGetValue(key, out string? k))
                {
                    _kinds[key] = k = Kind(B.FieldNames(f, info));
                }
                return k;
            }

            /// <summary>
            /// A GameObject's Transform and the MonoBehaviours that matter (with what they are), from its
            /// components (path ids).
            /// </summary>
            private (long Tr, List<(string Kind, long Pid)> Behaviours) Ids(IEnumerable<long> components)
            {
                long tr = 0;
                var behaviours = new List<(string, long)>();
                foreach (long pid in components)
                {
                    int t = B.TypeOf(pid);
                    if (t == TransformType)
                    {
                        tr = pid;
                    }
                    else if (t == MonoBehaviourType && KindOf(pid) is string k)
                    {
                        behaviours.Add((k, pid));
                    }
                }
                return (tr, behaviours);
            }

            /// <summary>
            /// A prefab root's Transform and MonoBehaviours that matter (those that can be read), read whole.
            /// </summary>
            public (AssetTypeValueField? Tr, List<(string Kind, AssetTypeValueField F)> Behaviours) Parts(AssetTypeValueField go)
            {
                var (tr, ids) = Ids(go["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong));
                var behaviours = new List<(string, AssetTypeValueField)>();
                foreach (var (k, pid) in ids)
                {
                    if (B.Field(pid) is { } f)
                    {
                        behaviours.Add((k, f));
                    }
                }
                return (tr == 0 ? null : B.Field(tr), behaviours);
            }

            /// <summary>
            /// A GameObject of the trees read: its name, whether it is active, its Transform (0: none) and
            /// the MonoBehaviours that matter (path ids); null when it was not read.
            /// </summary>
            public (string Name, bool Active, long Tr, List<(string Kind, long Pid)> Behaviours)? Node(long go)
            {
                if (_nodes.TryGetValue(go, out var node))
                {
                    return node;
                }
                if (!_gos.TryGetValue(go, out var g))
                {
                    return null;
                }
                var (tr, ids) = Ids(g.Components);
                return _nodes[go] = (g.Name, g.Active, tr, ids);
            }

            /// <summary>
            /// A Transform of the trees: its local position and rotation, and its children's GameObjects
            /// (children not read left out); null when it was not read.
            /// </summary>
            public (V3 Pos, Quat Rot, List<long> Children)? Transform(long tr)
            {
                if (_frames.TryGetValue(tr, out var frame))
                {
                    return frame;
                }
                if (!_transforms.TryGetValue(tr, out var t))
                {
                    return null;
                }
                var children = new List<long>();
                foreach (long c in t.Children)
                {
                    if (_transforms.TryGetValue(c, out var ct))
                    {
                        children.Add(ct.Go);
                    }
                }
                return _frames[tr] = (t.Pos, t.Rot, children);
            }

            /// <summary>
            /// A RandomSpawn's fields read raw (the many in the rooms), or null when it was read whole.
            /// </summary>
            public Dictionary<string, object?>? Spawn(long pid) => _spawns.GetValueOrDefault(pid);

            /// <summary>
            /// RandomSpawns read raw, by path id.
            /// </summary>
            private readonly Dictionary<long, Dictionary<string, object?>> _spawns = [];
            /// <summary>
            /// <c>Plan</c>'s answers, by script.
            /// </summary>
            private readonly Dictionary<(int File, ushort Script), (AssetTypeTemplateField Tree, int Last)?> _plans = [];

            /// <summary>
            /// The RandomSpawn fields the scan needs.
            /// </summary>
            private static readonly string[] SpawnFields = ["m_Enabled", "m_chanceToSpawn", "m_dungeonRequireTheme", "m_OffObject", "m_minElevation", "m_maxElevation"];

            /// <summary>
            /// How a RandomSpawn's script is read raw: its type tree and its last field that matters; null
            /// when one of its fields before that is not of a kind read here.
            /// </summary>
            private (AssetTypeTemplateField Tree, int Last)? Plan(long pid)
            {
                var f = B.FileWith(pid)!;
                var info = f.Assets.file.GetAssetInfo(pid)!;
                var key = ScriptOf(f, info);
                if (!_plans.TryGetValue(key, out var plan))
                {
                    var tree = B.Template(f, info);
                    int last = tree == null ? -1 : tree.Children.FindLastIndex(c => SpawnFields.Contains(c.Name));
                    _plans[key] = plan = last >= 0 && tree!.Children.Take(last + 1).All(c => Raw.CanSkip(c) && (!SpawnFields.Contains(c.Name) || Raw.CanRead(c))) ? (tree, last) : null;
                }
                return plan;
            }

            /// <summary>
            /// Whether a ZNetView is enabled (read raw: nothing else of it is needed).
            /// </summary>
            public bool Enabled(long netview) => _enabled.TryGetValue(netview, out bool on) && on;

            /// <summary>
            /// Keeps what the scan needs of one object read raw. A GameObject has its components first (a count,
            /// then each a file id and a path id), then its layer, name (aligned to 4 bytes), tag (2 bytes) and
            /// m_IsActive. A Transform has its GameObject, rotation (x, y, z, w), position and scale, then its
            /// children. A MonoBehaviour has its GameObject, then m_Enabled.
            /// </summary>
            private void Take(Bundle.Inner f, long pid, int type, Layout.Bytes d)
            {
                if (type == GameObjectType)
                {
                    long[] comps = new long[d.I32(0)];
                    for (int i = 0; i < comps.Length; i++)
                    {
                        comps[i] = d.I64(8 + 12 * i);
                    }
                    int name = 8 + 12 * comps.Length;
                    _gos[pid] = (d.Text(name), comps, d.U8(name + 4 + ((d.I32(name) + 3) & ~3) + 2) != 0);
                }
                else if (type == TransformType)
                {
                    long[] children = new long[d.I32(52)];
                    for (int i = 0; i < children.Length; i++)
                    {
                        children[i] = d.I64(60 + 12 * i);
                    }
                    _transforms[pid] = (d.I64(4), children, (d.F32(28), d.F32(32), d.F32(36)), new Quat(d.F32(12), d.F32(16), d.F32(20), d.F32(24)));
                }
                else if (type == MonoBehaviourType)
                {
                    _enabled[pid] = d.U8(12) != 0;
                }
            }

            /// <summary>
            /// Each path id with the file in the bundle that has it (those not there left out).
            /// </summary>
            private IEnumerable<(Bundle.Inner, long)> At(IEnumerable<long> pids)
            {
                foreach (long pid in pids)
                {
                    if (B.FileWith(pid) is { } f)
                    {
                        yield return (f, pid);
                    }
                }
            }

            /// <summary>
            /// These GameObjects' trees read raw, in the order of their files, a level at a time (a level's
            /// GameObjects with the next level's Transforms); then the MonoBehaviours that matter where the
            /// rooms are active (on a root: its openings), the ZNetViews raw, the others whole.
            /// </summary>
            public void ReadTrees(List<long> roots)
            {
                Layout.Sweep(B, At(roots), Take);
                var transforms = roots.Select(g => Node(g)?.Tr ?? 0).Where(t => t != 0).ToList();
                Layout.Sweep(B, At(transforms), Take);
                while (transforms.Count > 0)
                {
                    var nodes = transforms.Where(_transforms.ContainsKey).Select(t => _transforms[t]).ToList();
                    transforms = [.. nodes.SelectMany(n => n.Children)];
                    Layout.Sweep(B, At(nodes.Select(n => n.Go).Concat(transforms)), Take);
                }
                foreach (var (tr, t) in _transforms)
                {
                    if (_gos.ContainsKey(t.Go))
                    {
                        foreach (long child in Transform(tr)!.Value.Children)
                        {
                            _parents[child] = t.Go;
                        }
                    }
                }
                var behaviours = new List<(string Kind, long Pid)>();
                void Active(long go, bool root)
                {
                    if (Node(go) is not { } g || !root && !g.Active)
                    {
                        return;
                    }
                    behaviours.AddRange(root ? g.Behaviours.Where(x => x.Kind == "connection") : g.Behaviours);
                    foreach (long child in Transform(g.Tr)?.Children ?? [])
                    {
                        Active(child, false);
                    }
                }
                foreach (long root in roots)
                {
                    Active(root, true);
                }
                Layout.Sweep(B, At(behaviours.Where(x => x.Kind == "netview").Select(x => x.Pid)), Take);
                var raw = behaviours.Where(x => x.Kind == "spawn" && Plan(x.Pid) != null).Select(x => x.Pid).ToList();
                Layout.Sweep(B, At(raw), (f, pid, _, d) =>
                {
                    var (tree, last) = Plan(pid)!.Value;
                    _spawns[pid] = Raw.Fields(tree, last, SpawnFields, d);
                });
                B.ReadInOrder(behaviours.Where(x => x.Kind != "netview" && !_spawns.ContainsKey(x.Pid)).Select(x => x.Pid));
                // The first of each script read whole too, to check.
                foreach (long pid in raw.DistinctBy(p => Plan(p)!.Value.Tree))
                {
                    if (B.Field(pid) is not { } whole || SpawnFields.Any(n => !Equals(_spawns[pid].GetValueOrDefault(n), n == "m_OffObject" ? whole["m_OffObject.m_PathID"].AsLong : Get(whole, n, null))))
                    {
                        throw Unexpected();
                    }
                }
                Check(roots[0]);
                Check(Transform(Node(roots[0])?.Tr ?? 0)?.Children.FirstOrDefault() ?? 0);
                if (behaviours.FirstOrDefault(x => x.Kind == "netview").Pid is long netview and not 0 && B.Field(netview) is { } nv && Truthy(Get(nv, "m_Enabled", 1L)) != Enabled(netview))
                {
                    throw Unexpected();
                }
            }

            /// <summary>
            /// The raw reading as the type tree says, for one GameObject and its Transform: said, rather
            /// than read wrong (as Layout.Check). Throws InvalidDataException when they differ.
            /// </summary>
            private void Check(long go)
            {
                if (!_gos.TryGetValue(go, out var raw) || B.Field(go) is not { } g)
                {
                    return;
                }
                bool same = g["m_Name"].AsString == raw.Name && Truthy(Get(g, "m_IsActive", true)) == raw.Active
                    && g["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong).SequenceEqual(raw.Components);
                if (Node(go) is { Tr: long tr and not 0 } && _transforms.TryGetValue(tr, out var rt) && B.Field(tr) is { } t)
                {
                    var rot = Quat.Of(t["m_LocalRotation"]);
                    same &= t["m_GameObject.m_PathID"].AsLong == rt.Go && t["m_Children.Array"].Children.Select(c => c["m_PathID"].AsLong).SequenceEqual(rt.Children)
                        && Vec(t["m_LocalPosition"]) == rt.Pos && rot == rt.Rot;
                }
                if (!same)
                {
                    throw Unexpected();
                }
            }

            /// <summary>
            /// The error for objects not laid out as the raw reading expects.
            /// </summary>
            private static InvalidDataException Unexpected() => new("The game's objects are not laid out as the editor expects (another Unity version?).");
        }

        /// <summary>
        /// Fields of an object read raw, as its type tree lays them out: each field after the other, a
        /// list or text as its count then its items, padded to 4 bytes after those marked so.
        /// </summary>
        private static class Raw
        {
            /// <summary>
            /// The byte size of a number type, 0 for others.
            /// </summary>
            private static int Size(AssetValueType t) => t switch
            {
                AssetValueType.Bool or AssetValueType.Int8 or AssetValueType.UInt8 => 1,
                AssetValueType.Int16 or AssetValueType.UInt16 => 2,
                AssetValueType.Int32 or AssetValueType.UInt32 or AssetValueType.Float => 4,
                AssetValueType.Int64 or AssetValueType.UInt64 or AssetValueType.Double => 8,
                _ => 0,
            };

            /// <summary>
            /// Whether a field can be stepped over raw: numbers, texts, byte arrays, lists of such, and structures of
            /// such.
            /// </summary>
            public static bool CanSkip(AssetTypeTemplateField t) =>
                t.IsArray ? t.Children.Count == 2 && CanSkip(t.Children[1])
                : t.ValueType is AssetValueType.String or AssetValueType.ByteArray || Size(t.ValueType) > 0 || t.ValueType == AssetValueType.None && t.Children.Count > 0 && t.Children.All(CanSkip);

            /// <summary>
            /// A number, a yes/no, a text, or a reference (its path id).
            /// </summary>
            public static bool CanRead(AssetTypeTemplateField t) =>
                t.ValueType is AssetValueType.Bool or AssetValueType.Int8 or AssetValueType.UInt8 or AssetValueType.Int32 or AssetValueType.Float or AssetValueType.String
                || t.ValueType == AssetValueType.None && t.Children.Count == 2 && t.Children[0].Name == "m_FileID" && t.Children[1].Name == "m_PathID";

            /// <summary>
            /// Moves <c>at</c> past one field, with its padding.
            /// </summary>
            private static void Skip(AssetTypeTemplateField t, Layout.Bytes d, ref int at)
            {
                if (t.IsArray)
                {
                    int n = d.I32(at);
                    at += 4;
                    for (int i = 0; i < n; i++)
                    {
                        Skip(t.Children[1], d, ref at);
                    }
                }
                else if (t.ValueType is AssetValueType.String or AssetValueType.ByteArray)
                {
                    at += 4 + d.I32(at);
                    if (t.Children.Count > 0 && t.Children[0].IsAligned)
                    {
                        at = (at + 3) & ~3;
                    }
                }
                else if (Size(t.ValueType) is int size and > 0)
                {
                    at += size;
                }
                else
                {
                    foreach (var c in t.Children)
                    {
                        Skip(c, d, ref at);
                    }
                }
                if (t.IsAligned)
                {
                    at = (at + 3) & ~3;
                }
            }

            /// <summary>
            /// The named fields up to the last-th, as Py has them (a reference: its path id).
            /// </summary>
            public static Dictionary<string, object?> Fields(AssetTypeTemplateField tree, int last, string[] names, Layout.Bytes d)
            {
                var res = new Dictionary<string, object?>();
                int at = 0;
                for (int i = 0; i <= last; i++)
                {
                    var c = tree.Children[i];
                    if (names.Contains(c.Name))
                    {
                        res[c.Name] = c.ValueType switch
                        {
                            AssetValueType.Bool => d.U8(at) != 0,
                            AssetValueType.Int8 => (long)(sbyte)d.U8(at),
                            AssetValueType.UInt8 => (long)d.U8(at),
                            AssetValueType.Int32 => (long)d.I32(at),
                            AssetValueType.Float => (double)d.F32(at),
                            AssetValueType.String => d.Text(at),
                            _ => d.I64(at + 4),
                        };
                    }
                    Skip(c, d, ref at);
                }
                return res;
            }
        }

        /// <summary>
        /// The characters a prefab name ends at.
        /// </summary>
        private static readonly System.Buffers.SearchValues<char> NameEnd = System.Buffers.SearchValues.Create("( ");

        /// <summary>
        /// Name up to the first '(' or space (the game's Utils.GetPrefabName).
        /// </summary>
        private static string PrefabName(string name)
        {
            int i = name.AsSpan().IndexOfAny(NameEnd);
            return i < 0 ? name : name[..i];
        }

        /// <summary>
        /// Finds the Room, DungeonGenerator and Location scripts of a bundle, up to their prefab roots, and reads each
        /// dungeon, each room and each location not met yet.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var owners = new List<(Bundle.Inner, long)>();
            var rooms = new HashSet<long>();
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId == MonoBehaviourType && game.ScriptClass(b, f, info) is string c and ("Room" or "DungeonGenerator" or "Location") && Layout.First(f, info) is (0, var go))
                {
                    owners.Add((f, go));
                    if (c != "DungeonGenerator")
                    {
                        rooms.Add(go);
                    }
                }
            }
            if (owners.Count == 0)
            {
                return;
            }
            var roots = Layout.Roots(owners);
            Layout.Check(b, owners[0].Item2, roots);
            var open = new Open(b);
            // The rooms' and locations' trees (a dungeon needs only its root).
            if (roots.Where(rooms.Contains).ToList() is { Count: > 0 } trees)
            {
                open.ReadTrees(trees);
            }
            foreach (var (_, info) in b.Objects)
            {
                if (info.TypeId != GameObjectType || !roots.Contains(info.PathId) || b.Field(info.PathId) is not { } go)
                {
                    continue;
                }
                var (tr, behaviours) = open.Parts(go);
                if (tr == null || tr["m_Father.m_PathID"].AsLong != 0)
                {
                    continue;
                }
                var kinds = new Dictionary<string, AssetTypeValueField>();
                foreach (var (k, f) in behaviours)
                {
                    kinds[k] = f;
                }
                string name = go["m_Name"].AsString;
                if (kinds.TryGetValue("generator", out var g))
                {
                    var (x, y, z) = Vec(g["m_zoneSize"]);
                    _dungeons[name] = new Dict
                    {
                        ["algorithm"] = Py(g["m_algorithm"]),
                        ["themes"] = Py(g["m_themes"]),
                        ["maxRooms"] = Py(g["m_maxRooms"]),
                        ["minRooms"] = Py(g["m_minRooms"]),
                        ["tileWidth"] = Round(Get(g, "m_tileWidth", 8L), 4),
                        ["zoneSize"] = new List<object?> { R(x, 4), R(y, 4), R(z, 4) },
                        ["customInterior"] = Int(Get(g, "m_useCustomInteriorTransform", 0L)),
                        ["baseSeed"] = Int(Get(g, "m_addBaseSeedToRandomSpawn", 0L)),
                        ["doorChance"] = Round(Get(g, "m_doorChance", 0.5), 4),
                        ["doors"] = Doors(b, g),
                    };
                }
                if (kinds.TryGetValue("room", out var room) && !_rooms.ContainsKey(name))
                {
                    _rooms[name] = new RoomReader(open, info.PathId).Read(room);
                }
                if (kinds.TryGetValue("location", out var location) && !_locations.ContainsKey(name))
                {
                    _locations[name] = new RoomReader(open, info.PathId).ReadLocation(location);
                }
            }
        }

        /// <summary>
        /// A dungeon's door types: [prefab, opening type, chance] for each one whose prefab is in the same file.
        /// </summary>
        private static List<object?> Doors(Bundle b, AssetTypeValueField g)
        {
            var res = new List<object?>();
            if (!Has(g, "m_doorTypes"))
            {
                return res;
            }
            foreach (var d in g["m_doorTypes.Array"].Children)
            {
                var pptr = d["m_prefab"];
                long pid = pptr["m_PathID"].AsLong;
                if (pptr["m_FileID"].AsInt == 0 && pid != 0 && b.Field(pid) is { } go && Has(go, "m_Name"))
                {
                    res.Add(new List<object?> { go["m_Name"].AsString, Py(d["m_connectionType"]), Round(Get(d, "m_chance", 0L), 4) });
                }
            }
            return res;
        }

        /// <summary>
        /// One room or location prefab's openings, contents and random parts.
        /// </summary>
        private sealed class RoomReader
        {
            /// <summary>
            /// The bundle's trees.
            /// </summary>
            private readonly Open _open;
            /// <summary>
            /// The room's root GameObject.
            /// </summary>
            private readonly long _root;
            /// <summary>
            /// Node of each GameObject that matters (numbered as first named).
            /// </summary>
            private readonly Dictionary<long, long> _node = [];
            /// <summary>
            /// The JSON lists: each node's parent node, the openings, contents, spawns and picks, and (a location's)
            /// teleport doors.
            /// </summary>
            private readonly List<object?> _nodes = [], _openings = [], _contents = [], _spawns = [], _picks = [], _teleports = [];
            /// <summary>
            /// A location's interior transform (Location.m_interiorTransform, by path id; 0: none), and where it is in the
            /// prefab once met.
            /// </summary>
            private long _interiorTr;
            private List<object?>? _interior;

            /// <summary>
            /// A reader for the room whose root is <c>root</c>.
            /// </summary>
            public RoomReader(Open open, long root)
            {
                _open = open;
                _root = root;
            }

            /// <summary>
            /// The room's JSON: its Room fields, then openings, contents, spawns, picks and nodes from a walk over its
            /// tree.
            /// </summary>
            public Dict Read(AssetTypeValueField room)
            {
                var info = new Dict
                {
                    ["theme"] = Py(room["m_theme"]),
                    ["size"] = new List<object?> { Py(room["m_size.x"]), Py(room["m_size.y"]), Py(room["m_size.z"]) },
                    ["endCap"] = Int(Py(room["m_endCap"])),
                    ["entrance"] = Int(Py(room["m_entrance"])),
                    ["divider"] = Int(Get(room, "m_divider", 0L)),
                    ["endCapPrio"] = Get(room, "m_endCapPrio", 0L),
                    ["weight"] = Round(Get(room, "m_weight", 1L), 4),
                    ["perimeter"] = Int(Get(room, "m_perimeter", 0L)),
                    ["enabled"] = Int(Get(room, "m_enabled", 1L)),
                    ["openings"] = _openings,
                    ["contents"] = _contents,
                    ["spawns"] = _spawns,
                    ["picks"] = _picks,
                    ["nodes"] = _nodes,
                };
                Walk(_root, default, Quat.Root, true);
                return info;
            }

            /// <summary>
            /// A location's JSON: what of its Location script the game spawns it by, then contents, spawns, picks and
            /// nodes from a walk over its tree, as a room's.
            /// </summary>
            public Dict ReadLocation(AssetTypeValueField location)
            {
                _interiorTr = Has(location, "m_interiorTransform") ? location["m_interiorTransform.m_PathID"].AsLong : 0;
                var info = new Dict
                {
                    ["clearArea"] = Int(Get(location, "m_clearArea", 0L)),
                    ["randomDamage"] = Int(Get(location, "m_applyRandomDamage", 0L)),
                    ["interior"] = Int(Get(location, "m_hasInterior", 0L)),
                    ["customInterior"] = Int(Get(location, "m_useCustomInteriorTransform", 0L)),
                    ["contents"] = _contents,
                    ["spawns"] = _spawns,
                    ["picks"] = _picks,
                    ["nodes"] = _nodes,
                    ["teleports"] = _teleports,
                };
                Walk(_root, default, Quat.Root, true);
                // Where the interior transform stands in the prefab (the game moves it to the zone's middle when the
                // location asks: SpawnLocation's custom interior transform), null when the location has none.
                info["interiorAt"] = _interior;
                return info;
            }

            /// <summary>
            /// A GameObject's node number (-1 the room's root); its parents are numbered first.
            /// </summary>
            private long Node(long pid)
            {
                if (pid == _root)
                {
                    return -1;
                }
                if (!_node.TryGetValue(pid, out long n))
                {
                    _open.ParentOf(pid, out long parent);
                    long p = Node(parent);
                    _node[pid] = n = _nodes.Count;
                    _nodes.Add(p);
                }
                return n;
            }

            /// <summary>
            /// Whether an object is in the room (below its root): those the random parts name.
            /// </summary>
            private bool Below(long pid)
            {
                while (_open.ParentOf(pid, out pid))
                {
                    if (pid == _root)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// A RandomSpawn, from its fields (m_OffObject: its path id).
            /// </summary>
            private void AddSpawn(long gid, Func<string, object?, object?> get)
            {
                long off = (long)get("m_OffObject", 0L)!;
                long node = Node(gid);
                _spawns.Add(new List<object?> { node, Round(get("m_chanceToSpawn", null), 4), get("m_dungeonRequireTheme", 0L), off != 0 && Below(off) ? Node(off) : -1L, get("m_minElevation", -10000L), get("m_maxElevation", 10000L) });
            }

            /// <summary>
            /// Depth first, in child order: the order the game finds the components in, so the order it
            /// rolls the random parts in. Inactive objects and their children are left out.
            /// </summary>
            private void Walk(long gid, V3 pos, Quat rot, bool root)
            {
                if (_open.Node(gid) is not { } g || _open.Transform(g.Tr) is not { } t)
                {
                    return;
                }
                if (!root)
                {
                    if (!g.Active)
                    {
                        return;
                    }
                    pos = Add(pos, rot.Rotate(t.Pos));
                    rot = rot.Mul(t.Rot);
                }
                List<object?> Place() => root ? [.. Origin(), .. rot.Rounded(4)] : [R(pos.X, 4), R(pos.Y, 4), R(pos.Z, 4), .. rot.Rounded(4)];
                if (!root && g.Tr == _interiorTr)
                {
                    _interior = Place();
                }
                foreach (var (k, pid) in g.Behaviours)
                {
                    if (root && k != "connection")
                    {
                        continue;
                    }
                    // A ZNetView: only whether it is enabled.
                    if (k == "netview")
                    {
                        if (_open.Enabled(pid))
                        {
                            _contents.Add(new List<object?> { PrefabName(g.Name) }.Concat(Place()).Append(Node(gid)).ToList());
                        }
                        continue;
                    }
                    if (k == "spawn" && _open.Spawn(pid) is { } raw)
                    {
                        if (Truthy(raw.GetValueOrDefault("m_Enabled", 1L)))
                        {
                            AddSpawn(gid, (n, fallback) => raw.TryGetValue(n, out object? v) ? v : fallback);
                        }
                        continue;
                    }
                    if (_open.B.Field(pid) is not { } b || !Truthy(Get(b, "m_Enabled", 1L)))
                    {
                        continue;
                    }
                    switch (k)
                    {
                        case "connection":
                            _openings.Add(new List<object?> { Py(b["m_type"]), Int(Py(b["m_entrance"])), Int(Py(b["m_allowDoor"])), Int(Get(b, "m_doorOnlyIfOtherAlsoAllowsDoor", 0L)) }.Concat(Place()).ToList());
                            break;
                        case "teleport":
                            // A location's door into its interior, or out of it (5000 m up in the prefab): where it stands
                            // and which way it faces, in the prefab's frame.
                            _teleports.Add(Place());
                            break;
                        case "spawn":
                            AddSpawn(gid, (n, fallback) => n == "m_OffObject" ? b["m_OffObject.m_PathID"].AsLong : Get(b, n, fallback));
                            break;
                        case "pick":
                            var choices = new List<object?>();
                            foreach (var e in b["m_objects.Array"].Children)
                            {
                                long cid = e["m_object.m_PathID"].AsLong;
                                if (cid != 0 && Below(cid))
                                {
                                    choices.Add(new List<object?> { Node(cid), Round(Py(e["m_weight"]), 4) });
                                }
                            }
                            _picks.Add(new List<object?> { Node(gid), Get(b, "m_dungeonRequireTheme", 0L), choices, Get(b, "m_minElevation", -10000L), Get(b, "m_maxElevation", 10000L) });
                            break;
                        default:
                            break;
                    }
                }
                foreach (long child in t.Children)
                {
                    Walk(child, pos, rot, false);
                }
            }
        }
    }
}
