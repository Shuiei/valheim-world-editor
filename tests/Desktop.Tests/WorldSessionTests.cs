using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Worlds opened in the app (WorldSession): a world just made from a seed (WorldCreator) opens with its objects, and
/// live zone resets the game did are not sent again when the world cannot be read back.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-session-" + Guid.NewGuid().ToString("N")[..8]);

    // The test world's seed (tests/fixtures/CITest).
    private const string Seed = "GbXfypqU3G";

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// A folder inside the temporary folder.
    /// </summary>
    private string Folder(string name) => Path.Combine(_dir, name);

    [Fact]
    public void TheNativeAppOpensIt()
    {
        WorldCreator.Create(Folder("Open"), "Open", Seed, radius: 1);
        using var world = WorldSession.Open(Folder("Open"));
        var scene = WorldScene.Load(world, 0, 0, 1);
        Assert.NotEmpty(scene.Things);
        Assert.Equal("Open", world.World.Name);
    }

    [Fact]
    public async Task ZoneResetsAreNotSentAgainWhenTheWorldCannotBeReadBack()
    {
        using var game = new FakeGame();
        using var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
        // The game resets the zone, then reading the world back fails (a timeout, a dropped tunnel).
        game.Fail["/snapshot"] = 500;
        var o = await world.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.False(o.Reloaded);
        Assert.Contains("1 zone(s) reset", o.Message);
        Assert.Contains("could not be read again", o.Message);
        Assert.Equal(0, world.Edits.ResetCount);
        // The next apply has nothing to reset: the players' building there since stays.
        await world.ApplyLive();
        Assert.Single(game.Resets);
    }
}
