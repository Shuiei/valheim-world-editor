using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// After a save that needed the world read again (ReadBackSave): the save read back becomes the session's world, and when
/// none was handed over the world is read from its folder again.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionReadBackTests : WorldsFolder
{
    // The test world's seed (tests/fixtures/CITest).
    private const string Seed = "GbXfypqU3G";

    /// <summary>
    /// A test's own folder for its worlds.
    /// </summary>
    public WorldSessionReadBackTests() : base("vwe-readback-")
    {
    }

    [Fact]
    public void TheSaveReadBackBecomesTheWorld()
    {
        string folder = Path.Combine(Dir, "Given");
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
        string folder = Path.Combine(Dir, "Again");
        WorldCreator.Create(folder, "Again", Seed, radius: 1);
        using var world = WorldSession.Open(folder);
        var before = world.World;
        var unread = world.ReadBackSave(new WorldWriter.Result(true, "saved", null, 0, 0, []));
        Assert.True(unread == Said.Empty);
        Assert.NotSame(before, world.World);
        Assert.Equal(folder, world.World.Directory);
    }

    // The save was written, but the world's folder is gone before it is read back (a sync tool, the game deleting the
    // world): said, the session starts over from the world as it was, and that save counts as its own, not as one the
    // game made since.
    [Fact]
    public void ASaveThatCannotBeReadBackIsSaidAndTheSessionStartsOver()
    {
        string folder = Path.Combine(Dir, "Gone");
        WorldCreator.Create(folder, "Gone", Seed, radius: 1);
        using var world = WorldSession.Open(folder);
        var before = world.World;
        Directory.Delete(folder, true);

        var unread = world.ReadBackSave(new WorldWriter.Result(true, "saved", null, 0, 0, []) { Number = before.SaveNumber + 1 });

        Assert.NotEqual(Said.Empty, unread);
        Assert.Contains("could not be read", unread.English);
        Assert.Same(before, world.World);
        Assert.Null(world.GameSaveSince());
    }
}
