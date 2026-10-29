using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.EditReplayHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// An object of the save read that comes back on the game's save (deleted, saved, then the deletion undone, and the
/// game saved from the editor's save, so lacks it): added again with its data as read; with its bytes damaged since,
/// as a new object of its kind, counted as unread.
/// </summary>
public class EditReplayOldDataTests
{
    /// <summary>
    /// The copy's save read and its edits, after the first near object was deleted and saved, the deletion undone, and
    /// the game saved from the editor's save: the object's id, and the editor's save as the replay reads it.
    /// </summary>
    private static (WorldSave Read, EditStore Edits, int Id, EditReplay.Saved Saved) UndoneDeletion(TempWorld w)
    {
        var (read, edits, (id, _, _, _, _)) = OpenEditsOfNearObject(w);
        edits.SetDeleted([id], true);
        var first = WorldWriter.Save(read, [], edits.Deleted, edits.Added, options: new WorldWriter.Options(KeepBase: true, Latest: read.SaveNumber));
        Assert.True(first.Saved, first.Message);
        var saved = SavedBy(edits);
        edits.SetDeleted([id], false);
        int n = WorldWriter.LatestNumber(w.Dir);
        CopySave(w.Dir, n, n + 1);
        return (read, edits, id, saved);
    }

    [Fact]
    public void AnUndoneDeletionTheGamesSaveLacksComesBackWithItsData()
    {
        using var w = new TempWorld();
        var (read, edits, id, saved) = UndoneDeletion(w);
        var o = read.ObjectRefs[id];

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        Assert.Empty(replay.Clashes);
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, IdSource())!;

        Assert.Equal((0, 1, 0), (r.Deleted, r.Added, r.Unread));
        var back = edits.FindAdded(-100)!;
        Assert.Equal((o.Prefab, o.Position, false), (back.Prefab, back.Position, back.Fresh));
        Assert.Equal(ZdoData.Parse(read.ObjectBytes(id)).Serialize(), back.Raw);
    }

    [Fact]
    public void AnObjectWhoseBytesAreDamagedSinceComesBackAsANewOneOfItsKind()
    {
        using var w = new TempWorld();
        var (read, edits, id, saved) = UndoneDeletion(w);
        var o = read.ObjectRefs[id];
        // Its bytes in the save read overwritten (every section flagged, counts past the object's end).
        string file = Path.Combine(read.Directory, o.File.FileName);
        byte[] bytes = File.ReadAllBytes(file);
        Array.Fill(bytes, (byte)0xFF, (int)o.Start, (int)(o.End - o.Start));
        File.WriteAllBytes(file, bytes);
        using var log = Log.Listen();

        var replay = EditReplay.Prepare(read, edits, saved, w.Load());
        var r = replay.Apply(_ => throw new InvalidOperationException("nothing clashes"), edits, IdSource())!;

        Assert.Equal((1, 1), (r.Added, r.Unread));
        var back = edits.FindAdded(-100)!;
        Assert.Equal((o.Prefab, o.Position, true), (back.Prefab, back.Position, back.Fresh));
        Assert.Null(back.Raw);
        Assert.Contains(log.Lines, l => l.Contains("damaged", StringComparison.Ordinal));
    }
}
