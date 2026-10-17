namespace TerrainEditor.App;

/// <summary>
/// A world's backups: the editor's own (&lt;World&gt;_backup_terraineditor-&lt;date&gt;) and the game's
/// (&lt;World&gt;_backup_auto-&lt;date&gt;), next to the world folder. Used to restore an area from a backup.
/// </summary>
public static class Backups
{
    /// <summary>
    /// One backup of a world.
    /// </summary>
    /// <param name="Path">The backup's folder.</param>
    /// <param name="Name">The folder's name.</param>
    /// <param name="Date">The folder's last write time.</param>
    /// <param name="Kind">"game" for the game's own auto backups, "editor" for the editor's.</param>
    public sealed record Info(string Path, string Name, DateTime Date, string Kind);

    /// <summary>
    /// The backups found next to a world folder, newest first; none when the folder beside it cannot be read (said in
    /// the log).
    /// </summary>
    public static List<Info> Find(string worldDir)
    {
        // Either separator at the end ("C:/x/World1/" on Windows too).
        string trimmed = worldDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parent = Path.GetDirectoryName(trimmed);
        if (parent == null || !Directory.Exists(parent))
        {
            return [];
        }
        string prefix = Path.GetFileName(trimmed) + "_backup_";
        try
        {
            return [.. Directory.GetDirectories(parent, prefix + "*")
                .Where(Places.HasWorld)
                .Select(d => new Info(d, Path.GetFileName(d), Directory.GetLastWriteTime(d), Path.GetFileName(d)[prefix.Length..].StartsWith("auto", StringComparison.Ordinal) ? "game" : "editor"))
                .OrderByDescending(b => b.Date)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "listing a world's backups (none are shown)");
            return [];
        }
    }
}
