using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.EditReplayHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Pending edits brought onto a game save the game made from the editor's own save: what the editor's save wrote is
/// found there and not made twice, and only what changed since is pending. An object replaced at its own place is told
/// from the original by its data.
/// </summary>
public class EditReplayOwnSaveTests
{
    // The editor saved during the session and the game saved from that save: what it wrote is found there (not made
    // twice), and only what changed since is pending.
    [Fact]
    public void WhatTheEditorsOwnSaveWroteIsNotMadeTwice()
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var near = Near(read);
        var (moved, model, later) = (near[0], near[1], near[2]);
        Move(edits, moved, -1, moved.Position + new Vector3(3, 0, 3));
        edits.AddObjects([new NewObject(-2, model.Prefab, model.Position + new Vector3(0, 0, 4), Vector3.Zero, 0f, model.Id)]);
        Raise(edits, 100, 2.5f);
        var first = WorldWriter.Save(read, edits.Copies(e => e.Changed), edits.Deleted, edits.Added, options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber));
        Assert.True(first.Saved, first.Message);
        edits.MarkApplied([(0, 0)]);
        var saved = SavedBy(edits, (0, 0));
        // The game saves from the editor's save (the same files, under the next number).
        int n = WorldWriter.LatestNumber(w.Dir);
        CopySave(w.Dir, n, n + 1);
        // Since the editor's save: an object deleted, the added one deleted again.
        edits.SetDeleted([later.Id, -2], true);

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => true, edits, IdSource())!;
        Assert.Equal((0, 2, 0), (r.Zones, r.Deleted, r.Added));
        // The history finds the moved copy in the game's save, and keeps the deleted one to bring back.
        Assert.NotNull(replay.NewerId(-1));
        Assert.Null(replay.NewerId(moved.Id));
        Assert.NotNull(edits.FindAdded(-2));
    }

    // An object replaced at its own place (a piece claimed: deleted, and a copy with a builder put where it was) and
    // saved: the game's object there is the editor's copy when the game saved from the editor's save (nothing to do),
    // the original when it saved from the save it had (the claim made again). Neither is a clash.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnObjectReplacedAtItsPlaceIsToldFromTheOriginalByItsData(bool fromEditorsSave)
    {
        using var w = new TempWorld();
        var (read, edits, (id, prefab, position, rotation, _)) = OpenEditsOfNearObject(w);
        var claimed = ZdoData.Parse(read.ObjectBytes(id));
        claimed.Set("longs", ObjectField.CreatorKey, "12345");
        edits.SetDeleted([id], true);
        edits.AddObjects([new NewObject(-1, prefab, position, rotation, 0f, null, false, claimed.Serialize())]);
        var first = WorldWriter.Save(read, [], edits.Deleted, edits.Added, options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber));
        Assert.True(first.Saved, first.Message);
        var saved = SavedBy(edits);
        int n = WorldWriter.LatestNumber(w.Dir);
        int from = fromEditorsSave ? n : read.SaveNumber;
        CopySave(w.Dir, from, n + 1);

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, IdSource())!;
        Assert.Equal(fromEditorsSave ? (0, 0) : (1, 1), (r.Deleted, r.Added));
    }
}
