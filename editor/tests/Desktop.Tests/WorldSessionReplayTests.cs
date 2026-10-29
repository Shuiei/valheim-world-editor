using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A world whose saves the game made since the editor read it (WorldSession): the changes go onto the game's save with
/// their history, and steps touching what the game removed are dropped.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionReplayTests
{
    // The game saved the world after the editor read it: the changes go onto the game's save, with their history, and
    // nothing is written until the next save, which is made from the game's save.
    [Fact]
    public void TheChangesGoOntoASaveTheGameMadeSinceWithTheirHistory()
    {
        using var w = new TempWorld();
        using var world = WorldSession.Open(w.Dir);
        var s = RaisedZone(world, 32, 32);
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
        var s = RaisedZone(world, 32, 32);
        TestScenes.MovedThenRemovedByTheGame(s, w.Dir);

        var plan = world.PrepareReplay()!;
        Assert.Equal(Editing.ReplayClash.Removed, Assert.Single(plan.Clashes).Kind);
        var o = world.Replay(plan, _ => false);
        Assert.True(o.Done, o.Message);
        Assert.Contains("History: 0 step(s) kept, 2 dropped", o.Message);
        Assert.Null(world.History);
        Assert.Equal((1, 0, 0, 0), world.Pending);
    }
}
