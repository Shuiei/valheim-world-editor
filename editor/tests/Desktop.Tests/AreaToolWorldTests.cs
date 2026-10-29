using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Regrow and Restore from a backup, on a copy of the test world.
/// </summary>
[Collection("World files")]
public class AreaToolWorldTests
{
    /// <summary>
    /// A window on zone 0, 0 of a copy of the test world, with the Area tool selecting the whole zone.
    /// </summary>
    private static (MainWindow W, EditSession S, string Dir) Open()
    {
        var (w, s, dir) = TestWindows.AreaToolOnWorldCopy();
        // The whole zone (its outer line stays: it is locked).
        w.View.Area.Points.Add(new Vector2(2, 2));
        w.View.Area.Points.Add(new Vector2(62, 62));
        w.View.Area.Notify();
        return (w, s, dir);
    }

    /// <summary>
    /// Deletes the copy of the test world (its temporary parent folder).
    /// </summary>
    private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

    // Saving offline is like Apply live: the area and its history stay, the steps saved; an undo is
    // then pending, and saved by the next save. The save the world was opened from stays until the world
    // is left, then only the latest. No backup folder.
    [AvaloniaFact]
    public void SavingKeepsTheHistoryAndAnUndoIsSavedToo()
    {
        var (w, s, dir) = Open();
        try
        {
            float H(int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
            float was = H(32, 32);
            int tree = s.Scene.Things.FindIndex(t => !t.Piece);
            s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "x");
            s.Delete([tree]);
            Assert.True(s.Save().Saved);
            Assert.Equal((0, 0, 0, 0), s.Pending);
            Assert.True(s.CanUndo);
            Assert.Equal(0, s.UnappliedSteps);
            Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(dir)!, "*_backup_*"));
            // The tree back (undo): pending (the next save puts it back), then saved.
            s.Undo();
            Assert.Equal((0, 0, 1, 0), s.Pending);
            Assert.True(s.Save().Saved);
            Assert.Equal((0, 0, 0, 0), s.Pending);
            var saved = WorldSave.Load(dir);
            Assert.Contains(saved.Objects, o => o.Prefab == s.Scene.Things[tree].Prefab && Vector3.Distance(o.Position, s.Scene.Things[tree].Position) < 0.01f);
            // The raise back too.
            s.Undo();
            Assert.Equal(1, s.Pending.Zones);
            Assert.True(s.Save().Saved);
            Assert.Equal(was, H(32, 32), 2);
            // Two saves in the folder (the one it was opened from, the latest); one once left.
            Assert.Equal(2, Directory.GetFiles(dir, "_main.*.ok").Length);
            s.Scene.Owner!.Dispose();
            Assert.Single(Directory.GetFiles(dir, "_main.*.ok"));
        }
        finally
        {
            Done(dir);
        }
    }

    [AvaloniaFact]
    public async Task RegrowPutsBackWhatTheGameGrows()
    {
        var (w, s, dir) = Open();
        try
        {
            var p = w.AreaPanel;
            foreach (var k in p.KindButtons.Keys)
            {
                p.KindButtons[k].IsChecked = k == ObjectKind.Trees;
            }
            p.Choose(AreaPanel.Act.Remove);
            await p.Apply();
            int removed = s.Pending.Deleted;
            Assert.True(removed > 0, "the test world has trees in zone 0, 0");
            p.Choose(AreaPanel.Act.Regrow);
            await p.Apply();
            Assert.True(s.Pending.Added > 0, w.MessageText.Text);
            Assert.StartsWith("Regrew", w.MessageText.Text);
            // Only trees, and only inside.
            var added = s.Scene.Things.Where(t => t.Id < 0 && !t.Gone).ToList();
            Assert.All(added, t => Assert.Equal(ObjectKind.Trees, ObjectKinds.Of(Terrain.PrefabCatalog.DisplayName(t.Prefab), false)));
            Assert.All(added, t => Assert.InRange(t.Position.X, -30, 30));
            // Again: everything is standing now, so nothing is doubled.
            int before = s.Pending.Added;
            await p.Apply();
            Assert.Equal(before, s.Pending.Added);
        }
        finally
        {
            Done(dir);
        }
    }

    [AvaloniaFact]
    public async Task RestoreFromABackup()
    {
        var (w, s, dir) = Open();
        try
        {
            var p = w.AreaPanel;
            float H(int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
            float was = H(32, 32);
            int tree = s.Scene.Things.FindIndex(t => !t.Piece);
            var gone = s.Scene.Things[tree];
            // A backup of how it was (the game's), then the world changed and saved.
            TempWorld.CopyDir(dir, dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000");
            s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "x");
            s.Delete([tree]);
            Assert.True(s.Save().Saved);
            var backup = Assert.Single(Backups.Find(dir));
            Assert.Equal("game", backup.Kind);
            Assert.Equal(was + 3, H(32, 32), 2);

            foreach (var k in p.KindButtons.Keys)
            {
                p.KindButtons[k].IsChecked = true;
            }
            p.Choose(AreaPanel.Act.Backup);
            await p.RestoreBackup(s, w.View.Area.Polygon()!, backup.Path);
            Assert.Equal(was, H(32, 32), 2);
            Assert.Contains(s.Scene.Things, t => !t.Gone && t.Prefab == gone.Prefab && Vector3.Distance(t.Position, gone.Position) < 0.01f);
            Assert.Equal(1, s.Pending.Added);
            Assert.Equal(0, s.Pending.Deleted);
            Assert.StartsWith("Restored from the backup: the ground, 1 object(s) brought back, 0 removed.", w.MessageText.Text);
            // A second restore finds nothing to do.
            await p.RestoreBackup(s, w.View.Area.Polygon()!, backup.Path);
            Assert.StartsWith("Nothing to restore", w.MessageText.Text);
        }
        finally
        {
            Done(dir);
        }
    }
}
