using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Pending edits brought onto a save the game made after the editor read the world: what does not clash is made again
/// on the game's save (a moved, an added and a deleted object, a changed zone) without writing anything; an object the
/// game removed or changed and the editor moved or deleted, and a zone both changed, are clashes kept or dropped as
/// chosen; what the editor's own save wrote and the game's save holds is not made twice, an object it replaced at its
/// own place told from the original by its data.
/// </summary>
public class EditReplayTests
{
    /// <summary>Ids for objects that come back, as the session gives them.</summary>
    private int _next = -100;

    /// <summary>The next id for an object that comes back.</summary>
    private int Next() => _next--;

    /// <summary>Every file of the folder, by name.</summary>
    private static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

    /// <summary>Objects of the save near the world's middle (the test world's far ones are outside the world).</summary>
    private static List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> Near(WorldSave world) =>
        [.. world.Objects.Where(o => MathF.Abs(o.Position.X) < 200 && MathF.Abs(o.Position.Z) < 200)];

    /// <summary>The game saving the world: the next save, with these objects removed and these added (the base stays).</summary>
    private static void GameSaves(string dir, IReadOnlyList<ZoneEdit> ground, int[] removed, NewObject[]? added = null)
    {
        var r = WorldWriter.Save(WorldSave.Load(dir), ground, removed, added, options: new WorldWriter.Options(KeepBase: true));
        Assert.True(r.Saved, r.Message);
    }

    /// <summary>Whether a save has an object of this kind at this place.</summary>
    private static bool Has(WorldSave world, int prefab, Vector3 at) => world.ObjectRefs.Any(o => o.Prefab == prefab && Vector3.Distance(o.Position, at) < 0.01f);

    /// <summary>An object moved: deleted where it was, a copy keeping its data where it goes.</summary>
    private static void Move(EditStore edits, (int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale) o, int id, Vector3 to)
    {
        edits.SetDeleted([o.Id], true);
        edits.AddObjects([new NewObject(id, o.Prefab, to, o.Rotation, 0f, o.Id, Fresh: false)]);
    }

    /// <summary>One point of zone 0, 0 raised by the editor.</summary>
    private static void Raise(EditStore edits, int point, float by)
    {
        var zone = edits.Get(0, 0)!;
        zone.Modified[point] = true;
        zone.Level[point] = by;
        edits.Put(zone);
    }

