using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The newest save of a world folder: the save with the highest number among its _main.N.chunks files, with that
/// file's write time; files that do not have that name are not saves.
/// </summary>
public sealed class WorldsLatestSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-latest-" + Guid.NewGuid().ToString("N")[..8]);

    public WorldsLatestSaveTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Writes a file in the test folder with the given write time, and returns its path.</summary>
    private string Save(string name, DateTime written)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, "");
        File.SetLastWriteTime(path, written);
        return path;
    }

    [Fact]
    public void AFolderWithoutSavesHasNoSaveNumberAndNoTime()
    {
        Assert.Equal((0, DateTime.MinValue), Worlds.LatestSave(_dir));
    }

    [Fact]
    public void TheHighestNumberedSaveIsTheLatestWithItsWriteTime()
    {
        Save("_main.2.chunks", new DateTime(2026, 1, 1, 10, 0, 0));
        string newest = Save("_main.10.chunks", new DateTime(2026, 3, 5, 8, 30, 0));
        Save("_main.9.chunks", new DateTime(2026, 6, 1, 9, 0, 0));
        Assert.Equal((10, File.GetLastWriteTime(newest)), Worlds.LatestSave(_dir));
    }

    [Fact]
    public void FilesThatAreNotSavesAreNotCounted()
    {
        Save("_main.chunks", DateTime.Now);
        Save("_main.x.chunks", DateTime.Now);
        Save("main.7.chunks", DateTime.Now);
        Save("_main.5.txt", DateTime.Now);
        Assert.Equal((0, DateTime.MinValue), Worlds.LatestSave(_dir));
    }
}
