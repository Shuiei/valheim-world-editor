using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's backups list ("Another folder…", a folder that is not a world, another world) and a restore of the backup, with a soft edge and with nothing chosen.
/// </summary>
[Collection("World files")]
public class AreaPanelBackupListTests
{
    [AvaloniaFact]
    public async Task TheBackupListOffersAnotherFolder()
    {
        using var r = new SharedAreaPanelRun();
        r.P.Choose(AreaPanel.Act.Backup);
        var items = ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ToList();
        Assert.Equal("Another folder…", items.Last());
        // Nothing chosen yet.
        await r.P.Apply();
        Assert.Equal("Choose a backup first.", r.Said);
        // Another folder, cancelled: nothing chosen.
        r.P.PickFolder = () => Task.FromResult<string?>(null);
        r.P.BackupBox.SelectedIndex = items.Count - 1;
        await TestWindows.Until(() => r.P.BackupBox.SelectedIndex == -1);
        // A folder that is not a world: said in the panel and in the message.
        string notAWorld = Path.Combine(Path.GetDirectoryName(r.Dir)!, "empty");
        Directory.CreateDirectory(notAWorld);
        int asked = 0;
        r.P.PickFolder = () => { asked++; return Task.FromResult<string?>(notAWorld); };
        r.P.BackupBox.SelectedIndex = r.P.BackupBox.ItemCount - 1;
        await TestWindows.Until(() => r.P.BackupBox.SelectedIndex >= 0 && ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().Contains(notAWorld));
        // Asked once: the new list used to keep "Another folder…" selected and ask again, forever.
        Assert.Equal(1, asked);
        Assert.Equal(notAWorld, ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ElementAt(r.P.BackupBox.SelectedIndex));
        await r.P.Apply();
        Assert.StartsWith("That folder is not a world save that can be read", r.P.BackupInfo.Text);
        Assert.Equal(r.P.BackupInfo.Text, r.Said);
    }

    [AvaloniaFact]
    public async Task ABackupOfAnotherWorldIsRefused()
    {
        using var r = new SharedAreaPanelRun();
        var other = WorldSave.Load(r.Dir);
        typeof(WorldSave).GetProperty(nameof(WorldSave.Seed))!.SetValue(other, other.Seed + 1);
        typeof(WorldSave).GetProperty(nameof(WorldSave.SeedName))!.SetValue(other, "Other");
        Assert.Null(await r.P.OpenBackup(r.Dir, other));
        Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
        // The same world is read once, then kept.
        Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
        Assert.StartsWith("Backup: save #", r.P.BackupInfo.Text);
        r.P.BackupInfo.Text = "";
        Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
        Assert.Equal("", r.P.BackupInfo.Text);
        // Kept, it is still refused for another world (the same folder chosen after opening that one).
        Assert.Null(await r.P.OpenBackup(r.Dir, other));
        Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
    }

    [AvaloniaFact]
    public async Task ARestoreWithASoftEdgeBlendsTowardsTheBackup()
    {
        using var r = new SharedAreaPanelRun();
        r.Select(10, 10, 54, 54);
        r.P.SoftSlider.Value = 10;
        float was = r.H(32, 32), wasEdge = r.H(12, 32), wasOut = r.H(6, 32);
        // Raised and painted everywhere, saved: the backup is the world before.
        r.S.Shape(32, 32, Formula.Compile("3", []), 40, 0, "x");
        DirtPaint.Set(r.S, true);
        // The game's backup from before, then saved.
        TempWorld.CopyDir(r.Dir, r.Dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000");
        Assert.True(r.S.Save().Saved);
        var backup = Assert.Single(Backups.Find(r.Dir));
        r.ChooseNoKinds();
        await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, backup.Path);
        Assert.StartsWith("Restored from the backup: the ground", r.Said);
        // The middle as it was; the soft edge part way; outside the selection, still raised.
        Assert.Equal(was, r.H(32, 32), 2);
        float edge = r.H(12, 32);
        Assert.InRange(edge, wasEdge + 0.05f, wasEdge + 2.95f);
        Assert.Equal(wasOut + 3, r.H(6, 32), 2);
        int mid = 32 * r.S.Ground.W + 32;
        Assert.Equal(0, r.S.Ground.PMod[mid]);
    }

    [AvaloniaFact]
    public async Task ARestoreOfNeitherGroundNorObjectsDoesNothing()
    {
        using var r = new SharedAreaPanelRun();
        r.P.BackupGroundBox.IsChecked = false;
        r.P.BackupObjectsBox.IsChecked = false;
        await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, r.Dir);
        Assert.Equal("Nothing to restore: the selection is as it was in the backup.", r.Said);
        Assert.False(r.S.CanUndo);
    }
}
