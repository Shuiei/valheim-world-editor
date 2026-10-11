namespace TerrainEditor.App;

// A world's backups: the editor's own (<World>_backup_terraineditor-<date>) and the game's
// (<World>_backup_auto-<date>), next to the world folder. Used to restore an area from a backup.
public static class Backups
{
    public sealed record Info(string Path, string Name, DateTime Date, string Kind);

    // The backups found next to a world folder, newest first.
    public static List<Info> Find(string worldDir)
    {
        string trimmed = worldDir.TrimEnd(Path.DirectorySeparatorChar);
        string? parent = Path.GetDirectoryName(trimmed);
        if (parent == null || !Directory.Exists(parent))
        {
            return [];
        }
        string prefix = Path.GetFileName(trimmed) + "_backup_";
        return [.. Directory.GetDirectories(parent, prefix + "*")
            .Where(Places.HasWorld)
            .Select(d => new Info(d, Path.GetFileName(d), Directory.GetLastWriteTime(d), Path.GetFileName(d)[prefix.Length..].StartsWith("auto", StringComparison.Ordinal) ? "game" : "editor"))
            .OrderByDescending(b => b.Date)];
    }
}
