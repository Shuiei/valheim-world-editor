using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Live mode against a stand-in for the game (FakeGame): Apply live (ground, objects, zone resets), Reload, failed applies
/// and two applies at once.
/// </summary>
[Collection("World files")]
public class LiveTests
{
    [Fact]
    public async Task ApplyLiveSendsTheGroundAndObjectsAndKeepsTheHistory()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        Assert.True(world.IsLive);
        Assert.Equal(1, game.Snapshots);
        var scene = WorldScene.Load(world, 0, 0, 1);
        var s = scene.Session!;
        Assert.True(s.IsLive);
        Raise(s);
        int tree = scene.Things.FindIndex(t => !t.Gone && !t.Piece);
        Assert.True(tree >= 0);
        s.Delete([tree]);
        Assert.Equal((1, 1, 0, 0), world.Pending);
        Assert.StartsWith("Not applied:", s.PendingText);

        var o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.False(o.Reloaded);
        Assert.Equal(new[] { (0, 0) }, game.Terrain);
        Assert.Equal((1, 0), Assert.Single(game.ObjectCalls));
        Assert.Equal((0, 0, 0, 0), world.Pending);
        Assert.Equal("All applied", s.PendingText);
        // The history stays, every change tagged as applied.
        Assert.Equal(2, s.UndoList.Count);
        Assert.All(s.UndoList, c => Assert.True(c.Applied));

        // Undone after applying: the tree is waiting to come back; applying sends it.
        s.Undo();
        Assert.NotEqual((0, 0, 0, 0), world.Pending);
        o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal(2, game.ObjectCalls.Count);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // Applying with nothing waiting sends nothing.
        o = await s.ApplyLive();
        Assert.Equal("Nothing to apply.", o.Message);
        Assert.Equal(2, game.ObjectCalls.Count);
    }

    [Fact]
    public async Task ZoneResetsAreAppliedThenTheWorldIsReadAgain()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        var scene = WorldScene.Load(world, 0, 0, 1);
        Raise(scene.Session!);
        world.Edits.SetReset(new ZoneReset(2, 3, true, false), true);
        var o = await world.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.True(o.Reloaded);
        Assert.Equal((2, 3, true, false), Assert.Single(game.Resets));
        // The game regenerates the zones: the world is read again from it.
        Assert.Equal(2, game.Snapshots);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // The history comes along: the raise, outside the reset zone, applied.
        Assert.True(Assert.Single(world.History!.Undo).Applied);
    }

    [Fact]
    public async Task ReloadDropsWhatIsNotApplied()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        var scene = WorldScene.Load(world, 0, 0, 1);
        Raise(scene.Session!);
        Assert.Equal(1, world.Pending.Zones);
        await world.Reload();
        Assert.Equal(2, game.Snapshots);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        Assert.Empty(game.Terrain);
    }

    // The game refuses the objects (an error on its side): nothing counts as applied, and the next
    // Apply live sends them again.
    [Fact]
    public async Task AFailedApplyIsSentAgainNextTime()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        var scene = WorldScene.Load(world, 0, 0, 1);
        var s = scene.Session!;
        int tree = scene.Things.FindIndex(t => !t.Gone && !t.Piece);
        s.Delete([tree]);
        game.Fail["/objects"] = 500;
        var o = await s.ApplyLive();
        Assert.False(o.Done);
        Assert.StartsWith("Could not apply live", o.Message);
        Assert.Equal(1, world.Pending.Deleted);
        Assert.All(s.UndoList, c => Assert.False(c.Applied));
        game.Fail.Clear();
        o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal((1, 0), Assert.Single(game.ObjectCalls));
        Assert.Equal(0, world.Pending.Deleted);
    }

    [Fact]
    public async Task AFailedGroundApplyIsSentAgainNextTime()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        Raise(s);
        game.Fail["/terrain"] = 500;
        var o = await s.ApplyLive();
        Assert.False(o.Done);
        Assert.Equal(1, world.Pending.Zones);
        game.Fail.Clear();
        o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal(new[] { (0, 0) }, game.Terrain);
        Assert.Equal(0, world.Pending.Zones);
    }

    // Two applies at once (a double click): what is new is sent once.
    [Fact]
    public async Task TwoAppliesAtOnceSendEachChangeOnce()
    {
        using var game = new FakeGame();
        var world = await OpenOn(game);
        var scene = WorldScene.Load(world, 0, 0, 1);
        var s = scene.Session!;
        var tree = (new NewObject(0, StableHash.Of("Beech1"), new System.Numerics.Vector3(10, 30, 10), System.Numerics.Vector3.Zero, 1), false);
        s.Commit("plant", null, [], [tree]);
        Raise(s);
        var results = await Task.WhenAll(world.ApplyLive(), world.ApplyLive());
        // The first sends everything; the second finds nothing left.
        Assert.Equal(["Applied", "Nothing to apply."], results.Select(r => r.Message.StartsWith("Applied", StringComparison.Ordinal) ? "Applied" : r.Message).Order());
        Assert.Equal((0, 1), Assert.Single(game.ObjectCalls));
        Assert.Equal(new[] { (0, 0) }, game.Terrain);
        Assert.Equal((0, 0, 0, 0), world.Pending);
    }
}
