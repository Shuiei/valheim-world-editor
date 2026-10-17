using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Every game prefab with a ZNetView, read from the player's own Valheim (GameData; the tests use
/// tests/fixtures/prefabs.json): what a new object of it needs in the save. This lets the editor create
/// any kind of object, also ones that do not exist anywhere in the world yet.
/// </summary>
public static class PrefabCatalog
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
    public sealed record Info(string Name, ushort Flags, float GrowRadius, bool NeedsCultivated, int ContainerW = 0, int ContainerH = 0, float WardRadius = 0, float BuildRange = 0);

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
    private sealed record Data(Dictionary<int, Info> ByHash, Dictionary<int, string> AllNames, List<string> ItemNames, Dictionary<int, Info> Extra, HashSet<int> Creatures,
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
    /// What an item is, for the stands that hold items.
    /// </summary>
    /// <param name="Type">Its type (ItemDrop.ItemData.ItemType).</param>
    /// <param name="AttachOverride">The type it attaches as when that differs (atgeirs).</param>
    /// <param name="Variants">Its number of variants (shield styles).</param>
    /// <param name="Attach">Whether it has an "attach" child (item stands take no item without).</param>
    /// <param name="AttachSkin">Whether it has an "attach_skin" child (armour stands take either, or chest and legs
    /// pieces without).</param>
    /// <param name="Durability">For items that wear, their durability when new at quality 1 (0 for items that do
    /// not wear).</param>
    /// <param name="DurabilityPerLevel">What each further quality level adds to it.</param>
    /// <param name="MaxStack">The largest stack of the item (1 for items that do not stack).</param>
    public sealed record ItemKind(int Type, int AttachOverride, int Variants, bool Attach, bool AttachSkin, float Durability = 0, float DurabilityPerLevel = 0, int MaxStack = 1)
    {
        /// <summary>
        /// A new item's durability at this quality (ItemDrop.ItemData.GetMaxDurability); 100 for items that do not
        /// wear.
        /// </summary>
        public float NewDurability(int quality) => Durability > 0 ? Durability + Math.Max(0, quality - 1) * DurabilityPerLevel : 100f;
    }

    /// <summary>
    /// An item stand's rule (ItemStand.CanAttach): items of these types, and these items whatever their
    /// type, but never the refused ones.
    /// </summary>
    /// <param name="Types">Item types (ItemDrop.ItemData.ItemType) it takes.</param>
    /// <param name="Takes">Item prefab names it takes whatever their type.</param>
    /// <param name="Refuses">Item prefab names it never takes.</param>
    public sealed record StandRule(IReadOnlySet<int> Types, IReadOnlySet<string> Takes, IReadOnlySet<string> Refuses);

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
    /// What a spawner makes: an invisible spawn point (CreatureSpawner, or a TriggerSpawner an event wakes) or a
    /// nest (SpawnArea: a greydwarf nest, a bone pile...) that spawns around itself.
    /// </summary>
    /// <param name="Creatures">The creatures' prefab names (one for a CreatureSpawner).</param>
    /// <param name="Nest">A nest (SpawnArea), not a spawn point.</param>
    /// <param name="Trigger">A spawn point an event wakes (TriggerSpawner), not one that spawns by itself.</param>
    /// <param name="RespawnMinutes">A CreatureSpawner's time before its creature comes back once dead; 0 when it
    /// spawns its creature once.</param>
    /// <param name="MinLevel">The lowest level (1 = no star).</param>
    /// <param name="MaxLevel">The highest level, as the game's data has it (see <see cref="Levels"/>).</param>
    /// <param name="Day">Spawns by day.</param>
    /// <param name="Night">Spawns by night.</param>
    /// <param name="Radius">A nest's radius it spawns in.</param>
    /// <param name="MaxNear">How many creatures a nest keeps around.</param>
    public sealed record SpawnKind(IReadOnlyList<string> Creatures, bool Nest, bool Trigger, float RespawnMinutes, int MinLevel, int MaxLevel, bool Day, bool Night, float Radius, int MaxNear)
    {
        /// <summary>
        /// Whether the creature comes back after it dies.
        /// </summary>
        public bool Respawns => !Nest && !Trigger && RespawnMinutes > 0;

        /// <summary>
        /// The levels the creature can have, as the game rolls them (CreatureSpawner.Spawn): from the lowest up to
        /// the highest, and none above 1 when the highest is 1 (some spawners have 3 to 1: always level 1).
        /// </summary>
        public (int Min, int Max) Levels => MaxLevel > 1 ? (Math.Min(MinLevel, MaxLevel), MaxLevel) : (1, 1);

        /// <summary>
        /// One line on what it spawns: "Boar · 0–2 ★ · spawns once", "Draugr · 0–2 ★ · back 60 min after it dies ·
        /// by night only", "Greydwarf, Greydwarf_Elite · up to 3 around, within 5 m".
        /// </summary>
        public string Summary()
        {
            var parts = new List<string> { string.Join(", ", Creatures) };
            if (Nest)
            {
                parts.Add(MaxNear > 0 ? FormattableString.Invariant($"up to {MaxNear} around, within {Radius:0.#} m") : FormattableString.Invariant($"within {Radius:0.#} m"));
                return string.Join(" · ", parts);
            }
            var (lo, hi) = Levels;
            if (hi > 1)
            {
                parts.Add(lo == hi ? $"{lo - 1} ★" : $"{lo - 1}–{hi - 1} ★");
            }
            parts.Add(Trigger ? "when an event wakes it" : Respawns ? FormattableString.Invariant($"back {RespawnMinutes:0.#} min after it dies") : "spawns once");
            if (Day != Night)
            {
                parts.Add(Night ? "by night only" : "by day only");
            }
            return string.Join(" · ", parts);
        }
    }

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
    /// <summary>
    /// One prefab as PrefabScan writes it (flags as 0 or 1).
    /// </summary>
    /// <param name="p">Persistent.</param>
    /// <param name="d">Distant.</param>
    /// <param name="t">Object type (two bits).</param>
    /// <param name="c">A creature.</param>
    /// <param name="i">An item.</param>
    /// <param name="gr">Grow radius, metres.</param>
    /// <param name="cult">Grows only on cultivated ground.</param>
    /// <param name="cw">Container grid columns.</param>
    /// <param name="ch">Container grid rows.</param>
    /// <param name="ward">Ward radius.</param>
    /// <param name="build">Crafting station build range.</param>
    /// <param name="grows">What a sapling grows into.</param>
    /// <param name="it">Item type; -1 for anything that is not an item.</param>
    /// <param name="ao">Attach override type.</param>
    /// <param name="var">Number of variants.</param>
    /// <param name="at">Has an "attach" child.</param>
    /// <param name="ats">Has an "attach_skin" child.</param>
    /// <param name="dur">Durability when new at quality 1.</param>
    /// <param name="durl">Durability added per quality level.</param>
    /// <param name="ms">Item: the largest stack (absent: 1, it does not stack). The catalogue is scanned again by each
    /// build of the editor (GameData's cache stamp), so none lacks it.</param>
    /// <param name="is">Item stand: item types it takes.</param>
    /// <param name="isi">Item stand: items it takes whatever their type.</param>
    /// <param name="isu">Item stand: items it refuses.</param>
    /// <param name="as">Armour stand: item types per slot.</param>
    /// <param name="cs">Spawn point: the creatures it spawns.</param>
    /// <param name="sa">Nest: the creatures it spawns.</param>
    /// <param name="trig">Spawn point an event wakes.</param>
    /// <param name="rt">Spawn point: respawn time, minutes.</param>
    /// <param name="lv">Spawn point: lowest and highest level.</param>
    /// <param name="day">Spawn point: spawns by day.</param>
    /// <param name="night">Spawn point: spawns by night.</param>
    /// <param name="sr">Nest: radius it spawns in.</param>
    /// <param name="max">Nest: how many it keeps around.</param>
    private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0, int cw = 0, int ch = 0, double ward = 0, double build = 0, string[]? grows = null,
        int it = -1, int ao = 0, int var = 0, int at = 0, int ats = 0, double dur = 0, double durl = 0, int ms = 1, int[]? @is = null, string[]? isi = null, string[]? isu = null, int[][]? @as = null,
        string[]? cs = null, string[]? sa = null, int trig = 0, double rt = 0, int[]? lv = null, int day = 1, int night = 1, double sr = 0, int max = 0);
