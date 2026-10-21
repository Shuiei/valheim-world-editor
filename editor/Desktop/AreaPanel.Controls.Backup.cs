using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>Restore from a backup: the backup, from <c>_backupPaths</c>, then "Another folder…".</summary>
    internal ComboBox BackupBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>Restore from a backup: put the ground back.</summary>
    internal CheckBox BackupGroundBox { get; } = new() { Content = Lang.T("area.label.backupGround"), IsChecked = true, FontSize = 12 };

    /// <summary>Restore from a backup: put the objects of the chosen kinds back.</summary>
    internal CheckBox BackupObjectsBox { get; } = new() { Content = Lang.T("area.label.backupObjects"), IsChecked = true, FontSize = 12 };

    /// <summary>Restore from a backup: what it does, then the backup read or why it cannot be used.</summary>
    internal TextBlock BackupInfo { get; } = new()
    {
        FontSize = 11,
        Foreground = Ui.Muted,
        TextWrapping = TextWrapping.Wrap,
        Text = Lang.T("area.hint.backup"),
    };

    /// <summary>The backup folders behind <see cref="BackupBox"/>'s entries, in its order.</summary>
    private List<string> _backupPaths = [];

    /// <summary>Asks for another backup folder (the window's folder picker).</summary>
    internal Func<Task<string?>> PickFolder { get; set; } = () => Task.FromResult<string?>(null);

    /// <summary>The last backup read by <see cref="OpenBackup"/>, kept for the next restore from it while its world is shown.</summary>
    private (string Path, WorldSave World, EditStore Edits)? _backup;
}
