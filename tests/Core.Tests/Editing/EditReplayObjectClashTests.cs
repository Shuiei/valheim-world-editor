using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.EditReplayHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Object clashes when pending edits are brought onto a save the game made: an object the game removed or changed and
/// the editor moved, deleted or edited, kept or dropped as chosen; an added object the game changed and the editor then
/// deleted or moved.
/// </summary>
public class EditReplayObjectClashTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnObjectTheGameRemovedThatTheEditorMovedClashes(bool keepMine)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var moved = Near(read)[0];
        Move(edits, moved, -1, moved.Position + new Vector3(3, 0, 3));
        GameSaves(w.Dir, [], [moved.Id]);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        var clash = Assert.Single(replay.Clashes);
        Assert.Equal(new EditReplay.Clash(ReplayClash.Removed, moved.Prefab, moved.Position, false), clash);
        var r = replay.Apply(k => k == ReplayClash.Removed && keepMine, edits, IdSource())!;
        if (keepMine)
        {
            // Back where the editor put it, with the data it had.
            var back = Assert.Single(edits.Added);
            Assert.Equal(-1, back.Id);
            Assert.Null(back.SourceId);
            Assert.NotNull(back.Raw);
            Assert.Empty(r.Dropped);
        }
        else
        {
            // Gone, as the game has it; the editor's copy kept for the history only.
            Assert.Empty(edits.Added);
            Assert.True(r.Dropped.SetEquals([moved.Id, -1]));
            Assert.NotNull(edits.FindAdded(-1));
        }
        Assert.Equal(0, r.Deleted);
    }

    // The game changed an object the editor deleted (its data, at the same place). When the save the editor read is gone
    // (the game removes older saves), the editor cannot tell, and says so.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnObjectTheGameChangedThatTheEditorDeletedClashes(bool readSaveKept)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var (id, prefab, position, rotation, _) = Near(read)[0];
        edits.SetDeleted([id], true);
        var changed = ZdoData.Parse(read.ObjectBytes(id));
        changed.Set("ints", StableHash.Of("vwe-test"), "7");
        var r = WorldWriter.Save(w.Load(), [], [id], [new NewObject(-1, prefab, position, rotation, 0f, Fresh: false, Raw: changed.Serialize())],
            options: new WorldWriter.Options(KeepBase: readSaveKept));
        Assert.True(r.Saved, r.Message);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        var clash = Assert.Single(replay.Clashes);
        Assert.Equal((ReplayClash.Changed, !readSaveKept), (clash.Kind, clash.Unsure));
        var done = replay.Apply(_ => true, edits, IdSource())!;
        Assert.Equal(1, done.Deleted);
        Assert.Equal(readSaveKept ? 0 : 1, done.Unread);
    }

    // An object edited where it stands (the inspector: deleted, a copy with the new data in its place) is settled with
    // its original: kept or dropped together when the game changed or removed the original.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AnObjectEditedInPlaceGoesWithItsOriginalsClash(bool gameRemoved, bool keepMine)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var (id, prefab, position, rotation, _) = Near(read)[0];
        var edited = ZdoData.Parse(read.ObjectBytes(id));
        edited.Set("ints", StableHash.Of("vwe-mine"), "1");
        edits.SetDeleted([id], true);
        edits.AddObjects([new NewObject(-1, prefab, position, rotation, 0f, null, false, edited.Serialize())]);
        var game = ZdoData.Parse(read.ObjectBytes(id));
        game.Set("ints", StableHash.Of("vwe-game"), "2");
        GameSaves(w.Dir, [], [id], gameRemoved ? null : [new NewObject(-5, prefab, position, rotation, 0f, null, false, game.Serialize())]);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        Assert.Equal(gameRemoved ? ReplayClash.Removed : ReplayClash.Changed, Assert.Single(replay.Clashes).Kind);
        var r = replay.Apply(_ => keepMine, edits, IdSource())!;
        Assert.Equal((keepMine && !gameRemoved ? 1 : 0, keepMine ? 1 : 0), (r.Deleted, r.Added));
    }

    // An object the editor added and saved, then deleted or moved: when the game, playing on from the editor's save,
    // changed it (a chest filled), that is a clash too, the move's copy kept or dropped with it (kept, it takes the
    // game's object along).
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AnAddedAndSavedObjectTheGameChangedClashesWhenDeletedOrMoved(bool keepMine, bool moved)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var (model, prefab, position, _, _) = Near(read)[1];
        var added = new NewObject(-1, prefab, position + new Vector3(0, 0, 4), Vector3.Zero, 0f, model);
        edits.AddObjects([added]);
        Assert.True(WorldWriter.Save(read, [], [], edits.Added, options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber)).Saved);
        var saved = SavedBy(edits);
        var editors = w.Load();
        int y = editors.ObjectRefs.FindIndex(o => o.Prefab == added.Prefab && Vector3.Distance(o.Position, added.Position) < 0.01f);
        var filled = ZdoData.Parse(editors.ObjectBytes(y));
        filled.Set("ints", StableHash.Of("vwe-game"), "2");
        GameSaves(w.Dir, [], [y], [new NewObject(-5, added.Prefab, added.Position, filled.Rotation, 0f, null, false, filled.Serialize())]);
        edits.SetDeleted([-1], true);
        if (moved)
        {
            edits.AddObjects([added with { Id = -7, Position = added.Position + new Vector3(3, 0, 0) }]);
        }

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Equal(new EditReplay.Clash(ReplayClash.Changed, added.Prefab, added.Position, false), Assert.Single(replay.Clashes));
        var r = replay.Apply(_ => keepMine, edits, IdSource())!;
        Assert.Equal(keepMine ? 1 : 0, r.Deleted);
        Assert.Equal(keepMine && moved ? 1 : 0, r.Added);
        Assert.Equal(!keepMine, r.Dropped.Contains(-1));
        if (keepMine && moved)
        {
            var copy = Assert.Single(edits.Added);
            Assert.False(copy.Fresh);
            Assert.NotNull(copy.SourceId);
        }
    }
}
