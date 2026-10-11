using System.Text.Json;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// What the editor needs of the game besides its look, read from the player's own Valheim: the editor
// keeps no copy, which would go out of date with each game update (and players have different
// versions). Each kind is read at the editor's start, in seconds (those found in the bundles all in one
// pass over them), and kept in the data folder (game-<kind>.json) until the game's files change. Without them the editor cannot work right (the
// ground near locations, the names of object data...), so no world or Workshop opens until Valheim
// is found (Ensure). The tests, and the smoke tests through the driver, use trimmed copies in
// tests/fixtures (UseCopies), made again after a game update with --export-test-data.
public static class GameData
{
	// One kind: its name (the files' <name>.json), how it is read from the game (Valheim's folder, its
	// bundles -> JSON; null: in the one pass over the bundles, GameCatalogues.ReadAtStart), who takes it,
	// and how the tests' copy is trimmed (the game's and the copy's JSON -> what the copy keeps; by
	// default the entries it has).
	private sealed record Kind(string Name, Func<string, GameBundles, string>? Read, Action<string> Use, Func<JsonElement, JsonElement, object>? Trim = null);

	private static readonly Kind[] Kinds =
	{
		new("terrain-modifiers", null, TerrainModifiers.Use),
		new("prefabs", null, PrefabCatalog.Use),
		new("pieces", null, PieceCatalog.Use),
		new("piece-support", null, Editing.Stability.Use),
		new("piece-place", null, Editing.Hammer.Use),
		new("zdo-keys", (valheim, _) => GameCatalogues.ZdoKeysJson(valheim), ZdoKeys.Use),
		new("cave-rocks", (_, bundles) => GameCatalogues.CaveRocksJson(bundles), CaveRocks.Use),
		new("piece-cost", null, PieceCost.Use, TrimPieceCost),
		new("vegetation", (_, bundles) => GameCatalogues.VegetationJson(bundles), Regrow.Use, TrimVegetation),
		new("dungeon-rooms", null, Editing.Dungeons.Use, TrimDungeons),
	};

	public static string CacheFile(string kind) => Path.Combine(AppSettings.UserDataDir, $"game-{kind}.json");

	// What opening a world says when Valheim's files cannot be read.
	public const string Missing = "Valheim was not found, and the editor needs its files (the game's look and what it knows of the game's objects). Choose its folder on the start page.";

	// What a cache was written by: data read by another editor build is read again.
	private static readonly string CacheVersion = typeof(GameData).Assembly.ManifestModule.ModuleVersionId.ToString();

	private static readonly object Lock = new();
	// Where the data in use came from: a bundles folder, Copies, or none yet.
	private static string? _from;
	private const string Copies = "(copies)";

	// Every kind in use for this game: from the caches, else read from the game; already in use, nothing
	// to do. False when they cannot all be had: no Valheim, or its files could not be read. The tests'
	// copies (UseCopies) count as had, whatever game folder the tests stand in.
	public static bool Ensure(GameBundles? bundles)
	{
		lock (Lock)
		{
			if (_from == Copies)
			{
				return true;
			}
			if (bundles == null)
			{
				return false;
			}
			if (_from == bundles.Folder)
			{
				return true;
			}
			if (Load(bundles, cache: true) is not { } read)
			{
				return false;
			}
			foreach (var kind in Kinds)
			{
				kind.Use(read[kind.Name]);
			}
			_from = bundles.Folder;
			return true;
		}
	}

	// The tests' copies (<folder>/<kind>.json) in use, for where there is no game: the tests, and the
	// smoke tests through the driver.
	public static void UseCopies(string folder)
	{
		lock (Lock)
		{
			foreach (var kind in Kinds)
			{
				kind.Use(File.ReadAllText(Path.Combine(folder, kind.Name + ".json")));
			}
			_from = Copies;
		}
	}

	// Valheim's folder from its bundles folder (<Valheim>/valheim_Data/StreamingAssets/SoftRef/Bundles).
	private static string ValheimOf(string bundles)
	{
		string? d = Path.TrimEndingDirectorySeparator(bundles);
		for (int i = 0; i < 4 && d != null; i++)
		{
			d = Path.GetDirectoryName(d);
		}
		return d ?? bundles;
	}

