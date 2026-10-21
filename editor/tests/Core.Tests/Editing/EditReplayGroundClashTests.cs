using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.EditReplayHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Ground clashes when pending edits are brought onto a save the game made: a zone both the editor and the game changed
/// keeps the editor's points or the game's, an undo after the editor's save is kept with the editor's ground, and a
/// ground reset goes with the game's ground.
/// </summary>
public class EditReplayGroundClashTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AZoneBothChangedKeepsTheEditorsPointsOverTheGamesOrTheGamesAlone(bool keepMine)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        Raise(edits, 100, 2.5f);
        GameSaves(w.Dir, [GameGround(w)], []);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        Assert.Equal((ReplayClash.Ground, (0, 0)), (Assert.Single(replay.Clashes).Kind, replay.Clashes[0].Zone));
        var r = replay.Apply(_ => keepMine, edits, IdSource())!;
        var zone = edits.Get(0, 0)!;
        Assert.Equal(-1.5f, zone.Level[300], 4);
        Assert.Equal(keepMine, zone.Modified[100]);
        Assert.Equal(keepMine ? 1 : 0, r.Zones);
        Assert.Equal(keepMine ? 0 : 1, r.GameGround.Count);
        Assert.Equal(!keepMine, r.GameGround.Contains((0, 0)));
    }

    // Raised and saved, then undone: when the game played on from the editor's save and changed another point of the
    // zone, keeping the editor's ground keeps the undo too.
    [Fact]
    public void AnUndoAfterTheEditorsSaveIsKeptInAZoneBothChanged()
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var before = edits.Get(0, 0)!;
        Raise(edits, 100, 2.5f);
        Assert.True(WorldWriter.Save(read, edits.Copies(e => e.Changed), options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber)).Saved);
        edits.MarkApplied([(0, 0)]);
        var saved = SavedBy(edits, (0, 0));
        edits.Put(before.Clone());
        GameSaves(w.Dir, [GameGround(w)], []);

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Equal(ReplayClash.Ground, Assert.Single(replay.Clashes).Kind);
        replay.Apply(_ => true, edits, IdSource());
        var zone = edits.Get(0, 0)!;
        Assert.Equal(before.Modified[100], zone.Modified[100]);
        Assert.Equal(before.Level[100], zone.Level[100], 4);
        Assert.Equal(-1.5f, zone.Level[300], 4);
    }

    // A zone left to the game's ground is not reset to generated ground either (that would take the game's ground
    // away); a reset of its objects alone stays.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AGroundResetGoesWithTheGamesGround(bool resetGround)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        Raise(edits, 100, 2.5f);
        edits.SetReset(new ZoneReset(0, 0, true, resetGround), true);
        GameSaves(w.Dir, [GameGround(w)], []);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        Assert.Equal(ReplayClash.Ground, Assert.Single(replay.Clashes).Kind);
        Assert.Equal(resetGround ? 0 : 1, replay.Apply(_ => false, edits, IdSource())!.Resets);
    }
}
