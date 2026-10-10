using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The game's own look (models, textures, the terrain shader, the map's textures) is read from the
// user's own Valheim install, never shipped nor copied: this finds the install and keeps the one
// reader of its asset bundles (GameBundles). The first time, and after a game update, the bundles are
// indexed (a few seconds, in the background; the index is kept in a file).
public static class GameLook
{
	public const int ValheimAppId = 892970;

	private static readonly object Lock = new();

	private static GameBundles? _bundles;

	// Where older editors copied the game's look (game-look) and kept the copy's cache (export-cache):
	// deleted, nothing reads them any more.
	public static string Dir => Path.Combine(AppSettings.UserDataDir, "game-look");

	private static string OldCache => Path.Combine(AppSettings.UserDataDir, "export-cache");

	// The bundles' index (prefab -> bundle), kept between runs.
	public static string IndexFile => Path.Combine(AppSettings.UserDataDir, "game-index.json");

	// False: Steam is only looked for under the home folder (not /opt/Steam, /usr/share/steam or
	// Program Files): the editor driven by the tests and the documentation's pictures, whose stand-in
	// home must not lead to the computer's own game.
	public static bool SearchOutsideHome { get; set; } = true;

	// "ready", "missing" (Valheim not found), "running" (indexing its bundles), "failed".
	public static string State { get; private set; } = "missing";

	public static string? Message { get; private set; }

	public static string? ValheimPath { get; private set; }

	// The reader of the game's bundles (null when Valheim is not found).
	public static GameBundles? Bundles
	{
		get
		{
			lock (Lock)
			{
				return _bundles;
			}
		}
	}

	// What the start page shows: the state, its message, the Valheim folder, a detail line and the
	// progress (0..1, null when not known).
	public sealed record Snapshot(string State, string? Message, string? Valheim, string? LastLine, double? Progress);

	public static Snapshot Now()
	{
		lock (Lock)
		{
			return new Snapshot(State, Message, ValheimPath, null, State == "ready" ? 1 : null);
		}
	}

	// At start and when the game folder changes: finds Valheim and indexes its bundles (in the
	// background when the kept index is out of date).
	public static void Check(AppSettings settings)
	{
		string? valheim = FindValheim(settings.ValheimPath);
		GameBundles? game;
		lock (Lock)
		{
			ValheimPath = valheim;
			if (valheim == null)
			{
				_bundles = null;
				Set("missing", "Valheim was not found on this computer. Choose its folder to get the game's look.");
				return;
			}
			if (_bundles == null || !string.Equals(_bundles.Folder, BundlesDir(valheim), StringComparison.Ordinal))
			{
				_bundles = GameBundles.ForGame(valheim, IndexFile);
			}
			game = _bundles;
			if (game == null)
			{
				Set("failed", "The game's files could not be read.");
				return;
			}
			if (game.Indexed)
			{
				Set("ready", null);
				return;
			}
			Set("running", "Reading the game's files (a few seconds, only after a game update).");
		}
		Task.Run(() =>
		{
			string state, message;
			try
			{
				int n = game.Index.Count;
				(state, message) = n > 0 ? ("ready", "The game's look is ready.") : ("failed", "No models were found in the game's files.");
			}
			catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or NotImplementedException)
			{
				Console.WriteLine($"game look: {e}");
				(state, message) = ("failed", "The game's files could not be read: " + e.Message);
			}
			lock (Lock)
			{
				if (ReferenceEquals(_bundles, game))
				{
					Set(state, message);
				}
			}
		});
	}

	// Use this Valheim folder: kept in the settings, then read. The problem, or null.
	public static string? Start(string valheim, AppSettings settings)
	{
		valheim = valheim.Trim().Trim('"');
		if (BundlesDir(valheim) == null)
		{
			return "That folder is not a Valheim game folder (it has no valheim_Data). Pick the folder Steam installed Valheim into.";
		}
		settings.ValheimPath = valheim;
		settings.Save();
		Check(settings);
		return null;
	}

	private static void Set(string state, string? message)
	{
		if (state != State || message != Message)
		{
			Console.WriteLine($"game look: {state}{(message == null ? "" : ": " + message)}");
		}
		State = state;
		Message = message;
	}

	// The copy older editors made (up to 1.16): about 150 MB nothing reads any more. Called by the app
	// at start (not by the tests' editors: their data folder is the user's).
	public static void DeleteOldCopy()
	{
		foreach (string d in new[] { Dir, OldCache })
		{
			try
			{
				if (Directory.Exists(d))
				{
					Directory.Delete(d, true);
					Console.WriteLine($"game look: deleted the old copy in {d}");
				}
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				Console.WriteLine($"game look: could not delete the old copy in {d}: {e.Message}");
			}
		}
	}

	// ---- Finding things.

	public static string? BundlesDir(string valheim)
	{
		foreach (string sub in new[] { "valheim_Data", "Valheim_Data" })
		{
			string d = Path.Combine(valheim, sub, "StreamingAssets", "SoftRef", "Bundles");
			if (Directory.Exists(d))
			{
				return d;
			}
		}
		return null;
	}

	// The configured folder if it is valid, else Valheim in any Steam library on this computer.
	public static string? FindValheim(string? configured)
	{
		if (configured != null && BundlesDir(configured) != null)
		{
			return configured;
		}
		foreach (string lib in SteamLibraries())
		{
			string v = Path.Combine(lib, "steamapps", "common", "Valheim");
			if (BundlesDir(v) != null)
			{
				return v;
			}
		}
		return null;
	}

	private static readonly string[] SystemSteamRoots = { "/opt/Steam", "/usr/share/steam" };

	private static IEnumerable<string> SteamLibraries()
	{
		var roots = new List<string>();
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsWindows())
		{
			try
			{
				if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string sp)
				{
					roots.Add(sp.Replace('/', '\\'));
				}
			}
			catch
			{
			}
			if (SearchOutsideHome)
			{
				roots.Add(@"C:\Program Files (x86)\Steam");
				roots.Add(@"C:\Program Files\Steam");
			}
		}
		else
		{
			roots.AddRange(new[]
			{
				Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".steam", "root"),
				Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
			});
			if (SearchOutsideHome)
			{
				roots.AddRange(SystemSteamRoots);
			}
		}
		var seen = new HashSet<string>();
		foreach (string root in roots)
		{
			if (!Directory.Exists(root) || !seen.Add(Path.GetFullPath(root)))
			{
				continue;
			}
			yield return root;
			// Other libraries are listed in libraryfolders.vdf: "path"   "D:\\SteamLibrary"
			string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(vdf))
			{
				continue;
			}
			foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
			{
				string lib = m.Groups[1].Value.Replace(@"\\", @"\");
				if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib)))
				{
					yield return lib;
				}
			}
		}
	}
}
