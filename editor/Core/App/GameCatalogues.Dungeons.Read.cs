using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
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
    }
}
