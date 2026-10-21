using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a new world that failed had written is taken away again (WorldCreator.Unwrite): its files, and the folder when
/// the creation made it and nothing else is in it; a folder that was there, or holds something else, stays; a file that
/// cannot be taken away is said in the log and the rest is left.
/// </summary>
public sealed class WorldCreatorUnwriteTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-unwrite-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Deletes whatever the test left.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private static void Unwrite(string folder, List<string> written, bool madeFolder) =>
        PrivateMember.Call(typeof(WorldCreator), "Unwrite", null, folder, written, madeFolder);

    [Fact]
    public void TheFilesAndTheFolderItMadeAreTakenAway()
    {
        Directory.CreateDirectory(_dir);
        string a = Path.Combine(_dir, "_main.1.fwl2"), b = Path.Combine(_dir, "_main.1.chunks");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");

        // A file named twice, and one never written: both fine.
        Unwrite(_dir, [a, b, a, Path.Combine(_dir, "_main.1.ok")], madeFolder: true);

        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void AFolderThatWasThereOrHoldsSomethingElseStays()
    {
        Directory.CreateDirectory(_dir);
        string a = Path.Combine(_dir, "_main.1.fwl2");
        File.WriteAllText(a, "a");
        Unwrite(_dir, [a], madeFolder: false);
        Assert.True(Directory.Exists(_dir));
        Assert.False(File.Exists(a));

        string other = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(other, "mine");
        File.WriteAllText(a, "a");
        Unwrite(_dir, [a], madeFolder: true);
        Assert.True(File.Exists(other));
        Assert.False(File.Exists(a));
    }

    [Fact]
    public void AFileThatCannotBeTakenAwayIsSaidAndTheRestLeft()
    {
        Directory.CreateDirectory(_dir);
        // A folder where the file should be: deleting it as a file is refused.
        string folderInstead = Path.Combine(_dir, "_main.1.db2");
        Directory.CreateDirectory(folderInstead);
        string after = Path.Combine(_dir, "_main.1.ok");
        File.WriteAllText(after, "ok");
        using var log = Log.Listen();

        Unwrite(_dir, [folderInstead, after], madeFolder: true);

        Assert.True(Directory.Exists(folderInstead));
        Assert.True(File.Exists(after));
        Assert.Contains(log.Lines, l => l.Contains("taking away what a new world that failed had written", StringComparison.Ordinal));
    }
}
