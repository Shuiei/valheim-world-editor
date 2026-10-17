using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
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
        /// Each role and the fields that mark it: a script has the role when its class has all of them.
        /// </summary>
        private static readonly (Role Flag, string[] Fields)[] RoleFields =
        [
            (Role.Net, ["m_persistent", "m_distant", "m_type"]),
            (Role.Creature, ["m_runSpeed"]),
            (Role.Item, ["m_itemData"]),
            (Role.Plant, ["m_grownPrefabs", "m_growRadius"]),
            (Role.Container, ["m_width", "m_height", "m_privacy"]),
            (Role.Ward, ["m_radius", "m_areaMarker", "m_enabledEffect"]),
            (Role.Station, ["m_rangeBuild"]),
            (Role.Drop, ["m_itemData", "m_autoPickup"]),
            (Role.Stand, ["m_supportedTypes", "m_attachOther"]),
            (Role.Armour, ["m_slots", "m_poseCount"]),
            (Role.Spawner, ["m_creaturePrefab", "m_respawnTimeMinuts"]),
            (Role.Trigger, ["m_creaturePrefabs", "m_spawnChance"]),
            (Role.Nest, ["m_prefabs", "m_spawnIntervalSec"]),
        ];

        /// <summary>
        /// A script's roles, from its class's field names.
        /// </summary>
        private static Role RoleOf(IReadOnlySet<string> fields)
        {
            var r = Role.None;
            foreach (var (flag, names) in RoleFields)
            {
                if (names.All(fields.Contains))
                {
                    r |= flag;
                }
            }
            return r;
        }

        /// <summary>
        /// The role of a MonoBehaviour's script, read once per script (its class's fields, or the role the
        /// same class got in another bundle).
        /// </summary>
        private Role RoleIn(Bundle b, Bundle.Inner f, AssetFileInfo info)
        {
            ushort index = info.GetScriptIndex(f.Assets.file);
            if (_scriptRoles.TryGetValue((f, index), out var r))
            {
                return r;
            }
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
            _scriptRoles[(f, index)] = r;
            return r;
        }

        /// <summary>
        /// Finds the bundle's scripts with a role, by their roles: the networked ones and those read anywhere (kept
        /// in <c>_nets</c> and <c>_scripts</c>). False when it has none (nothing to read).
        /// </summary>
        private bool ClassifyScripts(Bundle b)
        {
            _scriptRoles.Clear();
            _bundleRoles.Clear();
            _nets.Clear();
            _scripts.Clear();
            foreach (var (f, info) in b.Objects)
            {
                var r = info.TypeId == MonoBehaviourType ? RoleIn(b, f, info) : Role.None;
                if (r == Role.None)
                {
                    continue;
                }
                _bundleRoles[info.PathId] = r;
                if ((r & Role.Net) != 0)
                {
                    _nets.Add((f, info.PathId));
                }
                if ((r & Anywhere) != 0)
                {
                    _scripts.Add((f, info.PathId, r));
                }
            }
            return _bundleRoles.Count > 0;
        }
    }
}
