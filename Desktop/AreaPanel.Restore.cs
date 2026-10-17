using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Puts the ground and the objects of the chosen kinds inside the selection back as they were in a backup (each
    /// when its box is ticked), as one undo step. Objects unchanged since then (same kind, place and facing) are left
    /// alone; runestones are not touched.
    /// </summary>
    /// <param name="session">The session the restore is committed to (one undo step).</param>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <param name="path">The backup folder; null: the one chosen on the list.</param>
    internal async Task RestoreBackup(EditSession session, List<Vector2> poly, string? path = null)
    {
        var s = session.Scene;
        path ??= SelectedBackupPath();
        if (path == null)
        {
            Say(Said.Of("area.msg.chooseBackup"));
            return;
        }
        if (await OpenBackup(path, s.World) is not var (bw, be))
        {
            Say(BackupInfo.Text == _backupSaid.Shown ? _backupSaid : Said.Raw(BackupInfo.Text ?? ""));
            return;
        }
        if (!StillOpen(session))
        {
            return;
        }
        int groundPoints = 0;
        Func<Ground, (List<int>, (int, int, int, int))>? ground = BackupGroundBox.IsChecked != true ? null : g =>
        {
            var (touched, box) = RestoreGround(session, g, be);
            groundPoints = touched.Count;
            return (touched, box);
        };
        var remove = new List<int>();
        var adds = new List<(NewObject, bool)>();
        if (BackupObjectsBox.IsChecked == true)
        {
            (remove, adds) = RestoreObjects(session, bw, poly);
        }
        if (ground == null && remove.Count == 0 && adds.Count == 0)
        {
            Say(Said.Of("area.msg.nothingRestore"));
            return;
        }
        var added = session.Commit(Lang.T("area.undo.restored"), ground, remove, adds);
        if (groundPoints == 0 && remove.Count == 0 && added.Count == 0)
        {
            Say(Said.Of("area.msg.nothingRestore"));
            return;
        }
        Say(groundPoints > 0 ? Said.Of("area.msg.restoredGround", added.Count, remove.Count) : Said.Of("area.msg.restored", added.Count, remove.Count));
    }
}
