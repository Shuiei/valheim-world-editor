using System.Text.Json;
using AssetsTools.NET;
using ValheimGen;

namespace TerrainEditor.App;

// The game's location rules (SeedLocations.RuleSet) read from its files: the main scene's ZoneSystem
// and LocationLists, and the alt biomes (in the main scene's bundle and the bundles it needs, which the
// SoftRef manifest lists), kept in the data folder until the game changes.
//
// The order is the game's (ZoneSystem.SetupLocations): ZoneSystem's own m_locations, then the
// LocationLists by m_sortOrder (they share one: the game keeps the order they wake in, which its log
// shows as "Added N locations, M vegetations ... from main"), then the alt biomes' m_addLocations.
// A prefab's name is the file name the manifest lists for its asset ID (what SoftReference.Name gives;
// m_prefabName is not always saved, and sometimes out of date).
public static class GameLocations
{
	// The lists' wake order, by (locations, vegetations), from the game's log (Valheim 0.221). After a
	// game update that changes them, reading fails and the editor's copy is used.
	private static readonly (int Locations, int Vegetation)[] Order = { (3, 0), (2, 0), (27, 25), (4, 0), (25, 33), (25, 35) };

	public static string CacheFile => Path.Combine(AppSettings.UserDataDir, "game-locations.json");

	private static readonly object Lock = new();
	private static Task? _using;

	// Once a run: the game's rules in use (SeedLocations.Use) when they can be read, else the copy stays.
	public static Task UseGameRules(GameBundles? bundles)
	{
		lock (Lock)
		{
			return _using ??= bundles == null ? Task.CompletedTask : Task.Run(() =>
			{
				if (Load(bundles, CacheFile) is { } rules)
				{
					SeedLocations.Use(rules);
				}
			});
		}
	}

