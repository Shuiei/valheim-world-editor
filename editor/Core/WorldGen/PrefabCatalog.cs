using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Every game prefab with a ZNetView, read from the player's own Valheim (GameData; the tests use
/// editor/tests/fixtures/prefabs.json): what a new object of it needs in the save. This lets the editor create
/// any kind of object, also ones that do not exist anywhere in the world yet.
/// </summary>
public static partial class PrefabCatalog
{
    /// <summary>
    /// The name of a prefab: a building piece's, or any other kind's from the game's list; null when unknown.
    /// </summary>
    public static string? DisplayName(int prefab) => PieceCatalog.Get(prefab)?.Name ?? NameOf(prefab);

    /// <summary>
    /// What a new object of a prefab needs in the save.
    /// </summary>
    /// <param name="Name">The prefab name.</param>
    /// <param name="Flags">Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.</param>
    /// <param name="GrowRadius">Saplings and crops need this much free space to grow (0 for anything else).</param>
    /// <param name="NeedsCultivated">Whether it grows only on cultivated ground.</param>
    /// <param name="ContainerW">Columns of a container's grid (0 when the prefab is none, or its container is on a
    /// child object, like carts and ships).</param>
    /// <param name="ContainerH">Rows of a container's grid (0 likewise).</param>
    /// <param name="WardRadius">Area a ward protects.</param>
    /// <param name="BuildRange">Radius a crafting station lets players build in.</param>
    public sealed record Info(string Name, ushort Flags, float GrowRadius, bool NeedsCultivated, int ContainerW = 0, int ContainerH = 0, float WardRadius = 0, float BuildRange = 0)
    {
        /// <summary>Its ZNetView keeps its first scale (m_syncInitialScale): the game writes the scale of a new one.</summary>
        public bool SyncScale { get; init; }

        /// <summary>A building piece's full health (WearNTear.m_health); 0 for an object without a WearNTear.</summary>
        public float WearHealth { get; init; }
    }

    /// <summary>
    /// None until Use: no world or Workshop opens before then (GameData).
    /// </summary>
    private static volatile Data _catalogue = new([], [], [], [], [], [], [], [], [], [], []);

    /// <summary>
    /// What Use read, swapped in whole so readers never see half of it.
    /// </summary>
    /// <param name="ByHash">The kinds offered for placing, by prefab hash.</param>
    /// <param name="AllNames">Every prefab of the catalogue by hash, placeable or not (items, creatures...), for
    /// names.</param>
    /// <param name="ItemNames">Item prefab names, sorted.</param>
    /// <param name="Extra">The kinds not offered for placing.</param>
    /// <param name="Creatures">Not offered for placing, but generated dungeons and blueprints carry their
    /// bosses.</param>
    /// <param name="GrownFrom">What saplings grow into, by grown prefab name, with the sapling's grow radius (the
    /// largest when several saplings grow the same thing).</param>
    /// <param name="ItemKinds">See <see cref="ItemKindOf"/>.</param>
    /// <param name="ItemStands">See <see cref="ItemStandOf"/>.</param>
    /// <param name="ArmourStands">See <see cref="ArmourSlotsOf"/>.</param>
    /// <param name="Grow">The room saplings and what they grow into need (see GrowRoom).</param>
    /// <param name="Spawns">See <see cref="SpawnOf(int)"/>.</param>
    internal sealed record Data(Dictionary<int, Info> ByHash, Dictionary<int, string> AllNames, List<string> ItemNames, Dictionary<int, Info> Extra, HashSet<int> Creatures,
        Dictionary<string, (float Radius, string Sapling)> GrownFrom, Dictionary<int, ItemKind> ItemKinds, Dictionary<int, StandRule> ItemStands, Dictionary<int, int[][]> ArmourStands,
        Dictionary<string, (float Radius, bool Cultivated)> Grow, Dictionary<int, SpawnKind> Spawns)
    {
        /// <summary>
        /// See <see cref="Ranges"/>: made once per catalogue, at first use (Use fills ByHash after making the record);
        /// of two first uses at once, the first table kept is the one both get.
        /// </summary>
        public Dictionary<string, (float WardRadius, float BuildRange)> Ranges
        {
            get
            {
                if (field is { } made)
                {
                    return made;
                }
                made = ByHash.Values.Where(p => p.WardRadius > 0 || p.BuildRange > 0).ToDictionary(p => p.Name, p => (p.WardRadius, p.BuildRange));
                return Interlocked.CompareExchange(ref field, made, null) ?? made;
            }
        }
    }

    /// <summary>
    /// Whether a prefab (by hash) is a creature.
    /// </summary>
    public static bool IsCreature(int prefab) => _catalogue.Creatures.Contains(prefab);

    /// <summary>The creatures' prefab hashes (see <see cref="IsCreature"/>).</summary>
    public static IEnumerable<int> Creatures => _catalogue.Creatures;

    /// <summary>
    /// Kinds the editor offers to place: saved objects (persistent), not creatures, item drops or ragdolls.
    /// </summary>
    public static IEnumerable<Info> Placeable => _catalogue.ByHash.Values;

