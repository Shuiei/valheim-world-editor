using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The worlds on this computer: recent ones, folders added in Settings, and Valheim's own world folders.
public static class Worlds
{
	public sealed record Info(string Name, string Path, DateTime Saved, int SaveNumber, string Where, bool Usable, string? Problem);

	public static List<Info> Find(AppSettings settings)
	{
		var list = new List<Info>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		void Add(string dir, string where)
		{
			try
			{
				string full = Path.GetFullPath(dir);
				if (!Directory.Exists(full) || !seen.Add(full))
				{
					return;
				}
				string? problem = Check(full);
				(int n, DateTime t) = LatestSave(full);
				list.Add(new Info(WorldName(full), full, t, n, where, problem == null, problem));
			}
			catch
			{
			}
		}
		foreach (var r in settings.Recent)
		{
			Add(r.Path, "recent");
		}
		// Folders added in Settings: a world, or a folder of worlds.
		foreach (string folder in settings.WorldFolders.Where(Directory.Exists))
		{
			if (Directory.GetFiles(folder, "_main.*.chunks").Length > 0)
			{
				Add(folder, "yours");
				continue;
			}
			foreach (string d in Directory.GetDirectories(folder).Where(d => Directory.GetFiles(d, "_main.*.chunks").Length > 0))
			{
				Add(d, "yours");
			}
		}
		foreach (string root in Places.WorldRoots())
		{
			if (!Directory.Exists(root))
			{
				continue;
			}
			foreach (string d in Directory.GetDirectories(root))
			{
				if (Directory.GetFiles(d, "_main.*.chunks").Length > 0)
				{
					Add(d, "local");
				}
			}
			// Old single-file worlds (<World>.db): the game converts them when it next loads them.
			foreach (string db in Directory.GetFiles(root, "*.db"))
			{
				string name = Path.GetFileNameWithoutExtension(db);
				if (!Directory.Exists(Path.Combine(root, name)) && seen.Add(db))
				{
					list.Add(new Info(name, db, File.GetLastWriteTime(db), 0, "local", false, "Old save format: load this world in Valheim once, then it can be edited here."));
				}
			}
		}
		return list.OrderByDescending(w => w.Where == "recent").ThenByDescending(w => w.Saved).ToList();
	}

	// Why a folder cannot be opened as a world, or null when it can.
	public static string? Check(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "Enter the world's folder.";
		}
		if (File.Exists(path) && path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
		{
			return "That is an old single-file world: load it in Valheim once, then open its folder here.";
		}
		if (!Directory.Exists(path))
		{
			return "That folder does not exist.";
		}
		// Folders that cannot be read (another user's, the system's) hold no world.
		static bool HasWorld(string dir)
		{
			try
			{
				return Directory.GetFiles(dir, "_main.*.chunks").Length > 0;
			}
			catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
			{
				return false;
			}
		}
		if (HasWorld(path))
		{
			return null;
		}
		try
		{
			// A worlds_local folder with one world in it: point at the world instead.
			string[] inside = Directory.GetDirectories(path).Where(HasWorld).ToArray();
			return inside.Length > 0
				? $"That folder holds worlds; pick one of them ({string.Join(", ", inside.Select(System.IO.Path.GetFileName).Take(4))})."
				: "No Valheim world in that folder (a world folder holds _main.<n>.chunks and *.chunk files).";
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
		{
			return $"That folder cannot be read: {ex.Message}";
		}
	}

	public static string WorldName(string path)
	{
		// The name is in _main.<n>.fwl2; the folder is named after the world anyway.
		return Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
	}

	private static (int, DateTime) LatestSave(string dir)
	{
		int best = 0;
		DateTime t = DateTime.MinValue;
		foreach (string f in Directory.GetFiles(dir, "_main.*.chunks"))
		{
			Match m = Regex.Match(Path.GetFileName(f), @"_main\.(\d+)\.chunks");
			if (m.Success && int.Parse(m.Groups[1].Value) >= best)
			{
				best = int.Parse(m.Groups[1].Value);
				t = File.GetLastWriteTime(f);
			}
		}
		return (best, t);
	}
}
