using System.Text.Json;

namespace TerrainEditor.App;

// What the app remembers between runs, per user: settings.json in the app's data folder
// (~/.local/share/ValheimWorldEditor on Linux, %LOCALAPPDATA%\ValheimWorldEditor on Windows).
public sealed class AppSettings
{
	// The Valheim game folder chosen in Settings (null: found automatically in the Steam libraries).
	public string? ValheimPath { get; set; }

	// Extra places to look for the game's BepInEx: a BepInEx folder, a mod manager profile, or a
	// folder of profiles (a mod manager with a custom data folder).
	public List<string> BepInExFolders { get; set; } = new();

	// Extra places with worlds, always listed: a world folder, or a folder of worlds (a server's
	// -savedir/worlds_local, a backup folder...).
	public List<string> WorldFolders { get; set; } = new();

	public List<RecentWorld> Recent { get; set; } = new();

	public string? LiveUrl { get; set; }

	// Which start-page choice was used last ("game", "server", "offline").
	public string? LastMode { get; set; }

	public sealed record RecentWorld(string Path, string Name, DateTime Opened);

	// The data folder: settings, servers, blueprints, the log (DataDirOverride: another folder, for
	// the tests' editor). The copied game files stay in the user's (UserDataDir), read only by tests.
	public static string DataDir
	{
		get
		{
			if (DataDirOverride is string o)
			{
				Directory.CreateDirectory(o);
				return o;
			}
			return UserDataDir;
		}
	}

	public static string? DataDirOverride { get; set; }

	public static string UserDataDir
	{
		get
		{
			string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (string.IsNullOrEmpty(root))
			{
				root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
			}
			string dir = Path.Combine(root, "ValheimWorldEditor");
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	// Tests: another file, so they never touch the user's settings.
	public static string? PathOverride { get; set; }

	private static string FilePath => PathOverride ?? Path.Combine(DataDir, "settings.json");

	private static readonly object Lock = new();

	public static AppSettings Load()
	{
		lock (Lock)
		{
			try
			{
				return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings() : new AppSettings();
			}
			catch
			{
				return new AppSettings();
			}
		}
	}

	public void Save()
	{
		lock (Lock)
		{
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		}
	}

	public void AddRecent(string path, string name)
	{
		Recent.RemoveAll(r => string.Equals(Path.GetFullPath(r.Path), Path.GetFullPath(path), StringComparison.Ordinal));
		Recent.Insert(0, new RecentWorld(path, name, DateTime.Now));
		if (Recent.Count > 12)
		{
			Recent.RemoveRange(12, Recent.Count - 12);
		}
		Save();
	}
}
