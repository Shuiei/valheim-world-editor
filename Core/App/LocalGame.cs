using System.Text.Json;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// Live editing of the player's own game (single player or hosting): finds the WorldEditorBridge
// plugin's settings in the Valheim install or a mod manager profile, and checks whether the game
// is running with it, so the start page can offer one click (the token never leaves this program).
public static class LocalGame
{
	public sealed record Bridge(string ConfigPath, string Where, int Port, string Token);

	public sealed record Running(Bridge Bridge, string World, int Players);

	// The plugin's settings file in BepInEx/config, and its name before 0.41.0 (an older plugin).
	public const string ConfigName = "Tie.WorldEditorBridge.cfg", OldConfigName = "local.worldeditorbridge.cfg";

	// Tests: only the folders of the settings given, never the player's own install or profiles.
	public static bool SearchDefaultPlaces { get; set; } = true;

	// Folders that can hold the game's BepInEx: the Valheim install, and mod manager profiles. Each
	// once: on Linux the application data folder is ~/.config, so r2modman's would come twice.
	public static IEnumerable<(string BepInEx, string Where)> BepInExFolders(AppSettings settings) =>
		AllBepInExFolders(settings).DistinctBy(f => Path.GetFullPath(f.BepInEx).TrimEnd(Path.DirectorySeparatorChar));

	private static IEnumerable<(string BepInEx, string Where)> AllBepInExFolders(AppSettings settings)
	{
		if (SearchDefaultPlaces && GameLook.FindValheim(settings.ValheimPath) is string valheim)
		{
			yield return (Path.Combine(valheim, "BepInEx"), "Valheim folder");
		}
		// Installed by a mod manager (Thunderstore package): the editor sits in <profile>/BepInEx/plugins/...
		if (SearchDefaultPlaces && InstalledIn() is string own)
		{
			yield return (own, "this mod manager profile");
		}
		// Folders added in Settings: a BepInEx folder itself, a profile holding one, or a folder of profiles.
		foreach (string extra in settings.BepInExFolders)
		{
			if (!Directory.Exists(extra))
			{
				continue;
			}
			if (Path.GetFileName(extra.TrimEnd('/', '\\')).Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
			{
				yield return (extra, "your folder");
			}
			else if (Directory.Exists(Path.Combine(extra, "BepInEx")))
			{
				yield return (Path.Combine(extra, "BepInEx"), $"\"{Path.GetFileName(extra.TrimEnd('/', '\\'))}\"");
			}
			else
			{
				foreach (string profile in Directory.GetDirectories(extra).Where(d => Directory.Exists(Path.Combine(d, "BepInEx"))))
				{
					yield return (Path.Combine(profile, "BepInEx"), $"profile \"{Path.GetFileName(profile)}\"");
				}
			}
		}
		if (!SearchDefaultPlaces)
		{
			yield break;
		}
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		var managers = new List<(string Root, string Name)>
		{
			(Path.Combine(appData, "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"),
			(Path.Combine(appData, "Thunderstore Mod Manager", "DataFolder", "Valheim", "profiles"), "Thunderstore Mod Manager"),
		};
		if (!OperatingSystem.IsWindows())
		{
			managers.Add((Path.Combine(home, ".config", "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"));
			managers.Add((Path.Combine(home, ".var", "app", "com.github.ebkr.r2modman", "config", "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"));
		}
		foreach (var (root, name) in managers)
		{
			if (!Directory.Exists(root))
			{
				continue;
			}
			foreach (string profile in Directory.GetDirectories(root))
			{
				yield return (Path.Combine(profile, "BepInEx"), $"{name} profile \"{Path.GetFileName(profile)}\"");
			}
		}
	}

	// The BepInEx folder holding the editor, when a mod manager installed it into a profile's plugins.
	public static string? InstalledIn(string? from = null)
	{
		for (var d = new DirectoryInfo(from ?? AppContext.BaseDirectory); d?.Parent != null; d = d.Parent)
		{
			if (d.Name.Equals("plugins", StringComparison.OrdinalIgnoreCase) && d.Parent.Name.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
			{
				return d.Parent.FullName;
			}
		}
		return null;
	}

	public static List<Bridge> FindBridges(AppSettings settings, out bool bepInEx, out bool plugin)
	{
		bepInEx = false;
		plugin = false;
		var list = new List<Bridge>();
		foreach (var (dir, where) in BepInExFolders(settings))
		{
			if (!Directory.Exists(dir))
			{
				continue;
			}
			bepInEx |= Directory.Exists(Path.Combine(dir, "core")) || Directory.Exists(Path.Combine(dir, "plugins"));
			string plugins = Path.Combine(dir, "plugins");
			plugin |= Directory.Exists(plugins) && Directory.EnumerateFiles(plugins, "WorldEditorBridge.dll", SearchOption.AllDirectories).Any();
			string cfg = Path.Combine(dir, "config", ConfigName);
			if (!File.Exists(cfg))
			{
				cfg = Path.Combine(dir, "config", OldConfigName);
			}
			if (File.Exists(cfg) && Parse(cfg, where) is Bridge b)
			{
				list.Add(b);
			}
		}
		return list;
	}

	// The plugin's settings file (BepInEx .cfg: "Key = value" lines).
	public static Bridge? Parse(string path, string where) => ParseText(File.ReadAllText(path), path, where);

	public static Bridge? ParseText(string text, string path, string where)
	{
		string? Get(string key) => Regex.Match(text, $@"^\s*{key}\s*=\s*(.*?)\s*$", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
		string? token = Get("Token");
		if (string.IsNullOrEmpty(token))
		{
			return null;
		}
		return new Bridge(path, where, int.TryParse(Get("Port"), out int p) ? p : 5182, token);
	}

	// Which bridge (if any) answers on this computer, and with which world.
	public static async Task<Running?> FindRunning(IEnumerable<Bridge> bridges)
	{
		foreach (Bridge b in bridges.DistinctBy(b => (b.Port, b.Token)))
		{
			try
			{
				using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(1.5) };
				using HttpRequestMessage req = new(HttpMethod.Get, $"http://127.0.0.1:{b.Port}/status");
				req.Headers.Add("X-Bridge-Token", b.Token);
				using HttpResponseMessage res = await http.SendAsync(req);
				if (!res.IsSuccessStatusCode)
				{
					continue;
				}
				using JsonDocument doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
				string world = doc.RootElement.TryGetProperty("world", out var w) ? w.GetString() ?? "" : "";
				int players = doc.RootElement.TryGetProperty("players", out var pl) && pl.TryGetInt32(out int n) ? n : 0;
				return new Running(b, world, players);
			}
			catch
			{
			}
		}
		return null;
	}

	// For the start page: what the player has, and what to do next.
	public static async Task<object> Status(AppSettings settings)
	{
		List<Bridge> bridges = FindBridges(settings, out bool bepInEx, out bool plugin);
		Running? running = await FindRunning(bridges);
		string state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
		return new
		{
			state,
			world = running?.World,
			players = running?.Players,
			where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where,
			valheim = GameLook.FindValheim(settings.ValheimPath),
		};
	}
}
