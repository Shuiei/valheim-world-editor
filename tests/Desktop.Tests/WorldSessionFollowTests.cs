using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Following the game from a saved world: nothing to read, and a read made for a world read again since dropped.
/// </summary>
[Collection("World files")]
public class WorldSessionFollowTests
{
    [AvaloniaFact]
    public async Task ASavedWorldHasNoGameToFollow()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            using var w = WorldSession.Open(dir);
            Assert.Null(await w.FetchGame([(0, 0)]));
            Assert.Null(await w.FetchGame([]));
            // A read of another world (the world was read again since): dropped, nothing merged.
            var other = new WorldSession.Fetched(new WorldSave { Directory = "elsewhere", SaveNumber = 1 }, [], [(0, 0)], []);
            var followed = w.MergeGame(other)!;
            Assert.Empty(followed.Ours);
            Assert.Empty(followed.Ground);
            Assert.Empty(followed.Kept);
            Assert.Empty(followed.Merged.Vanished);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
