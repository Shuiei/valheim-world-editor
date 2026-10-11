using System.Text.Json;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// What the editor needs of the game besides its look, read from the player's own Valheim: the editor
// keeps no copy, which would go out of date with each game update (and players have different
// versions). Each kind is read at the editor's start, in seconds, and kept in the data folder
// (game-<kind>.json) until the game's files change. Without them the editor cannot work right (the
// ground near locations, the names of object data...), so no world or Workshop opens until Valheim
// is found (Ensure). The tests, and the smoke tests through the driver, use trimmed copies in
// tests/fixtures (UseCopies), made again after a game update with --export-test-data.
public static class GameData
{
	// One kind: its name (the files' <name>.json), how it is read from the game (Valheim's folder, its
	// bundles -> JSON), and who takes it.
	private sealed record Kind(string Name, Func<string, GameBundles, string> Read, Action<string> Use);

	private static readonly Kind[] Kinds =
	{
		new("terrain-modifiers", (_, bundles) => GameCatalogues.TerrainModifiersJson(bundles.Folder), TerrainModifiers.Use),
		new("zdo-keys", (valheim, _) => GameCatalogues.ZdoKeysJson(valheim), ZdoKeys.Use),
		new("cave-rocks", (_, bundles) => GameCatalogues.CaveRocksJson(bundles), CaveRocks.Use),
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
			string valheim = ValheimOf(bundles.Folder);
			var read = new List<(Kind, string)>();
			foreach (var kind in Kinds)
			{
				if (Load(kind, valheim, bundles, CacheFile(kind.Name)) is not string json)
				{
					return false;
				}
				read.Add((kind, json));
			}
			foreach (var (kind, json) in read)
			{
				kind.Use(json);
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

	// A kind as JSON, from its cache when the game and the editor are unchanged, else read from the game
	// (and kept); null when it cannot be read.
	private static string? Load(Kind kind, string valheim, GameBundles bundles, string? cacheFile)
	{
		try
		{
			string stamp = kind.Name + " " + Stamp(valheim, bundles.Folder);
			if (cacheFile != null && Cached(cacheFile, stamp) is { } cached)
			{
				Console.WriteLine($"Game data {kind.Name}: from the cache.");
				return cached;
			}
			var watch = System.Diagnostics.Stopwatch.StartNew();
			string json = kind.Read(valheim, bundles);
			Console.WriteLine($"Game data {kind.Name}: read from the game in {watch.ElapsedMilliseconds} ms.");
			if (cacheFile != null)
			{
				// Written whole or not at all (a crash half way leaves the old file, or none).
				Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
				string part = cacheFile + ".part";
				File.WriteAllText(part, stamp + "\n" + json);
				File.Move(part, cacheFile, overwrite: true);
			}
			return json;
		}
		catch (Exception e)
		{
			// Anything in the game's files can fail the reading: no world opens then.
			Console.WriteLine($"The game's {kind.Name} could not be read ({e.GetType().Name}: {e.Message}).");
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

	private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

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
		foreach (var kind in Kinds)
		{
			string path = Path.Combine(folder, kind.Name + ".json");
			string? json = File.Exists(path) ? Load(kind, ValheimOf(bundles.Folder), bundles, null) : null;
			if (json == null)
			{
				Console.Error.WriteLine(File.Exists(path) ? $"The game's {kind.Name} could not be read (see the log)." : $"{path} is missing.");
				return 1;
			}
			var kept = JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
			using var game = JsonDocument.Parse(json);
			var trimmed = game.RootElement.EnumerateObject().Where(p => kept.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value);
			File.WriteAllText(path, JsonSerializer.Serialize(trimmed, Indented) + "\n");
			Console.WriteLine($"{path}: {trimmed.Count} of {kept.Count} entries" + (trimmed.Count < kept.Count ? $" (not in the game any more: {string.Join(", ", kept.Except(trimmed.Keys))})" : ""));
		}
		return 0;
	}
}
