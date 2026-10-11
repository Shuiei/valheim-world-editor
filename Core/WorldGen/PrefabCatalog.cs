using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Every game prefab with a ZNetView, extracted from the game's asset bundles
// (tools/asset-export/scan_prefabs.py): what a new object of it needs in the save. This lets the
// editor create any kind of object, also ones that do not exist anywhere in the world yet.
public static class PrefabCatalog
{
	// The name of a prefab: a building piece's, or any other kind's from the game's list.
	public static string? DisplayName(int prefab) => PieceCatalog.Get(prefab)?.Name ?? NameOf(prefab);

	// GrowRadius: saplings and crops need this much free space to grow (0 for anything else).
	// ContainerW x ContainerH: the grid of a container (0 when the prefab is none, or its container is
	// on a child object, like carts and ships). WardRadius: area a ward protects. BuildRange: radius a
	// crafting station lets players build in.
	public sealed record Info(string Name, ushort Flags, float GrowRadius, bool NeedsCultivated, int ContainerW = 0, int ContainerH = 0, float WardRadius = 0, float BuildRange = 0);

	// Every prefab of the catalogue by hash, placeable or not (items, creatures...): for names.
	private static readonly Dictionary<int, string> AllNames = new();

	private static readonly List<string> ItemNames = new();

	private static readonly Dictionary<int, Info> Extra = new();

	// Creatures (not offered for placing, but generated dungeons and blueprints carry their bosses).
	private static readonly HashSet<int> CreatureHashes = new();

	public static bool IsCreature(int prefab) => CreatureHashes.Contains(prefab);

	// GrownFrom's table, made before ByHash's initializer runs Load (which fills it): static initializers
	// run in the file's order, so one written after ByHash would replace the filled table with an empty one.
	private static readonly Dictionary<string, (float Radius, string Sapling)> GrownFromStart = new();

	// Filled by Load: declared before ByHash for the reason above.
	private static readonly Dictionary<int, ItemKind> ItemKinds = new();

	private static readonly Dictionary<int, StandRule> ItemStands = new();

	private static readonly Dictionary<int, int[][]> ArmourStands = new();

	private static readonly Dictionary<int, Info> ByHash = Load();

	// Kinds the editor offers to place: saved objects (persistent), not creatures, item drops or ragdolls.
	public static IEnumerable<Info> Placeable => ByHash.Values;

	public static Info? Get(int prefab) => ByHash.TryGetValue(prefab, out var info) ? info : null;

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
	public static string? NameOf(int prefab) => AllNames.TryGetValue(prefab, out string? n) ? n : RunestoneNames.GetValueOrDefault(prefab);

	// Item prefabs (what containers and item stands can hold), by name.
	public static IReadOnlyList<string> Items => ItemNames;

	// Container size, ward radius and build range, also for prefabs that are not offered for placing.
	public static Info? Details(int prefab) => ByHash.TryGetValue(prefab, out var info) ? info : Extra.TryGetValue(prefab, out var e) ? e : null;

	// What saplings grow into, with the sapling's grow radius: Pickable_Carrot -> 0.5 m (sapling_carrot),
	// Beech1 -> 2 m (Beech_Sapling). Placed grown, they keep the room they would have grown in.
	// Filled by Load (see GrownFromStart).
	public static Dictionary<string, (float Radius, string Sapling)> GrownFrom { get; private set; } = GrownFromStart;

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

	public static ItemKind? ItemKindOf(int prefab) => ItemKinds.GetValueOrDefault(prefab);

	// The rule of an item stand (itemstand, itemstandh and the boss altars), or null for anything else.
	public static StandRule? ItemStandOf(int prefab) => ItemStands.GetValueOrDefault(prefab);

	// An armour stand's slots in order, each with the item types it takes (empty: any), or null.
	public static int[][]? ArmourSlotsOf(int prefab) => ArmourStands.GetValueOrDefault(prefab);

	private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0, int cw = 0, int ch = 0, double ward = 0, double build = 0, string[]? grows = null,
		int it = -1, int ao = 0, int var = 0, int at = 0, int ats = 0, double dur = 0, double durl = 0, int[]? @is = null, string[]? isi = null, string[]? isu = null, int[][]? @as = null);

	private static Dictionary<int, Info> Load()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.prefabs.json")
			?? throw new InvalidOperationException("prefabs.json is not embedded");
		var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(s)!;
		GrownFrom = GrownFromStart;
		Dictionary<int, Info> map = new();
		foreach (var (name, e) in all)
		{
			int hash = StableHash.Of(name);
			AllNames[hash] = name;
			if (e.c != 0)
			{
				CreatureHashes.Add(hash);
			}
			if (e.i != 0)
			{
				ItemNames.Add(name);
			}
			if (e.it >= 0)
			{
				ItemKinds[hash] = new ItemKind(e.it, e.ao, e.var, e.at != 0, e.ats != 0, (float)e.dur, (float)e.durl);
			}
			if (e.@is != null)
			{
				ItemStands[hash] = new StandRule(e.@is.ToHashSet(), (e.isi ?? Array.Empty<string>()).ToHashSet(), (e.isu ?? Array.Empty<string>()).ToHashSet());
			}
			if (e.@as != null)
			{
				ArmourStands[hash] = e.@as;
			}
			// Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.
			ushort flags = (ushort)((e.p != 0 ? 0x100 : 0) | (e.d != 0 ? 0x200 : 0) | ((e.t & 3) << 10));
			Info info = new(name, flags, (float)e.gr, e.cult != 0, e.cw, e.ch, (float)e.ward, (float)e.build);
			foreach (string grown in e.grows ?? Array.Empty<string>())
			{
				if (!GrownFrom.TryGetValue(grown, out var g) || g.Radius < e.gr)
				{
					GrownFrom[grown] = ((float)e.gr, name);
				}
			}
			if (e.p == 0 || e.c != 0 || e.i != 0 || name.EndsWith("_ragdoll", StringComparison.Ordinal))
			{
				Extra[hash] = info;
				continue;
			}
			map[hash] = info;
		}
		ItemNames.Sort(StringComparer.OrdinalIgnoreCase);
		return map;
	}
}
