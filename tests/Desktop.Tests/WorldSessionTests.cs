using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Worlds opened in the app (WorldSession): a world just made from a seed (WorldCreator) opens with its objects, and a
/// live world that cannot be opened is disconnected, and an apply that is left during a game save still gives its outcome.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionTests : WorldsFolder
{
    // The test world's seed (tests/fixtures/CITest).
    private const string Seed = "GbXfypqU3G";

    /// <summary>
    /// A test's own folder for its worlds.
    /// </summary>
    public WorldSessionTests() : base("vwe-session-")
    {
    }

    /// <summary>
    /// A folder inside the temporary folder.
    /// </summary>
    private string Folder(string name) => Path.Combine(Dir, name);

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
    public async Task AWorldThatCannotBeOpenedLiveClosesItsConnection()
    {
        using var game = new FakeGame();
        game.Fail["/snapshot"] = 500;
        var live = new LiveBridge(game.Url, game.Token);
        await Assert.ThrowsAnyAsync<Exception>(() => WorldSession.OpenLive(live, FakeGame.Label()));
        await Assert.ThrowsAsync<ObjectDisposedException>(live.LoadWorld);
    }

    [Fact]
    public async Task LeavingDuringAnApplyStillGivesItsOutcome()
    {
        using var game = new FakeGame();
        var world = await OpenLive(game);
        var s = RaisedZone(world, 32, 32);
        game.HoldTerrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = world.ApplyLive();
        Assert.NotSame(applying, await Task.WhenAny(game.TerrainArrived.Task, applying));
        // Left while the game holds the ground: the apply ends with an outcome, not a disposed lock.
        world.Dispose();
        game.HoldTerrain.SetResult();
        Assert.NotNull(await applying);
    }

    [Fact]
    public void ADiscardThatCannotReadTheWorldKeepsItsSavesKnown()
    {
        WorldCreator.Create(Folder("Discard"), "Discard", Seed, radius: 1);
        using var world = WorldSession.Open(Folder("Discard"));
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        Assert.True(world.Save().Done);
        // The folder gone for a moment: Discard fails, and the session's own save is still its own.
        Directory.Move(Folder("Discard"), Folder("Away"));
        Assert.ThrowsAny<IOException>(world.Discard);
        Directory.Move(Folder("Away"), Folder("Discard"));
        s.Shape(20, 20, Formula.Compile("2", []), 3, 0, "raise");
        var o = world.Save();
        Assert.True(o.Done, o.Message);
    }
}
