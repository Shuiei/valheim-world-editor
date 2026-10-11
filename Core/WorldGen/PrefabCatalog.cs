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
    private static volatile Data _catalogue = new([], [], [], [], [], [], [], [], [], []);

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
    private sealed record Data(Dictionary<int, Info> ByHash, Dictionary<int, string> AllNames, List<string> ItemNames, Dictionary<int, Info> Extra, HashSet<int> Creatures,
        Dictionary<string, (float Radius, string Sapling)> GrownFrom, Dictionary<int, ItemKind> ItemKinds, Dictionary<int, StandRule> ItemStands, Dictionary<int, int[][]> ArmourStands,
        Dictionary<string, (float Radius, bool Cultivated)> Grow);

    /// <summary>
    /// Whether a prefab (by hash) is a creature.
    /// </summary>
    public static bool IsCreature(int prefab) => _catalogue.Creatures.Contains(prefab);

    /// <summary>
    /// Kinds the editor offers to place: saved objects (persistent), not creatures, item drops or ragdolls.
    /// </summary>
    public static IEnumerable<Info> Placeable => _catalogue.ByHash.Values;

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
    public sealed record ItemKind(int Type, int AttachOverride, int Variants, bool Attach, bool AttachSkin, float Durability = 0, float DurabilityPerLevel = 0)
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
    /// <param name="is">Item stand: item types it takes.</param>
    /// <param name="isi">Item stand: items it takes whatever their type.</param>
    /// <param name="isu">Item stand: items it refuses.</param>
    /// <param name="as">Armour stand: item types per slot.</param>
    private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0, int cw = 0, int ch = 0, double ward = 0, double build = 0, string[]? grows = null,
        int it = -1, int ao = 0, int var = 0, int at = 0, int ats = 0, double dur = 0, double durl = 0, int[]? @is = null, string[]? isi = null, string[]? isu = null, int[][]? @as = null);
#pragma warning restore IDE1006

    /// <summary>
    /// Takes the prefabs by name, as GameCatalogues' PrefabScan writes them.
    /// </summary>
    public static void Use(string json)
    {
        var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json)!;
        var d = new Data([], [], [], [], [], [], [], [], [], []);
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
                d.ItemKinds[hash] = new ItemKind(e.it, e.ao, e.var, e.at != 0, e.ats != 0, (float)e.dur, (float)e.durl);
            }
            if (e.@is != null)
            {
                d.ItemStands[hash] = new StandRule(e.@is.ToHashSet(), (e.isi ?? []).ToHashSet(), (e.isu ?? []).ToHashSet());
            }
            if (e.@as != null)
            {
                d.ArmourStands[hash] = e.@as;
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
