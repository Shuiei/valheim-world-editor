using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>What <see cref="BackupInfo"/> shows, in both languages: the status bar repeats it when a backup cannot be used.</summary>
    private Said _backupSaid = Said.Empty;

    /// <summary>
    /// A backup's world (read once; the last one is kept, and only ever used for a world of its seed: a
    /// folder chosen by hand stays in the list when another world is opened).
    /// </summary>
    /// <returns>The backup and its edits, or null when it cannot be read or is another world (BackupInfo says why).</returns>
    internal async Task<(WorldSave World, EditStore Edits)?> OpenBackup(string path, WorldSave current)
    {
        if (_backup is { } b && b.Path == path && b.World.Seed == current.Seed && b.World.SeedName == current.SeedName)
        {
            return (b.World, b.Edits);
        }
        ShowBackup(Said.Of("area.backup.reading"));
        try
        {
            var w = await Task.Run(() => WorldSave.Load(path));
            if (w.Seed != current.Seed || w.SeedName != current.SeedName)
            {
                ShowBackup(Said.Of("area.backup.otherWorld", w.SeedName, current.SeedName));
                return null;
            }
            var edits = new EditStore(w);
            _backup = (path, w, edits);
            ShowBackup(Said.Of("area.backup.read", w.SaveNumber, w.Name, w.ObjectCount, Directory.GetLastWriteTime(path)));
            return (w, edits);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading the chosen backup");
            ShowBackup(Said.Of("area.backup.unreadable", ex.Message));
            return null;
        }
    }

    /// <summary>Shows a line in <see cref="BackupInfo"/>, keeping its English.</summary>
    /// <param name="said">The line.</param>
    private void ShowBackup(Said said)
    {
        _backupSaid = said;
        BackupInfo.Text = said.Shown;
    }
}
