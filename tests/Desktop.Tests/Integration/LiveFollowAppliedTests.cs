using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveFollowSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Live: ground and objects the editor changed itself are kept and asked about on apply, and the reads taken around an Apply live are merged or dropped.
/// </summary>
public class LiveFollowAppliedTests
{
    // The editor's own new objects: not taken twice when the game has them, and gone when the game no
    // longer does (a player picked it), with nothing left to apply.
    [Fact]
    public async Task TheEditorsOwnObjectsAreFollowed()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        var tree = s.Scene.Things[Tree(s)];
        var placed = s.Commit("Place", null, [], [(new NewObject(0, tree.Prefab, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f), false)]);
        Assert.True((await world.ApplyLive()).Done);
        var objects = AsRead(w, (0, 0));
        objects.Add((77, 1000, w.ObjectBytes(tree.Id)));
        game.Zones[(0, 0)] = (1, objects);
        var f = await Follow(world, (0, 0));
        Assert.True(f == null || !f.Any);
        game.Zones[(0, 0)] = (2, AsRead(w, (0, 0)));
        f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { s.Scene.Things[placed[0]].Id }, f.Ours);
        s.TakeGameChanges(f);
        Assert.True(s.Scene.Things[placed[0]].Gone);
        Assert.Equal((0, 0, 0, 0), world.Pending);
    }

    // A read of the game taken before an Apply live and merged after it lacks what the apply sent: it is
    // dropped, so the editor's new objects are not taken as removed by players; the zone is read again.
    [Fact]
    public async Task AReadTakenBeforeAnApplyIsDropped()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        var tree = s.Scene.Things[Tree(s)];
        game.Zones[(0, 0)] = (1, AsRead(w, (0, 0)));
        var stale = (await world.FetchGame([(0, 0)]))!;
        var placed = s.Commit("Place", null, [], [(new NewObject(0, tree.Prefab, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f), false)]);
        Assert.True((await world.ApplyLive()).Done);
        var f = world.MergeGame(stale)!;
        Assert.False(f.Any);
        s.TakeGameChanges(f);
        Assert.False(s.Scene.Things[placed[0]].Gone);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // Not marked as merged: the zone is read again.
        int reads = game.ZoneReads;
        await world.FetchGame([(0, 0)]);
        Assert.Equal(reads + 1, game.ZoneReads);
    }

    // A read taken while an Apply live is under way is not merged then (null: later), and once the apply is done it
    // is dropped, as it lacks what the apply sent.
    [Fact]
    public async Task AReadTakenDuringAnApplyIsDropped()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        var tree = s.Scene.Things[Tree(s)];
        s.Shape(32, 32, Two, 3, 0, "raise");
        var placed = s.Commit("Place", null, [], [(new NewObject(0, tree.Prefab, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f), false)]);
        game.HoldTerrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = world.ApplyLive();
        if (await Task.WhenAny(game.TerrainArrived.Task, applying) == applying)
        {
            Assert.Fail("The Apply ended before the ground reached the game: " + (await applying).Message);
        }
        game.Zones[(0, 0)] = (1, AsRead(w, (0, 0)));
        var during = (await world.FetchGame([(0, 0)]))!;
        Assert.Null(world.MergeGame(during));
        game.HoldTerrain.SetResult();
        Assert.True((await applying).Done);
        var f = world.MergeGame(during)!;
        Assert.False(f.Any);
        s.TakeGameChanges(f);
        Assert.False(s.Scene.Things[placed[0]].Gone);
    }

    // Ground dug in the game comes in where the editor has no changes of its own; where it has, they are
    // kept (applying asks first), and undoing them brings in the game's ground.
    [Fact]
    public async Task GroundChangedInTheGameComesInOrWaits()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        var dug = world.Edits.Get(0, 0)!;
        dug.Modified[2000] = true;
        dug.Level[2000] = -3;
        dug.Smooth[2000] = 0;
        game.Zones[(0, 0)] = (1, Dug(w, dug));
        var f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { (0, 0) }, f.Ground);
        Assert.Equal(-3, world.Edits.Get(0, 0)!.Level[2000], 3);
        Assert.True(s.TakeGameChanges(f));
        int p = (2000 / 65) * s.Ground.W + 2000 % 65;
        Assert.Equal(-3, s.Ground.Level[p], 3);
        Assert.Empty(world.Edits.GameChanged);
        // The editor changes that zone, and the game digs it again: the editor's changes stay.
        s.Shape(10, 10, Two, 3, 0, "raise");
        dug.Level[2000] = -5;
        game.Zones[(0, 0)] = (2, Dug(w, dug));
        f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { (0, 0) }, f.Kept);
        Assert.Equal(new[] { (0, 0) }, world.Edits.PendingOverGame());
        Assert.Equal(-3, world.Edits.Get(0, 0)!.Level[2000], 3);
        // Applying asks; no: not applied.
        var asked = new List<IReadOnlyList<(int X, int Z)>>();
        int sent = game.Terrain.Count;
        var o = await world.ApplyLive(over => { asked.Add(over); return Task.FromResult(false); });
        Assert.True(o.Declined);
        Assert.Single(asked);
        Assert.Equal(sent, game.Terrain.Count);
        // Undone: the zone takes the game's ground.
        s.Undo();
        Assert.Equal(new[] { (0, 0) }, world.TakeWaitingGround());
        Assert.Equal(-5, world.Edits.Get(0, 0)!.Level[2000], 3);
        Assert.Empty(world.Edits.GameChanged);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // Changed again and applied (yes): the mark goes.
        s.Shape(10, 10, Two, 3, 0, "raise");
        dug.Level[2000] = -7;
        game.Zones[(0, 0)] = (3, Dug(w, dug));
        await Follow(world, (0, 0));
        Assert.NotEmpty(world.Edits.PendingOverGame());
        Assert.True((await world.ApplyLive(_ => Task.FromResult(true))).Done);
        Assert.Empty(world.Edits.GameChanged);
        // The game then sends back what was applied: nothing new.
        game.Zones[(0, 0)] = (4, Dug(w, world.Edits.Get(0, 0)!));
        f = (await Follow(world, (0, 0)))!;
        Assert.Empty(f.Ground);
        Assert.Empty(f.Kept);
    }

    // In the window: what the game changed is said in the status bar, and applying over ground the game
    // changed asks first (not applied when the answer is no).
    [AvaloniaFact]
    public async Task TheWindowSaysWhatCameAndAsksBeforeApplyingOverIt()
    {
        using var game = new FakeGame();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var asked = new List<string>();
        bool answer = false;
        w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
        await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
        await w.EditArea(0, 0, 1);
        var world = w.World!;
        var s = w.Session!;
        var wsave = world.World;
        int tree = Tree(s);
        var cut = Zdo(wsave, s.Scene.Things[tree].Id);
        game.Zones[(0, 0)] = (1, AsRead(wsave, (0, 0)).Where(o => (o.Item1, o.Item2) != cut).ToList());
        await w.FollowGame();
        Assert.True(s.Scene.Things[tree].Gone);
        Assert.StartsWith("From the game: 1 removed.", w.MessageText.Text);
        // Then ground: the editor raises zone 0, 0 while the game digs there.
        s.Shape(10, 10, Two, 3, 0, "raise");
        var dug = new EditStore(wsave).Get(0, 0)!;
        dug.Modified[2000] = true;
        dug.Level[2000] = -3;
        game.Zones[(0, 0)] = (2, Dug(wsave, dug));
        await w.FollowGame();
        Assert.Contains("where you have changes not applied", w.MessageText.Text);
        int sent = game.Terrain.Count;
        await w.ApplyNow();
        Assert.Equal(new[] { "Apply over the game's changes" }, asked);
        Assert.Equal(sent, game.Terrain.Count);
        answer = true;
        await w.ApplyNow();
        Assert.Equal(sent + 1, game.Terrain.Count);
        Assert.Empty(world.Edits.GameChanged);
        w.Close();
    }
}
