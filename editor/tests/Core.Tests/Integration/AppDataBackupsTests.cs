using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Backups listed next to a world, newest first, and the folders that are not backups.
/// </summary>
[Collection("World files")]
public class AppDataBackupsTests
{
    [Fact]
    public void BackupsNextToAWorldAreListedNewestFirst()
    {
        using var w = new TempWorld();
        string parent = Path.GetDirectoryName(w.Dir)!;
        string editor = Path.Combine(parent, "CITest_backup_terraineditor-20261001-100000");
        string game = Path.Combine(parent, "CITest_backup_auto-20261002100000");
        TempWorld.CopyDir(w.Dir, editor);
        TempWorld.CopyDir(w.Dir, game);
        Directory.SetLastWriteTime(editor, new DateTime(2026, 10, 1));
        Directory.SetLastWriteTime(game, new DateTime(2026, 10, 2));
        // Not backups: no save in it, or another world's.
        Directory.CreateDirectory(Path.Combine(parent, "CITest_backup_empty"));
        TempWorld.CopyDir(w.Dir, Path.Combine(parent, "Other_backup_auto-1"));
        var found = Backups.Find(w.Dir + Path.DirectorySeparatorChar);
        Assert.Equal(new[] { ("game", game), ("editor", editor) }, found.Select(b => (b.Kind, b.Path)).ToArray());
        Assert.Empty(Backups.Find(Path.Combine(parent, "nowhere", "World")));
        Assert.Empty(Backups.Find("/"));
    }
}
