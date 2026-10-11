using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Every game prefab with a ZNetView, read from the player's own Valheim (GameData; the tests use
// tests/fixtures/prefabs.json): what a new object of it needs in the save. This lets the editor create
// any kind of object, also ones that do not exist anywhere in the world yet.
public static class PrefabCatalog
{
    // The name of a prefab: a building piece's, or any other kind's from the game's list.
    public static string? DisplayName(int prefab) => PieceCatalog.Get(prefab)?.Name ?? NameOf(prefab);

    // GrowRadius: saplings and crops need this much free space to grow (0 for anything else).
    // ContainerW x ContainerH: the grid of a container (0 when the prefab is none, or its container is
    // on a child object, like carts and ships). WardRadius: area a ward protects. BuildRange: radius a
    // crafting station lets players build in.
    public sealed record Info(string Name, ushort Flags, float GrowRadius, bool NeedsCultivated, int ContainerW = 0, int ContainerH = 0, float WardRadius = 0, float BuildRange = 0);

    // None until Use: no world or Workshop opens before then (GameData).
    private static volatile Data Catalogue = new(new(), new(), new(), new(), new(), new(), new(), new(), new(), new());

    // AllNames: every prefab of the catalogue by hash, placeable or not (items, creatures...), for names.
    // ByHash: the kinds offered for placing; Extra: the others. Creatures: not offered for placing, but
    // generated dungeons and blueprints carry their bosses. Grow: the room saplings and what they grow
    // into need (see GrowRoom).
    private sealed record Data(Dictionary<int, Info> ByHash, Dictionary<int, string> AllNames, List<string> ItemNames, Dictionary<int, Info> Extra, HashSet<int> Creatures,
        Dictionary<string, (float Radius, string Sapling)> GrownFrom, Dictionary<int, ItemKind> ItemKinds, Dictionary<int, StandRule> ItemStands, Dictionary<int, int[][]> ArmourStands,
        Dictionary<string, (float Radius, bool Cultivated)> Grow);

    public static bool IsCreature(int prefab) => Catalogue.Creatures.Contains(prefab);

    // Kinds the editor offers to place: saved objects (persistent), not creatures, item drops or ragdolls.
    public static IEnumerable<Info> Placeable => Catalogue.ByHash.Values;

    public static Info? Get(int prefab) => Catalogue.ByHash.TryGetValue(prefab, out var info) ? info : null;

    // Runestones are not objects but locations: the save keeps a location proxy for each, and the game
    // builds the stone from it whenever the zone loads. The editor lists a runestone's proxy under the
    // location's name (its hash stands for the prefab), so it can be shown, picked and deleted, never
    // moved or copied (that would need the proxy's own prefab).
    public static readonly string[] RunestoneLocations =
    {
        "Runestone_Ashlands", "Runestone_BlackForest", "Runestone_Boars", "Runestone_DeepNorth", "Runestone_Draugr", "Runestone_Greydwarfs",
        "Runestone_Meadows", "Runestone_Mistlands", "Runestone_Mountains", "Runestone_Plains", "Runestone_Swamps",
    };

    private static readonly Dictionary<int, string> RunestoneNames = RunestoneLocations.ToDictionary(StableHash.Of);

    public static bool IsRunestone(int prefab) => RunestoneNames.ContainsKey(prefab);

    public static bool IsRunestoneName(string name) => name.StartsWith("Runestone_", StringComparison.Ordinal);

    // The game's name of any prefab of the catalogue (also items and creatures), or null.
    public static string? NameOf(int prefab) => Catalogue.AllNames.TryGetValue(prefab, out string? n) ? n : RunestoneNames.GetValueOrDefault(prefab);

    // Item prefabs (what containers and item stands can hold), by name.
    public static IReadOnlyList<string> Items => Catalogue.ItemNames;

    // Container size, ward radius and build range, also for prefabs that are not offered for placing.
    public static Info? Details(int prefab) => Catalogue.ByHash.TryGetValue(prefab, out var info) ? info : Catalogue.Extra.TryGetValue(prefab, out var e) ? e : null;

    // What saplings grow into, with the sapling's grow radius: Pickable_Carrot -> 0.5 m (sapling_carrot),
    // Beech1 -> 2 m (Beech_Sapling). Placed grown, they keep the room they would have grown in.
    public static IReadOnlyDictionary<string, (float Radius, string Sapling)> GrownFrom => Catalogue.GrownFrom;

    // The free room a kind needs to grow, and whether on cultivated ground: saplings and crops, and what
    // they grow into (placed grown, with their sapling's room).
    public static IReadOnlyDictionary<string, (float Radius, bool Cultivated)> GrowRoom => Catalogue.Grow;

    // What an item is, for the stands that hold items: its type (ItemDrop.ItemData.ItemType), the type it
    // attaches as when that differs (atgeirs), its number of variants (shield styles), and whether it has
    // an "attach" child (item stands take no item without) or an "attach_skin" one (armour stands take
    // either, or chest and legs pieces without), and for items that wear, their durability when new at
    // quality 1 and what each further level adds (0 for items that do not wear).
    public sealed record ItemKind(int Type, int AttachOverride, int Variants, bool Attach, bool AttachSkin, float Durability = 0, float DurabilityPerLevel = 0)
    {
        // A new item's durability at this quality (ItemDrop.ItemData.GetMaxDurability); 100 for items that do not wear.
        public float NewDurability(int quality) => Durability > 0 ? Durability + Math.Max(0, quality - 1) * DurabilityPerLevel : 100f;
    }

    // An item stand's rule (ItemStand.CanAttach): items of these types, and these items whatever their
    // type, but never the refused ones.
    public sealed record StandRule(IReadOnlySet<int> Types, IReadOnlySet<string> Takes, IReadOnlySet<string> Refuses);

    public static ItemKind? ItemKindOf(int prefab) => Catalogue.ItemKinds.GetValueOrDefault(prefab);

    // The rule of an item stand (itemstand, itemstandh and the boss altars), or null for anything else.
    public static StandRule? ItemStandOf(int prefab) => Catalogue.ItemStands.GetValueOrDefault(prefab);

    // An armour stand's slots in order, each with the item types it takes (empty: any), or null.
    public static int[][]? ArmourSlotsOf(int prefab) => Catalogue.ArmourStands.GetValueOrDefault(prefab);

    private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0, int cw = 0, int ch = 0, double ward = 0, double build = 0, string[]? grows = null,
        int it = -1, int ao = 0, int var = 0, int at = 0, int ats = 0, double dur = 0, double durl = 0, int[]? @is = null, string[]? isi = null, string[]? isu = null, int[][]? @as = null);

    // The prefabs by name, as GameCatalogues' PrefabScan writes them.
    public static void Use(string json)
    {
        var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json)!;
        var d = new Data(new(), new(), new(), new(), new(), new(), new(), new(), new(), new());
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
                d.ItemStands[hash] = new StandRule(e.@is.ToHashSet(), (e.isi ?? Array.Empty<string>()).ToHashSet(), (e.isu ?? Array.Empty<string>()).ToHashSet());
            }
            if (e.@as != null)
            {
                d.ArmourStands[hash] = e.@as;
            }
            // Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.
            ushort flags = (ushort)((e.p != 0 ? 0x100 : 0) | (e.d != 0 ? 0x200 : 0) | ((e.t & 3) << 10));
            Info info = new(name, flags, (float)e.gr, e.cult != 0, e.cw, e.ch, (float)e.ward, (float)e.build);
            foreach (string grown in e.grows ?? Array.Empty<string>())
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
        Catalogue = d;
    }
}
