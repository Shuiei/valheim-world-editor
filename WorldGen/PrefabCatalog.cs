using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Every game prefab with a ZNetView, extracted from the game's asset bundles
// (tools/asset-export/scan_prefabs.py): what a new object of it needs in the save. This lets the
// editor create any kind of object, also ones that do not exist anywhere in the world yet.
public static class PrefabCatalog
{
	// GrowRadius: saplings and crops need this much free space to grow (0 for anything else).
	public sealed record Info(string Name, ushort Flags, float GrowRadius, bool NeedsCultivated);

	private static readonly Dictionary<int, Info> ByHash = Load();

	// Kinds the editor offers to place: saved objects (persistent), not creatures, item drops or ragdolls.
	public static IEnumerable<Info> Placeable => ByHash.Values;

	public static Info? Get(int prefab) => ByHash.TryGetValue(prefab, out var info) ? info : null;

	private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0);

	private static Dictionary<int, Info> Load()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.prefabs.json")
			?? throw new InvalidOperationException("prefabs.json is not embedded");
		var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(s)!;
		Dictionary<int, Info> map = new();
		foreach (var (name, e) in all)
		{
			if (e.p == 0 || e.c != 0 || e.i != 0 || name.EndsWith("_ragdoll", StringComparison.Ordinal))
			{
				continue;
			}
			// Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.
			ushort flags = (ushort)((e.p != 0 ? 0x100 : 0) | (e.d != 0 ? 0x200 : 0) | ((e.t & 3) << 10));
			map[StableHash.Of(name)] = new Info(name, flags, (float)e.gr, e.cult != 0);
		}
		return map;
	}
}