    [Fact]
    public void EditsMadeOnASaveAreMadeAgainOnTheGamesNextSave()
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var near = Near(read);
        var (moved, deleted, gameDeleted, model) = (near[0], near[1], near[2], near[3]);
        var to = moved.Position + new Vector3(3, 0, 3);
        var placed = model.Position + new Vector3(5, 0, 0);
        Move(edits, moved, -1, to);
        edits.SetDeleted([deleted.Id], true);
        edits.AddObjects([new NewObject(-2, model.Prefab, placed, Vector3.Zero, 0f, model.Id)]);
        Raise(edits, 100, 2.5f);
        // The game played on and saved: an object of its own removed.
        GameSaves(w.Dir, [], [gameDeleted.Id]);
        var newer = w.Load();
        Assert.Equal(read.SaveNumber + 1, newer.SaveNumber);
        var before = Files(w.Dir);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, newer);
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, Next);
        Assert.NotNull(r);
        Assert.Equal((1, 2, 2, 0), (r.Zones, r.Deleted, r.Added, r.Resets));
        // Nothing written: the folder is as the game left it.
        Assert.Equal(before.Keys.Order(), Files(w.Dir).Keys.Order());
        Assert.All(Files(w.Dir), f => Assert.True(f.Value.AsSpan().SequenceEqual(before[f.Key]), f.Key));
        Assert.Throws<InvalidOperationException>(() => replay.Apply(_ => true, edits, Next));

        // Saved on the game's save: the editor's changes and the game's are both there.
        var saved = WorldWriter.Save(newer, edits.Copies(e => e.Changed), edits.Deleted, edits.Added, edits.Resets, new WorldWriter.Options(Latest: newer.SaveNumber));
        Assert.True(saved.Saved, saved.Message);
        var after = w.Load();
        Assert.False(Has(after, moved.Prefab, moved.Position));
        Assert.True(Has(after, moved.Prefab, to));
        Assert.False(Has(after, deleted.Prefab, deleted.Position));
        Assert.True(Has(after, model.Prefab, placed));
        Assert.False(Has(after, gameDeleted.Prefab, gameDeleted.Position));
        Assert.Equal(2.5f, after.TerrainZones.Single(z => z.ZoneX == 0 && z.ZoneZ == 0).LevelDelta[100], 4);
    }

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
        var r = replay.Apply(k => k == ReplayClash.Removed && keepMine, edits, Next)!;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AZoneBothChangedKeepsTheEditorsPointsOverTheGamesOrTheGamesAlone(bool keepMine)
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        Raise(edits, 100, 2.5f);
        var game = new EditStore(w.Load()).Get(0, 0)!;
        game.Modified[300] = true;
        game.Level[300] = -1.5f;
        GameSaves(w.Dir, [game], []);

        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        Assert.Equal((ReplayClash.Ground, (0, 0)), (Assert.Single(replay.Clashes).Kind, replay.Clashes[0].Zone));
        var r = replay.Apply(_ => keepMine, edits, Next)!;
        var zone = edits.Get(0, 0)!;
        Assert.Equal(-1.5f, zone.Level[300], 4);
        Assert.Equal(keepMine, zone.Modified[100]);
        Assert.Equal(keepMine ? 1 : 0, r.Zones);
        Assert.Equal(keepMine ? 0 : 1, r.GameGround.Count);
        Assert.Equal(!keepMine, r.GameGround.Contains((0, 0)));
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
        var done = replay.Apply(_ => true, edits, Next)!;
        Assert.Equal(1, done.Deleted);
        Assert.Equal(readSaveKept ? 0 : 1, done.Unread);
    }

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
        var saved = new EditReplay.Saved(new HashSet<(int X, int Z)> { (0, 0) }, edits.Deleted, edits.Added.ToHashSet());
        // The game saves from the editor's save (the same files, under the next number).
        int n = WorldWriter.LatestNumber(w.Dir);
        foreach (string ext in new[] { ".chunks", ".db2", ".fwl2", ".ok" })
        {
            File.Copy(Path.Combine(w.Dir, $"_main.{n}{ext}"), Path.Combine(w.Dir, $"_main.{n + 1}{ext}"));
        }
        // Since the editor's save: an object deleted, the added one deleted again.
        edits.SetDeleted([later.Id, -2], true);

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => true, edits, Next)!;
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
        var read = w.Load();
        var edits = new EditStore(read);
        var (id, prefab, position, rotation, _) = Near(read)[0];
        var claimed = ZdoData.Parse(read.ObjectBytes(id));
        claimed.Set("longs", ObjectField.CreatorKey, "12345");
        edits.SetDeleted([id], true);
        edits.AddObjects([new NewObject(-1, prefab, position, rotation, 0f, null, false, claimed.Serialize())]);
        var first = WorldWriter.Save(read, [], edits.Deleted, edits.Added, options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber));
        Assert.True(first.Saved, first.Message);
        var saved = new EditReplay.Saved(new HashSet<(int X, int Z)>(), edits.Deleted, edits.Added.ToHashSet());
        int n = WorldWriter.LatestNumber(w.Dir);
        int from = fromEditorsSave ? n : read.SaveNumber;
        foreach (string ext in new[] { ".chunks", ".db2", ".fwl2", ".ok" })
        {
            File.Copy(Path.Combine(w.Dir, $"_main.{from}{ext}"), Path.Combine(w.Dir, $"_main.{n + 1}{ext}"));
        }

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, Next)!;
        Assert.Equal(fromEditorsSave ? (0, 0) : (1, 1), (r.Deleted, r.Added));
    }
}
