using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.EditReplayHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Pending edits brought onto a save the game made after the editor read the world: what does not clash is made again
/// on the game's save (a moved, an added and a deleted object, a changed zone) without writing anything. The clashes
/// are in EditReplayObjectClashTests and EditReplayGroundClashTests, and what the editor's own save wrote is in
/// EditReplayOwnSaveTests.
/// </summary>
public class EditReplayTests
{
    /// <summary>Every file of the folder, by name.</summary>
    private static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

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
        var ids = IdSource();
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, ids);
        Assert.NotNull(r);
        Assert.Equal((1, 2, 2, 0), (r.Zones, r.Deleted, r.Added, r.Resets));
        // Nothing written: the folder is as the game left it.
        Assert.Equal(before.Keys.Order(), Files(w.Dir).Keys.Order());
        Assert.All(Files(w.Dir), f => Assert.True(f.Value.AsSpan().SequenceEqual(before[f.Key]), f.Key));
        Assert.Throws<InvalidOperationException>(() => replay.Apply(_ => true, edits, ids));

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

    // The edits changed between reading the game's save and bringing them over: nothing is done.
    [Fact]
    public void EditsChangedSinceTheGamesSaveWasReadAreNotBroughtOver()
    {
        using var w = new TempWorld();
        var read = w.Load();
        var edits = new EditStore(read);
        var near = Near(read);
        edits.SetDeleted([near[0].Id], true);
        GameSaves(w.Dir, [], [near[2].Id]);
        var replay = EditReplay.Prepare(read, edits, EditReplay.Saved.None, w.Load());
        edits.SetDeleted([near[1].Id], true);
        Assert.Null(replay.Apply(_ => true, edits, IdSource()));
        Assert.Equal(2, edits.DeletedCount);
    }
}
