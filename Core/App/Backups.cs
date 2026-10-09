namespace TerrainEditor.App;

// A world's backups: the editor's own (<World>_backup_terraineditor-<date>) and the game's
// (<World>_backup_auto-<date>), next to the world folder. Used to restore an area from a backup.
public static class Backups
{
	public sealed record Info(string Path, string Name, DateTime Date, string Kind);

	// The backups found next to a world folder, newest first.
	public static List<Info> Find(string worldDir)
	{
		string trimmed = worldDir.TrimEnd(System.IO.Path.DirectorySeparatorChar);
		string? parent = System.IO.Path.GetDirectoryName(trimmed);
		if (parent == null || !Directory.Exists(parent))
		{
			return new();
		}
		string prefix = System.IO.Path.GetFileName(trimmed) + "_backup_";
		return Directory.GetDirectories(parent, prefix + "*")
			.Where(Places.HasWorld)
			.Select(d => new Info(d, System.IO.Path.GetFileName(d), Directory.GetLastWriteTime(d), System.IO.Path.GetFileName(d)[prefix.Length..].StartsWith("auto", StringComparison.Ordinal) ? "game" : "editor"))
			.OrderByDescending(b => b.Date).ToList();
	}
}