#pragma warning restore IDE1006

    /// <summary>
    /// Takes the prefabs by name, as GameCatalogues' PrefabScan writes them.
    /// </summary>
    public static void Use(string json)
    {
        var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json)!;
        var d = new Data([], [], [], [], [], [], [], [], [], [], []);
        foreach (var (name, e) in all)
        {
            int hash = StableHash.Of(name);
            d.AllNames[hash] = name;
            if (e.c != 0)
            {
                d.Creatures.Add(hash);
            }
            if (e.i != 0)
            {
                d.ItemNames.Add(name);
            }
            if (e.it >= 0)
            {
                d.ItemKinds[hash] = new ItemKind(e.it, e.ao, e.var, e.at != 0, e.ats != 0, (float)e.dur, (float)e.durl, Math.Max(1, e.ms));
            }
            if (e.@is != null)
            {
                d.ItemStands[hash] = new StandRule(e.@is.ToHashSet(), (e.isi ?? []).ToHashSet(), (e.isu ?? []).ToHashSet());
            }
            if (e.@as != null)
            {
                d.ArmourStands[hash] = e.@as;
            }
            if ((e.cs ?? e.sa) is { } spawned)
            {
                d.Spawns[hash] = new SpawnKind(spawned, e.sa != null, e.trig != 0, (float)e.rt, e.lv?.ElementAtOrDefault(0) ?? 1, e.lv?.ElementAtOrDefault(1) ?? 1, e.day != 0, e.night != 0, (float)e.sr, e.max);
            }
            // Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.
            ushort flags = (ushort)((e.p != 0 ? 0x100 : 0) | (e.d != 0 ? 0x200 : 0) | ((e.t & 3) << 10));
            Info info = new(name, flags, (float)e.gr, e.cult != 0, e.cw, e.ch, (float)e.ward, (float)e.build);
            foreach (string grown in e.grows ?? [])
            {
                if (!d.GrownFrom.TryGetValue(grown, out var g) || g.Radius < e.gr)
                {
                    d.GrownFrom[grown] = ((float)e.gr, name);
                }
            }
            if (e.p == 0 || e.c != 0 || e.i != 0 || name.EndsWith("_ragdoll", StringComparison.Ordinal))
            {
                d.Extra[hash] = info;
                continue;
            }
            d.ByHash[hash] = info;
        }
        d.ItemNames.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var p in d.ByHash.Values.Where(p => p.GrowRadius > 0))
        {
            d.Grow[p.Name] = (p.GrowRadius, p.NeedsCultivated);
        }
        foreach (var (name, g) in d.GrownFrom)
        {
            d.Grow.TryAdd(name, (g.Radius, false));
        }
        _catalogue = d;
    }
}
