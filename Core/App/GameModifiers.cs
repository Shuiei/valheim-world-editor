using System.Text.Json;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// The game's terrain modifiers (how locations and a few objects shape the ground, TerrainModifiers)
// read from the player's own Valheim: the editor has no copy of its own, which would go out of date
// with each game update. Read at the editor's start (GameCatalogues.TerrainModifiersJson: a few
// seconds) and kept in the data folder until the game's files change. Without them the ground near
// locations cannot be known, so no world or Workshop opens until Valheim is found (Ensure).
public static class GameModifiers
{
	public static string CacheFile => Path.Combine(AppSettings.UserDataDir, "game-terrain-modifiers.json");

	// What opening a world says when Valheim's files cannot be read.
	public const string Missing = "Valheim was not found, and the editor needs its files (the game's look and how its locations shape the ground). Choose its folder on the start page.";

	// What the cache was written by: modifiers read by another editor build are read again.
	private static readonly string CacheVersion = "modifiers-1 " + typeof(GameModifiers).Assembly.ManifestModule.ModuleVersionId;

	private static readonly object Lock = new();
	private static string? _usedFolder;

	// The game's modifiers in use (TerrainModifiers.Use) for this game folder: from the cache, else read
	// from the game; already in use, nothing to do. False when they cannot be had: no Valheim, or its
	// files could not be read. Modifiers given by the tests (TerrainModifiers.Use) count as had.
	public static bool Ensure(GameBundles? bundles)
	{
		lock (Lock)
		{
			if (bundles == null)
			{
				return TerrainModifiers.Loaded && _usedFolder == null;
			}
			if (TerrainModifiers.Loaded && _usedFolder == bundles.Folder)
			{
				return true;
			}
			if (Load(bundles.Folder, CacheFile) is not string json)
			{
				return false;
			}
			TerrainModifiers.Use(json);
			_usedFolder = bundles.Folder;
			return true;
		}
	}

	// The modifiers as JSON, from the cache when the game and the editor are unchanged, else read from
	// the game (and kept); null when they cannot be read.
	private static string? Load(string bundlesFolder, string? cacheFile)
	{
		try
		{
			string stamp = CacheVersion + " " + JsonSerializer.Serialize(Directory.GetFiles(bundlesFolder).Order(StringComparer.Ordinal)
				.Select(f => new FileInfo(f)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
			if (cacheFile != null && Cached(cacheFile, stamp) is { } cached)
			{
				Console.WriteLine("Terrain modifiers: from the cache.");
				return cached;
			}
			var watch = System.Diagnostics.Stopwatch.StartNew();
			string json = GameCatalogues.TerrainModifiersJson(bundlesFolder);
			Console.WriteLine($"Terrain modifiers: read from the game in {watch.ElapsedMilliseconds} ms.");
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
			Console.WriteLine($"The game's terrain modifiers could not be read ({e.GetType().Name}: {e.Message}).");
			return null;
		}
	}

	// The cached modifiers when they are for this game and editor; else null (read again).
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

	// The tests' copy (tests/fixtures/terrain-modifiers.json) made again from the game, keeping the
	// prefabs it already lists: --export-terrain-modifiers <file>.
	public static int Export(string? path)
	{
		if (path == null || !File.Exists(path))
		{
			Console.Error.WriteLine("--export-terrain-modifiers needs the tests' copy to make again (tests/fixtures/terrain-modifiers.json).");
			return 2;
		}
		var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
		string? json = bundles == null ? null : Load(bundles.Folder, null);
		if (json == null)
		{
			Console.Error.WriteLine(bundles == null ? "Valheim was not found." : "The terrain modifiers could not be read (see the log).");
			return 1;
		}
		var kept = JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
		using var game = JsonDocument.Parse(json);
		var trimmed = game.RootElement.EnumerateObject().Where(p => kept.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value);
		File.WriteAllText(path, JsonSerializer.Serialize(trimmed, Indented) + "\n");
		Console.WriteLine($"{trimmed.Count} of {kept.Count} prefabs written to {path}" + (trimmed.Count < kept.Count ? $" (not in the game any more: {string.Join(", ", kept.Except(trimmed.Keys))})" : ""));
		return 0;
	}
}
