using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    // ---- Restore from a backup (WorldEdit's //restore): the ground and objects inside the selection as
    // they were in a backup of this world. Objects unchanged since then are left alone.
    /// <summary>The backup folder chosen on the list.</summary>
    /// <returns>The folder, or null when none is chosen (or "Another folder…" is).</returns>
    private string? SelectedBackupPath() => BackupBox.SelectedIndex >= 0 && BackupBox.SelectedIndex < _backupPaths.Count ? _backupPaths[BackupBox.SelectedIndex] : null;

    /// <summary>
    /// Lists the world's backups (the editor's and the game's), then the folders chosen by hand, then
    /// "Another folder…".
    /// </summary>
    /// <param name="select">The folder to select; null keeps the one selected.</param>
    private void FillBackups(string? select = null)
    {
        var s = _view.Scene;
        string? keep = select ?? SelectedBackupPath();
        // A world without a folder (live from a game) has no backups of its own; folders chosen by hand stay.
        var found = s?.World != null && !string.IsNullOrEmpty(s.World.Directory) && Directory.Exists(s.World.Directory) ? Backups.Find(s.World.Directory) : [];
        var extra = _backupPaths.Where(p => !found.Any(f => f.Path == p)).ToList();
        _backupPaths = [.. found.Select(b => b.Path), .. extra];
        // The list keeps the selected text across a new source: with "Another folder…" still selected it
        // would ask for a folder again.
        BackupBox.SelectedIndex = -1;
        BackupBox.ItemsSource = found.Select(b => b.Kind == "game" ? Lang.T("area.backup.game", b.Date) : Lang.T("area.backup.editor", b.Date)).Concat(extra).Append(Lang.T("area.backup.other")).ToList();
        BackupBox.SelectedIndex = keep != null ? _backupPaths.IndexOf(keep) : -1;
    }

    /// <summary>Lets the backup read last go (its world is left): its world and edits are no longer kept in memory.</summary>
    internal void ForgetBackup() => _backup = null;

    /// <summary>
    /// The backup list changed: the last entry asks for another folder, which is added to the list and read. Choosing
    /// it with no folder given clears the choice.
    /// </summary>
    /// <param name="sender">The backup list.</param>
    /// <param name="e">The change.</param>
    private async void OnBackupChosen(object? sender, SelectionChangedEventArgs e)
    {
        // The last entry: another folder.
        if (BackupBox.ItemCount > 0 && BackupBox.SelectedIndex == _backupPaths.Count)
        {
            if (await PickFolder() is string path)
            {
                _backupPaths.Add(path);
                FillBackups(path);
            }
            else
            {
                BackupBox.SelectedIndex = -1;
            }
        }
    }
}
