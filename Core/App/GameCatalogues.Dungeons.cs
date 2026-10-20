using Dict = TerrainEditor.App.CatalogueJson.Dict;

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
    ///   theme, size, endCap, entrance, divider, endCapPrio, weight, perimeter, enabled; minPlaceOrder when not 0
    ///     (how many rooms from the entrance it may join at the fewest);
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
    private sealed partial class DungeonScan : IStartScan
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
    }
}
