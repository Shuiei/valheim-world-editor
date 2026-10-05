using System.Text.Json;

namespace TerrainEditor.App;

// What the app remembers between runs, per user: settings.json in the app's data folder
// (~/.local/share/ValheimWorldEditor on Linux, %LOCALAPPDATA%\ValheimWorldEditor on Windows).
public sealed class AppSettings
{
	public string? ValheimPath { get; set; }

	public List<RecentWorld> Recent { get; set; } = new();

	public string? LiveUrl { get; set; }

	// Which start-page choice was used last ("game", "server", "offline").
	public string? LastMode { get; set; }

	public sealed record RecentWorld(string Path, string Name, DateTime Opened);

	public static string DataDir
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

	private static string FilePath => Path.Combine(DataDir, "settings.json");

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