    /// <summary>
    /// The placeable kinds with a ward radius or a build range (wards and crafting stations), by prefab name.
    /// </summary>
    public static IReadOnlyDictionary<string, (float WardRadius, float BuildRange)> Ranges => _catalogue.Ranges;

    /// <summary>
    /// A placeable kind by prefab hash; null for anything not offered for placing (see <see cref="Details"/>).
    /// </summary>
    public static Info? Get(int prefab) => _catalogue.ByHash.TryGetValue(prefab, out var info) ? info : null;

    /// <summary>
    /// Runestones are not objects but locations: the save keeps a location proxy for each, and the game
    /// builds the stone from it whenever the zone loads. The editor lists a runestone's proxy under the
    /// location's name (its hash stands for the prefab), so it can be shown, picked and deleted, never
    /// moved or copied (that would need the proxy's own prefab).
    /// </summary>
    public static readonly string[] RunestoneLocations =
    [
        "Runestone_Ashlands", "Runestone_BlackForest", "Runestone_Boars", "Runestone_DeepNorth", "Runestone_Draugr", "Runestone_Greydwarfs",
        "Runestone_Meadows", "Runestone_Mistlands", "Runestone_Mountains", "Runestone_Plains", "Runestone_Swamps",
    ];

    /// <summary>
    /// The runestone locations by name hash.
    /// </summary>
    private static readonly Dictionary<int, string> RunestoneNames = RunestoneLocations.ToDictionary(StableHash.Of);

    /// <summary>
    /// Whether a prefab hash is one of the runestone locations (see <see cref="RunestoneLocations"/>).
    /// </summary>
    public static bool IsRunestone(int prefab) => RunestoneNames.ContainsKey(prefab);

    /// <summary>
    /// Whether a name is a runestone's ("Runestone_" prefix).
    /// </summary>
    public static bool IsRunestoneName(string name) => name.StartsWith("Runestone_", StringComparison.Ordinal);

    /// <summary>
    /// The game's name of any prefab of the catalogue (also items and creatures, and runestone locations), or null.
    /// </summary>
    public static string? NameOf(int prefab) => _catalogue.AllNames.TryGetValue(prefab, out string? n) ? n : RunestoneNames.GetValueOrDefault(prefab);

    /// <summary>
    /// Item prefabs (what containers and item stands can hold), by name.
    /// </summary>
    public static IReadOnlyList<string> Items => _catalogue.ItemNames;

    /// <summary>
    /// Container size, ward radius and build range, also for prefabs that are not offered for placing.
    /// </summary>
    public static Info? Details(int prefab) => _catalogue.ByHash.TryGetValue(prefab, out var info) ? info : _catalogue.Extra.TryGetValue(prefab, out var e) ? e : null;

    /// <summary>
    /// What saplings grow into, with the sapling's grow radius: Pickable_Carrot -&gt; 0.5 m (sapling_carrot),
    /// Beech1 -&gt; 2 m (Beech_Sapling). Placed grown, they keep the room they would have grown in.
    /// </summary>
    public static IReadOnlyDictionary<string, (float Radius, string Sapling)> GrownFrom => _catalogue.GrownFrom;

    /// <summary>
    /// The free room a kind needs to grow, and whether on cultivated ground: saplings and crops, and what
    /// they grow into (placed grown, with their sapling's room).
    /// </summary>
    public static IReadOnlyDictionary<string, (float Radius, bool Cultivated)> GrowRoom => _catalogue.Grow;

    /// <summary>
    /// What an item prefab (by hash) is; null for anything that is not an item.
    /// </summary>
    public static ItemKind? ItemKindOf(int prefab) => _catalogue.ItemKinds.GetValueOrDefault(prefab);

    /// <summary>
    /// The rule of an item stand (itemstand, itemstandh and the boss altars), or null for anything else.
    /// </summary>
    public static StandRule? ItemStandOf(int prefab) => _catalogue.ItemStands.GetValueOrDefault(prefab);

    /// <summary>
    /// An armour stand's slots in order, each with the item types it takes (empty: any), or null.
    /// </summary>
    public static int[][]? ArmourSlotsOf(int prefab) => _catalogue.ArmourStands.GetValueOrDefault(prefab);

    /// <summary>
    /// What a spawn point or nest makes; null for anything else.
    /// </summary>
    public static SpawnKind? SpawnOf(int prefab) => _catalogue.Spawns.GetValueOrDefault(prefab);

    /// <summary>
    /// What a spawn point or nest makes, by prefab name; null for anything else.
    /// </summary>
    public static SpawnKind? SpawnOf(string name) => SpawnOf(StableHash.Of(name));

    /// <summary>
    /// Whether a prefab is an invisible spawn point (a CreatureSpawner or TriggerSpawner), not a nest.
    /// </summary>
    public static bool IsSpawnPoint(string name) => SpawnOf(name) is { Nest: false };

    /// <summary>
    /// Whether a prefab is a nest that spawns creatures around itself (a SpawnArea).
    /// </summary>
    public static bool IsNest(string name) => SpawnOf(name) is { Nest: true };

#pragma warning disable IDE1006 // named as the JSON keys they are read from
#pragma warning restore IDE1006
}