	private static readonly string[] DataFolders = { "valheim_Data", "Valheim_Data" };

	// The game's files a kind is read from, by name, size and time: what makes a cache out of date.
	private static string Stamp(string valheim, string bundles) =>
		CacheVersion + " " + JsonSerializer.Serialize(Directory.GetFiles(bundles).Order(StringComparer.Ordinal)
			.Concat(DataFolders.Select(d => Path.Combine(valheim, d, "Managed", "assembly_valheim.dll")).Where(File.Exists))
			.Select(f => new FileInfo(f)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

	// Every kind as JSON, by name: from its cache when the game and the editor are unchanged, else read
	// from the game (and kept, with cache); null when one cannot be read.
	private static Dictionary<string, string>? Load(GameBundles bundles, bool cache)
	{
		string valheim = ValheimOf(bundles.Folder);
		string what = "files";
		try
		{
			string stamp = Stamp(valheim, bundles.Folder);
			var json = new Dictionary<string, string>();
			var read = new List<string>();
			var pass = new List<string>();
			foreach (var kind in Kinds)
			{
				if (cache && Cached(CacheFile(kind.Name), kind.Name + " " + stamp) is { } cached)
				{
					Console.WriteLine($"Game data {kind.Name}: from the cache.");
					json[kind.Name] = cached;
				}
				else if (kind.Read == null)
				{
					pass.Add(kind.Name);
				}
				else
				{
					what = kind.Name;
					var watch = System.Diagnostics.Stopwatch.StartNew();
					json[kind.Name] = kind.Read(valheim, bundles);
					Console.WriteLine($"Game data {kind.Name}: read from the game in {watch.ElapsedMilliseconds} ms.");
					read.Add(kind.Name);
				}
			}
			if (pass.Count > 0)
			{
				what = string.Join(", ", pass);
				var watch = System.Diagnostics.Stopwatch.StartNew();
				foreach (var (name, j) in GameCatalogues.ReadAtStart(bundles, pass))
				{
					json[name] = j;
				}
				Console.WriteLine($"Game data {what}: read from the game's bundles in {watch.ElapsedMilliseconds} ms.");
				read.AddRange(pass);
			}
			// Kept: written whole or not at all (a crash half way leaves the old file, or none).
			if (cache)
			{
				foreach (string name in read)
				{
					what = name;
					string file = CacheFile(name);
					Directory.CreateDirectory(Path.GetDirectoryName(file)!);
					File.WriteAllText(file + ".part", name + " " + stamp + "\n" + json[name]);
					File.Move(file + ".part", file, overwrite: true);
				}
			}
			return json;
		}
		catch (Exception e)
		{
			// Anything in the game's files can fail the reading: no world opens then.
			Console.WriteLine($"The game's {what} could not be read ({e.GetType().Name}: {e.Message}).");
			return null;
		}
	}

	// A cache when it is for this game and editor; else null (read again).
	private static string? Cached(string file, string stamp)
	{
		try
		{
			if (!File.Exists(file))
			{
				return null;
			}
			string[] cached = File.ReadAllText(file).Split('\n', 2);
			return cached.Length == 2 && cached[0] == stamp ? cached[1] : null;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	// A copy written one entry per line (an entry's value on its line, compact): small, and a game
	// update shows in git entry by entry. A few groups of entries (piece-cost's items and pieces) are
	// opened the same way.
	private static string Lines(JsonElement e, string indent = "")
	{
		string inner = indent + "  ";
		if (e.ValueKind == JsonValueKind.Object)
		{
			var props = e.EnumerateObject().ToList();
			bool groups = props.Count is > 0 and <= 4 && props.All(p => p.Value.ValueKind == JsonValueKind.Object);
			var lines = props.Select(p => inner + JsonSerializer.Serialize(p.Name) + ": " + (groups ? Lines(p.Value, inner) : p.Value.GetRawText()));
			return props.Count == 0 ? "{}" : "{\n" + string.Join(",\n", lines) + "\n" + indent + "}";
		}
		if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0)
		{
			return "[\n" + string.Join(",\n", e.EnumerateArray().Select(x => inner + x.GetRawText())) + "\n" + indent + "]";
		}
		return e.GetRawText();
	}

	// The tests' copies (<folder>/<kind>.json) made again from the game, each keeping the entries it
	// already has: --export-test-data <folder>.
	public static int Export(string? folder)
	{
		if (folder == null || !Directory.Exists(folder))
		{
			Console.Error.WriteLine("--export-test-data needs the folder of the tests' copies (tests/fixtures).");
			return 2;
		}
		var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
		if (bundles == null)
		{
			Console.Error.WriteLine("Valheim was not found.");
			return 1;
		}
		if (Kinds.Select(k => Path.Combine(folder, k.Name + ".json")).FirstOrDefault(p => !File.Exists(p)) is string missing)
		{
			Console.Error.WriteLine($"{missing} is missing.");
			return 1;
		}
		if (Load(bundles, cache: false) is not { } read)
		{
			Console.Error.WriteLine("The game's data could not be read (see the log).");
			return 1;
		}
		foreach (var kind in Kinds)
		{
			string path = Path.Combine(folder, kind.Name + ".json");
			using var copy = JsonDocument.Parse(File.ReadAllText(path));
			using var game = JsonDocument.Parse(read[kind.Name]);
			var trimmed = (kind.Trim ?? Entries)(game.RootElement, copy.RootElement);
			File.WriteAllText(path, Lines(JsonSerializer.SerializeToElement(trimmed)) + "\n");
			var kept = Kept(copy.RootElement, kind.Trim == null);
			var now = Kept(JsonSerializer.SerializeToElement(trimmed), kind.Trim == null);
			Console.WriteLine($"{path}: {now.Count} of {kept.Count} entries" + (now.Count < kept.Count ? $" (not in the game any more: {string.Join(", ", kept.Except(now))})" : ""));
		}
		return 0;
	}

	// The game's entries the copy has.
	private static Dictionary<string, object> Entries(JsonElement game, JsonElement copy)
	{
		var kept = copy.EnumerateObject().Select(p => p.Name).ToHashSet();
		return game.EnumerateObject().Where(p => kept.Contains(p.Name)).ToDictionary(p => p.Name, p => (object)p.Value);
	}

	// piece-cost: the pieces the copy has, and the items they take.
	private static Dictionary<string, object> TrimPieceCost(JsonElement game, JsonElement copy)
	{
		var pieces = Entries(game.GetProperty("pieces"), copy.GetProperty("pieces"));
		var items = pieces.Values.Cast<JsonElement>().SelectMany(p => p.GetProperty("r").EnumerateArray().Select(r => r[0].GetString())).ToHashSet();
		return new()
		{
			["items"] = game.GetProperty("items").EnumerateObject().Where(p => items.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value),
			["pieces"] = pieces,
		};
	}

	// dungeon-rooms: every dungeon kind, and the rooms the copy has.
	private static Dictionary<string, object> TrimDungeons(JsonElement game, JsonElement copy) => new()
	{
		["dungeons"] = game.GetProperty("dungeons"),
		["rooms"] = Entries(game.GetProperty("rooms"), copy.GetProperty("rooms")),
	};

	// vegetation (a list of rules): the game's rules the copy has, by name and prefab.
	private static List<JsonElement> TrimVegetation(JsonElement game, JsonElement copy)
	{
		var kept = copy.EnumerateArray().Select(RuleKey).ToHashSet();
		return game.EnumerateArray().Where(r => kept.Contains(RuleKey(r))).ToList();
	}

	private static string RuleKey(JsonElement rule) => $"{rule.GetProperty("name").GetString()}/{rule.GetProperty("prefab").GetString()}";

	// What a copy holds, to tell what went: its entries (a list's: its rules), else (trimmed its own
	// way) those of each part.
	private static List<string> Kept(JsonElement copy, bool entries) =>
		copy.ValueKind == JsonValueKind.Array ? copy.EnumerateArray().Select(RuleKey).ToList()
		: copy.EnumerateObject().SelectMany(p => entries ? new[] { p.Name } : p.Value.EnumerateObject().Select(e => $"{p.Name}/{e.Name}")).ToList();
}
