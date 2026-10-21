using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The random draws a new object of each networked prefab makes as it wakes (its scripts' Awake, and OnEnable for
    /// an enabled one), as WakeDraws takes them: read at the editor's start (GameData), from each script that draws up
    /// to its prefab (as <see cref="ModifierScan"/> does, the tree read raw a level at a time). A prefab's list is in the order its scripts wake: down the
    /// prefab (an object's scripts in order, then its children's), those on inactive objects below the root left out
    /// (the root wakes even inactive, as the game's vegetation shows). It holds: a
    /// number (that many draws), "wear" (a building piece's WearNTear: what it draws depends on its health), "lod"
    /// (a LodFadeInOut beside a LODGroup: a draw only when a camera is more than 20 m away), "melt" (a snow-melting
    /// fire: a draw only in the Deep North) and a container's default items ({"chance", "min", "max", "each",
    /// "weights"}: how many draws they take depends on the draws). Prefabs whose scripts draw nothing are left out, as
    /// are locations, rooms and dungeons (their objects are listed alone). Not counted (rare where the editor makes
    /// objects, or drawing by what happens around them): a pet's material variation, a cinder spawner's first cinder, a
    /// ship's sounds, a warrior's name, a terrain change made as it is placed.
    /// </summary>
    private sealed partial class WakeScan : IStartScan
    {
        /// <summary>
        /// The scripts that draw the same whatever their fields, with how many draws: LightFlicker, Smoke and the others
        /// one each; AnimalAI two (its own and BaseAI's), LuredWisp and RandomFlyingBird two, ObjectFlicker five.
        /// </summary>
        public static readonly Dictionary<string, int> Fixed = new(StringComparer.Ordinal)
        {
            ["LightFlicker"] = 1,
            ["Smoke"] = 1,
            ["SmokeSpawner"] = 1,
            ["ZSFX"] = 1,
            ["ConditionalObject"] = 1,
            ["Fire"] = 1,
            ["Fish"] = 1,
            ["Floating"] = 1,
            ["MineRock5"] = 1,
            ["Turret"] = 1,
            ["Procreation"] = 1,
            ["AnimalAI"] = 2,
            ["LuredWisp"] = 2,
            ["RandomFlyingBird"] = 2,
            ["ObjectFlicker"] = 5,
        };

        /// <summary>
        /// The scripts that draw once only when their own object has the network view (they read its data first).
        /// </summary>
        private static readonly HashSet<string> OnceWithView = new(StringComparer.Ordinal) { "ItemDrop", "ResourceRoot", "SapCollector", "CreatureSpawner" };

        /// <summary>
        /// The scripts whose draws depend on their fields (<see cref="ByField"/>): the only ones read whole.
        /// </summary>
        private static readonly HashSet<string> ByFields = new(StringComparer.Ordinal)
        {
            "Pickable", "Aoe", "Vine", "MonsterAI", "Tameable", "VisEquipment", "Fireplace", "Container", "Radiator", "PickableItem", "CharacterTimedDestruction",
        };

        /// <summary>The scripts whose draws depend on their object only (<see cref="TokenOf"/>): a building piece's, a LOD fade's.</summary>
        private static readonly HashSet<string> ByObject = new(StringComparer.Ordinal) { "WearNTear", "LodFadeInOut" };

        /// <summary>
        /// Each prefab's list, by name (the first of a name kept).
        /// </summary>
        private readonly Dict _entries = new();

        /// <summary>
        /// The "wake-draws" JSON (keys sorted).
        /// </summary>
        public string Json() => CatalogueJson.Write(_entries, new CatalogueJson.Style(",", ":", SortKeys: true));

        /// <summary>
        /// Adds the bundle's networked prefabs whose scripts draw (see the class summary).
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var scripts = new List<(Bundle.Inner File, long Pid, string Class, long Go)>();
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId == MonoBehaviourType && game.ScriptClass(b, f, info) is { } c && (Fixed.ContainsKey(c) || OnceWithView.Contains(c) || ByObject.Contains(c) || ByFields.Contains(c))
                    && Layout.First(f, info) is (0, var go))
                {
                    scripts.Add((f, info.PathId, c, go));
                }
            }
            if (scripts.Count == 0)
            {
                return;
            }
            ReadUp(b, [.. scripts.Select(s => (s.File, s.Go)).Distinct()], scripts.Where(s => ByFields.Contains(s.Class)).Select(s => (s.File, s.Pid)));
            var found = new Dictionary<(Bundle.Inner, long), List<(List<int> Path, int Index, object Token)>>();
            foreach (var (f, pid, c, go) in scripts)
            {
                if (Up(f, go) is not (var root, true, var path) || TokenOf(b, game, c, (f, pid), _gos[(f, go)].Components) is not { } token)
                {
                    continue;
                }
                int place = Array.FindIndex(_gos[(f, go)].Components, x => x.Pid == pid);
                (found.TryGetValue((f, root), out var list) ? list : found[(f, root)] = []).Add((path, place, token));
            }
            foreach (var (root, tokens) in found)
            {
                if (!IsPlace(b, game, _gos[root].Components))
                {
                    tokens.Sort((x, y) => Chain.Order(x.Path, y.Path) is var o and not 0 ? o : x.Index.CompareTo(y.Index));
                    _entries.TryAdd(_gos[root].Name, Merged(tokens.Select(t => t.Token)));
                }
            }
        }

        /// <summary>
        /// What one script draws (see the class summary); null when it draws nothing.
        /// </summary>
        private static object? TokenOf(Bundle b, Game game, string c, (Bundle.Inner File, long Pid) script, (int File, long Pid)[] go)
        {
            if (Fixed.TryGetValue(c, out int n))
            {
                return (long)n;
            }
            if (OnceWithView.Contains(c))
            {
                return HasView(b, game, go) ? 1L : null;
            }
            if (c == "WearNTear")
            {
                return "wear";
            }
            if (c == "LodFadeInOut")
            {
                return go.Any(x => x.File == 0 && Bundle.TypeOf(script.File, x.Pid) == LodGroupType) ? "lod" : null;
            }
            return b.Field(script.File, script.Pid) is { } m ? ByField(b, game, c, m, go) : null;
        }

        /// <summary>
        /// What a script draws by its fields: Pickable once when it respawns, Aoe once with a longest time to live,
        /// Vine two and a third without a set seed, MonsterAI four and a fifth with a wake-up delay, Tameable once with
        /// starting names, VisEquipment once with a random model, Radiator once when enabled, CharacterTimedDestruction once
        /// when it starts as it wakes, PickableItem two when it picks its item, a Fireplace "melt" when it melts snow, a
        /// Container its default items.
        /// </summary>
        private static object? ByField(Bundle b, Game game, string c, AssetTypeValueField m, (int File, long Pid)[] go) => c switch
        {
            "Pickable" => HasView(b, game, go) && Num(Get(m, "m_respawnTimeMinutes", 0L)) > 0 ? 1L : null,
            "Aoe" => Num(Get(m, "m_ttlMax", 0L)) > 0 ? 1L : null,
            "Vine" => Num(Get(m, "m_forceSeed", 0L)) == 0 ? 3L : 2L,
            "MonsterAI" => Num(Get(m, "m_wakeUpDelayMin", 0L)) > 0 || Num(Get(m, "m_wakeUpDelayMax", 0L)) > 0 ? 5L : 4L,
            "Tameable" => Has(m, "m_randomStartingName") && m["m_randomStartingName.Array"].Children.Count > 0 ? 1L : null,
            "VisEquipment" => Num(Get(m, "m_randomModelIndex", 0L)) != 0 ? 1L : null,
            "Radiator" => Num(Get(m, "m_Enabled", 1L)) == 1 ? 1L : null,
            "CharacterTimedDestruction" => Num(Get(m, "m_triggerOnAwake", 0L)) != 0 ? 1L : null,
            "PickableItem" => HasView(b, game, go) && RandomItem(m) ? 2L : null,
            "Fireplace" => HasView(b, game, go) && Has(m, "m_snowMelter") && m["m_snowMelter.m_PathID"].AsLong != 0 ? "melt" : null,
            _ => Loot(b, game, m, go),
        };

        /// <summary>
        /// A container's default items, as its draws need them; null when it has none or no network view (on its own
        /// object, or its root override's).
        /// </summary>
        private static Dict? Loot(Bundle b, Game game, AssetTypeValueField m, (int File, long Pid)[] go)
        {
            bool view = Has(m, "m_rootObjectOverride") && m["m_rootObjectOverride.m_PathID"].AsLong != 0 || HasView(b, game, go);
            if (!view || !Has(m, "m_defaultItems") || m["m_defaultItems"] is not { } table || !Has(table, "m_drops"))
            {
                return null;
            }
            var weights = table["m_drops.Array"].Children.Select(d => (object?)Num(Get(d, "m_weight", 1.0))).ToList();
            return weights.Count == 0 ? null : new Dict
            {
                ["chance"] = Num(Get(table, "m_dropChance", 1.0)),
                ["min"] = Int(Get(table, "m_dropMin", 1L)),
                ["max"] = Int(Get(table, "m_dropMax", 1L)),
                ["each"] = Truthy(Get(table, "m_oneOfEach", 0L)),
                ["weights"] = weights,
            };
        }

        /// <summary>Whether a prefab root is a location, a room or a dungeon: not an object (its objects are listed alone).</summary>
        private static bool IsPlace(Bundle b, Game game, (int File, long Pid)[] go) =>
            go.Any(x => x.File == 0 && game.ScriptClass(b, x.Pid) is "Location" or "Room" or "DungeonGenerator");

        /// <summary>Whether a PickableItem picks its item as it wakes: none set, several to pick from.</summary>
        private static bool RandomItem(AssetTypeValueField m) =>
            Has(m, "m_itemPrefab") && m["m_itemPrefab.m_PathID"].AsLong == 0 && Has(m, "m_randomItemPrefabs") && m["m_randomItemPrefabs.Array"].Children.Count > 0;

        /// <summary>Whether a GameObject has a network view (a ZNetView script).</summary>
        private static bool HasView(Bundle b, Game game, (int File, long Pid)[] go) =>
            go.Any(x => x.File == 0 && game.ScriptClass(b, x.Pid) == "ZNetView");

        /// <summary>The tokens with each run of numbers added into one.</summary>
        private static List<object?> Merged(IEnumerable<object> tokens)
        {
            var list = new List<object?>();
            foreach (object t in tokens)
            {
                if (t is long n && list.Count > 0 && list[^1] is long before)
                {
                    list[^1] = before + n;
                }
                else
                {
                    list.Add(t);
                }
            }
            return list;
        }
    }
}
