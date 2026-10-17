using TerrainEditor.App;
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
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.

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
        var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
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

    // The ground goes in, then the game refuses the objects: applied, said with the error, and only the steps the
    // game has all of are applied.
    [Fact]
    public async Task APartialApplyLiveSaysWhatWentIn()
    {
        using var game = new FakeGame();
        using var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        s.Delete([s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone)]);
        game.Fail["/objects"] = 500;
        var o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Equal(WorldSession.LiveParts.Objects, o.Unsent);
        Assert.Contains("zone(s) of ground", o.Message);
        Assert.Contains("but then it failed", o.Message);
        Assert.Equal(0, world.Edits.ChangedZoneCount);
        Assert.Equal([true, false], s.UndoList.Select(c => c.Applied));
    }

    // The game saved the world after the editor read it: the changes go onto the game's save, with their history, and
    // nothing is written until the next save, which is made from the game's save.
    [Fact]
    public void TheChangesGoOntoASaveTheGameMadeSinceWithTheirHistory()
    {
        using var w = new TempWorld();
        using var world = WorldSession.Open(w.Dir);
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        int index = s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone);
        int deleted = s.Scene.Things[index].Id;
        s.Delete([index]);
        // The game saves, an object of its own removed.
        int other = world.World.Objects.First(o => o.Id != deleted && MathF.Abs(o.Position.X) < 200 && MathF.Abs(o.Position.Z) < 200).Id;
        Assert.True(WorldWriter.Save(WorldSave.Load(w.Dir), [], [other], options: new WorldWriter.Options(KeepBase: true)).Saved);
        int game = world.World.SaveNumber + 1;
        Assert.False(world.Save().Done);
        Assert.Equal(game, world.GameSaveSince());
        var files = Directory.GetFiles(w.Dir).ToDictionary(f => f, File.ReadAllBytes);

        var plan = world.PrepareReplay();
        Assert.NotNull(plan);
        Assert.Empty(plan.Clashes);
        var o = world.Replay(plan, _ => throw new InvalidOperationException("nothing clashes"));
        Assert.True(o.Done && o.Reloaded, o.Message);
        Assert.StartsWith($"Your changes are on the game's save #{game} now", o.Message);
        Assert.Contains("History: 2 step(s) kept", o.Message);
        Assert.Equal(files.Keys.Order(), Directory.GetFiles(w.Dir).Order());
        Assert.All(files, f => Assert.True(f.Value.AsSpan().SequenceEqual(File.ReadAllBytes(f.Key)), f.Key));
        Assert.Equal(game, world.World.SaveNumber);
        Assert.Null(world.GameSaveSince());
        Assert.Equal((1, 1, 0, 0), world.Pending);
        Assert.True(world.Restored!.Replayed);
        Assert.True(world.EarlierMayDiffer);
        // The area opened again has the steps, fitted to the game's save: undoing the deletion keeps the object.
        var again = WorldScene.Load(world, 0, 0, 1).Session!;
        Assert.Equal(2, again.UndoList.Count);
        Assert.All(again.UndoList, c => Assert.True(c.Earlier));
        Assert.True(again.Undo());
        Assert.Equal((1, 0, 0, 0), world.Pending);
        // Saved from the game's save.
        var saved = world.Save();
        Assert.True(saved.Done, saved.Message);
        Assert.Equal(game + 1, WorldSave.Load(w.Dir).SaveNumber);
    }

    // The game removed an object the editor moved: left to the game, the move's step cannot be undone any more, and
    // neither can the steps before it.
    [Fact]
    public void StepsTouchingWhatIsLeftToTheGameAreDropped()
    {
        using var w = new TempWorld();
        using var world = WorldSession.Open(w.Dir);
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        TestScenes.MovedThenRemovedByTheGame(s, w.Dir);

        var plan = world.PrepareReplay()!;
        Assert.Equal(Editing.ReplayClash.Removed, Assert.Single(plan.Clashes).Kind);
        var o = world.Replay(plan, _ => false);
        Assert.True(o.Done, o.Message);
        Assert.Contains("History: 0 step(s) kept, 2 dropped", o.Message);
        Assert.Null(world.History);
        Assert.Equal((1, 0, 0, 0), world.Pending);
    }

    // A zone reset marked and then unmarked has nothing left to send, while another zone's reset still waits: after a
    // partial Apply live the first zone's steps are applied, and so is the ground after them that went in.
    [Fact]
    public async Task AResetUnmarkedSinceIsNotCountedAsUnsent()
    {
        using var game = new FakeGame();
        using var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.MarkResets([(0, 0)], false, false, "Reset");
        s.UnmarkResets([(0, 0)], "Keep");
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        s.MarkResets([(1, 0)], false, false, "Reset");
        s.Delete([s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone)]);
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
        using var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var lifted = world.Edits.Get(0, 0) ?? new ZoneEdit(0, 0);
        for (int i = 0; i < EditStore.Cells; i++)
        {
            lifted.Lift[i] = 20f;
        }
        world.Edits.Put(lifted);
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
        using var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var lifted = world.Edits.Get(0, 0) ?? new ZoneEdit(0, 0);
        for (int i = 0; i < EditStore.Cells; i++)
        {
            lifted.Lift[i] = 20f;
        }
        world.Edits.Put(lifted);
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
