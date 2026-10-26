using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Restoring an area from a backup: objects come back with all their data.
/// </summary>
public class BackupsTests
{
    [Fact]
    public void TheBackupsNextToAWorldAreFound()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        // A backup the editor made before (older versions made one at each save).
        string copy = w.Dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_terraineditor-20261001-100000";
        TempWorld.CopyDir(w.Dir, copy);
        var found = TerrainEditor.App.Backups.Find(w.Dir);
        var b = Assert.Single(found);
        Assert.Equal(copy, b.Path);
        Assert.Equal("editor", b.Kind);
    }

    // The folder beside the world cannot be read: no backups (it threw).
    [Fact]
    public void AFolderThatCannotBeReadHasNoBackups()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string parent = Path.Combine(Path.GetTempPath(), "vwe-backups-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(parent, "World1"));
        File.SetUnixFileMode(parent, UnixFileMode.UserWrite);
        try
        {
            Assert.Empty(TerrainEditor.App.Backups.Find(Path.Combine(parent, "World1") + "/"));
        }
        finally
        {
            File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void ADeletedObjectComesBackFromTheBackupWithItsData()
    {
        using var w = new TempWorld();
        var (world, (id, prefab, position, rotation, scale)) = TestWorldTree.Open(w);
        var edits = new EditStore(world);
        byte[] original = world.ObjectBytes(id);
        edits.SetDeleted([id], true);
        string copy = w.Dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000";
        TempWorld.CopyDir(w.Dir, copy);
        var r = WorldWriter.Save(world, [], edits.Deleted);
        Assert.True(r.Saved);

        WorldSave now = WorldSave.Load(w.Dir), backup = WorldSave.Load(copy);
        Assert.Equal(world.ObjectCount - 1, now.ObjectCount);
        var (inBackupId, _, _, _, _) = backup.Objects.Single(o => o.Prefab == prefab && o.Position == position);
        byte[] raw = backup.ObjectBytes(inBackupId);
        Assert.Equal(original, raw);
        var e2 = new EditStore(now);
        var z = ZdoData.Parse(raw);
        e2.AddObjects([new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, null, false, raw)]);
        Assert.True(WorldWriter.Save(now, [], null, e2.Added).Saved);
        var after = WorldSave.Load(w.Dir);
        Assert.Equal(world.ObjectCount, after.ObjectCount);
        int back = after.Objects.Single(o => o.Prefab == prefab && o.Position == position).Id;
        Assert.Equal(original, after.ObjectBytes(back));
    }
}