	// The rules from the game (from the cache when the game is unchanged), or null when they cannot be
	// read (then the editor's copy stays in use).
	public static SeedLocations.RuleSet? Load(GameBundles bundles, string? cacheFile)
	{
		try
		{
			var manifest = Manifest.Read(bundles.Folder);
			var scanned = manifest.SceneBundles("main").Where(bundles.HasBundle).ToList();
			if (scanned.Count == 0)
			{
				return null;
			}
			string stamp = JsonSerializer.Serialize(scanned.Select(b => new FileInfo(bundles.PathOf(b)!)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
			if (cacheFile != null && File.Exists(cacheFile))
			{
				string[] cached = File.ReadAllText(cacheFile).Split('\n', 2);
				if (cached.Length == 2 && cached[0] == stamp)
				{
					return SeedLocations.RuleSet.FromJson(cached[1]);
				}
			}
			var rules = Read(bundles, scanned, manifest);
			if (rules != null && cacheFile != null)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
				File.WriteAllText(cacheFile, stamp + "\n" + rules.ToJson());
			}
			return rules;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or KeyNotFoundException)
		{
			Console.WriteLine($"The game's location rules could not be read ({e.Message}): the editor's copy is used.");
			return null;
		}
	}

	private static SeedLocations.RuleSet? Read(GameBundles bundles, List<string> scanned, Manifest manifest)
	{
		AssetTypeValueField? zone = null, alts = null;
		var lists = new Dictionary<(int, int), AssetTypeValueField>();
		foreach (string bundle in scanned)
		{
			foreach (var f in bundles.MonoBehaviours(bundle))
			{
				var locations = f["m_locations"];
				if (!locations.IsDummy && !f["m_zoneSize"].IsDummy)
				{
					// Some bundles hold another ZoneSystem too (fewer locations): the main one has most.
					if (zone == null || Count(locations) > Count(zone))
					{
						zone = locations;
					}
				}
				else if (!locations.IsDummy && !f["m_sortOrder"].IsDummy)
				{
					var key = (Count(locations), Count(f["m_vegetation"]));
					if (Order.Contains(key))
					{
						lists.TryAdd(key, locations);
					}
				}
				else if (!f["m_alts"].IsDummy && alts == null)
				{
					alts = f["m_alts"];
				}
			}
		}
		if (zone == null || alts == null || Order.Any(k => !lists.ContainsKey(k)))
		{
			Console.WriteLine($"The game's location rules were not all found (ZoneSystem: {zone != null}, alt biomes: {alts != null}, lists: {lists.Count}/{Order.Length}); a game update? The editor's copy is used.");
			return null;
		}
		var rules = new SeedLocations.RuleSet();
		rules.locations.AddRange(Items(zone).Select(l => Location(l, manifest, null)));
		foreach (var key in Order)
		{
			rules.locations.AddRange(Items(lists[key]).Select(l => Location(l, manifest, null)));
		}
		foreach (var a in Items(alts))
		{
			string name = a["m_name"].AsString;
			rules.altBiomes.Add(new SeedLocations.AltBiomeRule { name = name, blockLocationNames = Items(a["m_blockLocationNames"]).Select(s => s.AsString).ToList() });
			rules.locations.AddRange(Items(a["m_addLocations"]).Select(l => Location(l, manifest, name)));
		}
		return rules;
	}

	private static List<AssetTypeValueField> Items(AssetTypeValueField list) => list["Array"].IsDummy ? list.Children : list["Array"].Children;

	private static int Count(AssetTypeValueField list) => list.IsDummy ? 0 : Items(list).Count;

	// One ZoneLocation: each rule property from the field of the same name (m_ before it).
	private static SeedLocations.Rule Location(AssetTypeValueField l, Manifest manifest, string? altBiome)
	{
		var rule = new SeedLocations.Rule { altBiome = altBiome };
		foreach (var prop in typeof(SeedLocations.Rule).GetProperties())
		{
			var f = l["m_" + prop.Name];
			if (f.IsDummy || f.Value == null)
			{
				continue;
			}
			if (prop.PropertyType == typeof(int))
			{
				prop.SetValue(rule, f.Value.ValueType == AssetValueType.Bool ? (f.AsBool ? 1 : 0) : f.AsInt);
			}
			else if (prop.PropertyType == typeof(float))
			{
				prop.SetValue(rule, f.AsFloat);
			}
			else if (prop.PropertyType == typeof(string) && prop.Name != nameof(SeedLocations.Rule.altBiome))
			{
				prop.SetValue(rule, f.AsString);
			}
		}
		var id = l["m_prefab"]["m_assetID"];
		string hex = $"{id["v3"].AsUInt:x8}{id["v2"].AsUInt:x8}{id["v1"].AsUInt:x8}{id["v0"].AsUInt:x8}";
		rule.prefab = manifest.Names.GetValueOrDefault(hex) ?? l["m_prefabName"].AsString;
		return rule;
	}

	// The editor's copy (WorldGen/locations.json) made again from the game: --export-locations <file>.
	public static int Export(string? path)
	{
		if (path == null)
		{
			Console.Error.WriteLine("--export-locations needs the file to write.");
			return 2;
		}
		var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
		var rules = bundles == null ? null : Load(bundles, null);
		if (rules == null)
		{
			Console.Error.WriteLine(bundles == null ? "Valheim was not found." : "The location rules could not be read (see the log).");
			return 1;
		}
		File.WriteAllText(path, rules.ToJson());
		Console.WriteLine($"{rules.locations.Count} locations, {rules.altBiomes.Count} alt biomes written to {path}");
		return 0;
	}

	// The SoftRef manifest beside the bundles folder: each asset's file name by its ID, and each
	// bundle's dependencies.
	private sealed class Manifest
	{
		public Dictionary<string, string> Names { get; } = new();
		private readonly Dictionary<string, List<string>> _needs = new();
		private readonly Dictionary<string, string> _sceneBundle = new();

		public static Manifest Read(string bundlesDir)
		{
			var m = new Manifest();
			string? id = null, bundle = null, depsOf = null;
			foreach (string raw in File.ReadLines(Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(bundlesDir))!, "manifest")))
			{
				string line = raw.Trim();
				if (raw.StartsWith("- bundle: ", StringComparison.Ordinal))
				{
					depsOf = line[10..];
					m._needs[depsOf] = new();
				}
				else if (depsOf != null && raw.StartsWith("  - ", StringComparison.Ordinal))
				{
					m._needs[depsOf].Add(line[2..]);
				}
				else if (line.StartsWith("- asset ID: ", StringComparison.Ordinal))
				{
					depsOf = null;
					id = line[12..];
					bundle = null;
				}
				else if (line.StartsWith("bundle: ", StringComparison.Ordinal))
				{
					bundle = line[8..];
				}
				else if (line.StartsWith("path in bundle: ", StringComparison.Ordinal) && id != null)
				{
					string file = line[16..];
					m.Names[id] = Path.GetFileNameWithoutExtension(file);
					if (file.EndsWith(".unity", StringComparison.Ordinal) && bundle != null)
					{
						m._sceneBundle[Path.GetFileNameWithoutExtension(file)] = bundle;
					}
				}
			}
			return m;
		}

		// A scene's bundle, then the bundles it needs.
		public List<string> SceneBundles(string scene) => _sceneBundle.TryGetValue(scene, out string? b)
			? new[] { b }.Concat(_needs.GetValueOrDefault(b) ?? new()).ToList()
			: new();
	}
}
