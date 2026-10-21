using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// One room or location prefab's openings, contents and random parts.
        /// </summary>
        private sealed partial class RoomReader
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
                AddPlaceOrder(info, room);
                Walk(_root, default, Quat.Root, true);
                return info;
            }

            /// <summary>The room's place order (m_minPlaceOrder), kept only when it is not 0.</summary>
            private static void AddPlaceOrder(Dict info, AssetTypeValueField room)
            {
                if (Int(Get(room, "m_minPlaceOrder", 0L)) is var order and not 0)
                {
                    info["minPlaceOrder"] = order;
                }
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
        }
    }
}
