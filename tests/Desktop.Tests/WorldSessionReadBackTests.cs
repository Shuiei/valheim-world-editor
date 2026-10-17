using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// After a save that needed the world read again (ReadBackSave): the save read back becomes the session's world, and when
/// none was handed over the world is read from its folder again.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionReadBackTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-readback-" + Guid.NewGuid().ToString("N")[..8]);

    // The test world's seed (tests/fixtures/CITest).
    private const string Seed = "GbXfypqU3G";

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void TheSaveReadBackBecomesTheWorld()
    {
        string folder = Path.Combine(_dir, "Given");
        WorldCreator.Create(folder, "Given", Seed, radius: 1);
        using var world = WorldSession.Open(folder);
        var read = WorldSave.Load(folder);
        var unread = world.ReadBackSave(new WorldWriter.Result(true, "saved", null, 0, 0, []) { Read = read });
        Assert.True(unread == Said.Empty);
        Assert.Same(read, world.World);
    }

    [Fact]
    public void WithoutASaveReadBackTheWorldIsReadFromItsFolderAgain()
    {
        string folder = Path.Combine(_dir, "Again");
        WorldCreator.Create(folder, "Again", Seed, radius: 1);
        using var world = WorldSession.Open(folder);
        var before = world.World;
        var unread = world.ReadBackSave(new WorldWriter.Result(true, "saved", null, 0, 0, []));
        Assert.True(unread == Said.Empty);
        Assert.NotSame(before, world.World);
        Assert.Equal(folder, world.World.Directory);
    }
}
