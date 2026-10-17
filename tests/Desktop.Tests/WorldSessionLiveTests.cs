using TerrainEditor.App;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Applying a live world (WorldSession): zone resets sent once, a partial apply saying what went in, the ground's
/// steps counted as sent or not, and the No limit ground kept or turned into ground discs.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionLiveTests
{
    [Fact]
    public async Task ZoneResetsAreNotSentAgainWhenTheWorldCannotBeReadBack()
    {
        using var game = new FakeGame();
        using var world = await OpenLive(game);
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

    // The ground goes in, then the game refuses the objects: applied, said with the error, and only the steps the
    // game has all of are applied.
    [Fact]
    public async Task APartialApplyLiveSaysWhatWentIn()
    {
        using var game = new FakeGame();
        using var world = await OpenLive(game);
        var s = Zone(world);
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        DeleteFirstObject(s);
        game.Fail["/objects"] = 500;
        var o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal(WorldSession.LiveParts.Objects, o.Unsent);
        Assert.Contains("zone(s) of ground", o.Message);
        Assert.Contains("but then it failed", o.Message);
        Assert.Equal(0, world.Edits.ChangedZoneCount);
        Assert.Equal([true, false], s.UndoList.Select(c => c.Applied));
    }

    // A zone reset marked and then unmarked has nothing left to send, while another zone's reset still waits: after a
    // partial Apply live the first zone's steps are applied, and so is the ground after them that went in.
    [Fact]
    public async Task AResetUnmarkedSinceIsNotCountedAsUnsent()
    {
        using var game = new FakeGame();
        using var world = await OpenLive(game);
        var s = Zone(world);
        s.MarkResets([(0, 0)], false, false, "Reset");
        s.UnmarkResets([(0, 0)], "Keep");
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        s.MarkResets([(1, 0)], false, false, "Reset");
        DeleteFirstObject(s);
        game.Fail["/objects"] = 500;
        var o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal(WorldSession.LiveParts.Objects | WorldSession.LiveParts.Resets, o.Unsent);
        Assert.Equal([(1, 0)], o.UnsentResets);
        Assert.Equal([true, true, true, false, false], s.UndoList.Select(c => c.Applied));
    }

    // Ground discs made from No limit ground go with the objects: while they are not in the game, no step counts as
    // sent (none says which made them), even one that only changed the ground.
    [Fact]
    public void WithTheGroundDiscsUnsentNoStepIsSent()
    {
        var ground = new EditSession.Change("raise", [], new Ground.State([], [], [], [], [], []), new Ground.State([], [], [], [], [], []), [(0, 0)]);
        Assert.True(new WorldSession.Outcome(true, Said.Empty, false) { Unsent = WorldSession.LiveParts.Objects }.Sent(ground));
        Assert.False(new WorldSession.Outcome(true, Said.Empty, false) { Unsent = WorldSession.LiveParts.Objects, Lifted = true }.Sent(ground));
        Assert.True(new WorldSession.Outcome(true, Said.Empty, false) { Unsent = WorldSession.LiveParts.Resets, Lifted = true }.Sent(ground));
    }

    [Fact]
    public async Task NoLimitGroundStaysAsItWasWhenNothingWasApplied()
    {
        using var game = new FakeGame();
        using var world = await OpenLive(game);
        LiftZone(world, 20f);
        var modifiers = world.Modifiers;
        int added = world.Edits.AddedCount, deleted = world.Edits.DeletedCount;
        // The game turns the ground down before reading it (no world loaded): nothing went in, so the lift is not
        // turned into ground discs.
        game.Fail["/terrain"] = 503;
        var o = await world.ApplyLive();
        Assert.False(o.Done, o.Message);
        Assert.False(o.Lifted);
        Assert.True(world.Edits.Get(0, 0)!.HasLift);
        Assert.Equal(added, world.Edits.AddedCount);
        Assert.Equal(deleted, world.Edits.DeletedCount);
        Assert.Same(modifiers, world.Modifiers);
        // Once the game answers, the lift goes in as ground discs.
        game.Fail.Remove("/terrain");
        o = await world.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.True(o.Lifted);
        Assert.False(world.Edits.Get(0, 0)!.HasLift);
    }

    [Fact]
    public async Task NoLimitGroundStaysAsDiscsWhenTheGameMayHaveTakenTheGround()
    {
        using var game = new FakeGame();
        using var world = await OpenLive(game);
        LiftZone(world, 20f);
        // The plugin failed while setting the ground (500): some zones may be in the game, so the discs stay and the
        // area is read again (Lifted); the next Apply live sends the ground again.
        game.Fail["/terrain"] = 500;
        var o = await world.ApplyLive();
        Assert.False(o.Done, o.Message);
        Assert.True(o.Lifted);
        Assert.False(world.Edits.Get(0, 0)!.HasLift);
        game.Fail.Remove("/terrain");
        int sent = game.Terrain.Count;
        o = await world.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.True(game.Terrain.Count > sent);
    }
}
