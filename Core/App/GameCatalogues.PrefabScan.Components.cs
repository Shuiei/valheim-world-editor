using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// What a networked root's components say, gathered in one pass over them (see <c>ReadComponents</c>).
        /// </summary>
        private sealed class RootComponents
        {
            /// <summary>
            /// Whether one of the components is a Character (the prefab is a creature).
            /// </summary>
            public bool Creature { get; set; }
            /// <summary>
            /// Whether one of the components marks an item.
            /// </summary>
            public bool Item { get; set; }
            /// <summary>
            /// The ZNetView's fields, when there is one.
            /// </summary>
            public AssetTypeValueField? Net { get; set; }
            /// <summary>
            /// The Plant's fields, when there is one.
            /// </summary>
            public AssetTypeValueField? Plant { get; set; }
            /// <summary>
            /// The numbers the components add to the entry, by key.
            /// </summary>
            public Dict Extra { get; } = new();
            /// <summary>
            /// The spawn key and the creatures a spawn point or nest names, when there is one.
            /// </summary>
            public (string Key, List<Ref> Creatures)? Spawns { get; set; }
        }

        /// <summary>
        /// Reads a networked root's components: the flags of each, and what each one's fields say (see <c>ReadSettings</c>
        /// and <c>ReadSpawn</c>).
        /// </summary>
        private RootComponents ReadComponents(Bundle b, Bundle.Inner f, (int File, long Pid)[] components)
        {
            var parts = new RootComponents();
            foreach (var (_, pid) in components)
            {
                var r = b.TypeOf(pid) == MonoBehaviourType ? _bundleRoles.GetValueOrDefault(pid) : Role.None;
                parts.Creature |= (r & Role.Creature) != 0;
                parts.Item |= (r & Role.Item) != 0;
                if ((r & OnRoot) == 0 || b.Field(pid) is not { } c)
                {
                    continue;
                }
                if ((r & Role.Net) != 0)
                {
                    parts.Net = c;
                }
                if ((r & Role.Plant) != 0)
                {
                    parts.Plant = c;
                }
                ReadSettings(r, c, parts.Extra);
                ReadSpawn(f, r, c, parts);
            }
            return parts;
        }

        /// <summary>
        /// The container's grid size ("cw" x "ch"), the ward's radius ("ward") and the crafting station's build range
        /// ("build") of a component, as its role says.
        /// </summary>
        private static void ReadSettings(Role r, AssetTypeValueField c, Dict extra)
        {
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
        }

        /// <summary>
        /// A spawn point's or nest's numbers and the creatures it names, as its role says (see <c>ReadPrefab</c>).
        /// </summary>
        private static void ReadSpawn(Bundle.Inner f, Role r, AssetTypeValueField c, RootComponents parts)
        {
            if ((r & Role.Spawner) != 0)
            {
                parts.Extra["rt"] = R(Num(Py(c["m_respawnTimeMinuts"])), 2);
                parts.Extra["lv"] = new List<object?> { Int(Get(c, "m_minLevel", 1L)), Int(Get(c, "m_maxLevel", 1L)) };
                parts.Extra["day"] = Int(Get(c, "m_spawnAtDay", 1L));
                parts.Extra["night"] = Int(Get(c, "m_spawnAtNight", 1L));
                parts.Spawns = ("cs", RefsOf(f, [c["m_creaturePrefab"]]));
            }
            else if ((r & Role.Trigger) != 0)
            {
                parts.Extra["trig"] = 1L;
                parts.Extra["lv"] = new List<object?> { Int(Get(c, "m_minLevel", 1L)), Int(Get(c, "m_maxLevel", 1L)) };
                parts.Spawns = ("cs", RefsOf(f, c["m_creaturePrefabs"]["Array"].Children));
            }
            else if ((r & Role.Nest) != 0)
            {
                parts.Extra["sr"] = R(Num(Get(c, "m_spawnRadius", 0L)), 2);
                parts.Extra["max"] = Int(Get(c, "m_maxNear", 0L));
                parts.Spawns = ("sa", RefsOf(f, c["m_prefabs"]["Array"].Children.Select(d => d["m_prefab"])));
            }
        }
    }
}
