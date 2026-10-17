using System.Security.Cryptography;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>
    /// <see cref="Write"/> for Apply live, which can come at every edit (Auto apply): the steps, the edits' objects and
    /// the world (held as it is) are taken now, on the caller's thread, and the objects are found, their data read and
    /// the file written in the background, one write after the other; a write a newer one for the same world has
    /// replaced before it started is left out. A failure to write is logged, as <see cref="Write"/>'s callers do.
    /// </summary>
    public static void WriteLater(WorldSession w, EditSession.Kept k)
    {
        var snapshot = Take(w, k);
        lock (QueueLock)
        {
            long turn = ++_queued;
            Latest[snapshot.Path] = turn;
            _tail = _tail.ContinueWith(_ =>
            {
                try
                {
                    lock (QueueLock)
                    {
                        if (Latest[snapshot.Path] != turn)
                        {
                            return;
                        }
                    }
                    Save(snapshot);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Caught(ex, "keeping the history on disk");
                }
                finally
                {
                    snapshot.Release();
                }
            }, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }

    /// <summary>Guards <see cref="_tail"/>, <see cref="_queued"/> and <see cref="Latest"/>.</summary>
    private static readonly Lock QueueLock = new();

    /// <summary>
    /// The last write queued by <see cref="WriteLater"/>; each runs after the one before, whatever became of it (a
    /// write failing for another reason than the disk faults its own task, which the app reports as a background
    /// task's error).
    /// </summary>
    private static Task _tail = Task.CompletedTask;

    /// <summary>Counts the writes queued, to tell the latest for a file.</summary>
    private static long _queued;

    /// <summary>The latest write queued for each history file.</summary>
    private static readonly Dictionary<string, long> Latest = [];

    /// <summary>
    /// Waits until the writes queued by <see cref="WriteLater"/> are done, so a read, a removal or another write of a
    /// history file comes after them. A failed write is not thrown here: it is reported on its own.
    /// </summary>
    public static void Flush()
    {
        Task tail;
        lock (QueueLock)
        {
            tail = _tail;
        }
        // Waits on a continuation, which does not observe a failure of the write itself.
        tail.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default).Wait();
    }

    /// <summary>
    /// Takes what the history file of the world will hold, cheaply: each step's fields as they are now, and what
    /// finding its objects needs (<see cref="Sources"/>; the world is held from here until they are read).
    /// </summary>
    private static Snapshot Take(WorldSession w, EditSession.Kept k)
    {
        string key = KeyOf(w);
        string path = PathOf(key);
        var all = k.Undo.Concat(k.Redo).ToList();
        if (all.Count == 0)
        {
            return new Snapshot(path, key, null, 0, null, [], 0, 0);
        }
        var order = all.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i);
        var steps = new List<KeptStep>(all.Count);
        foreach (var c in all)
        {
            int[] cids = k.Ids[c];
            (int, bool, bool)[] things = [.. cids.Select((id, n) => (id, c.Things[n].Before, c.Things[n].After))];
            steps.Add(new KeptStep(c.Label, c.Time.Ticks, c.Removed, c.Applied, c.RevertOf != null && order.TryGetValue(c.RevertOf, out int r) ? r : -1,
                k.FrameOf(c), c.Points, c.Before, c.After, [.. c.Zones], things, c.Resets));
        }
        string? stamp = Stamp(w);
        long savedAt = DateTime.UtcNow.Ticks;
        // Every object the steps need, once; the added ones as they are now (an edit replaces them, a reload drops
        // them).
        var ids = k.Ids.Values.SelectMany(v => v).Distinct().ToList();
        var edits = w.Edits;
        var addedById = new Dictionary<int, NewObject>();
        foreach (int id in ids.Where(i => i < 0))
        {
            if (edits.FindAdded(id) is { } n)
            {
                addedById[id] = n;
            }
        }
        var world = w.World;
        // Last, once nothing can fail: the hold is let go by whoever writes the snapshot.
        var sources = new Sources(world, world.Hold(), ids, edits.Deleted, edits.Added, addedById, WorldSave.Builder);
        return new Snapshot(path, key, stamp, savedAt, sources, steps, k.Undo.Count, k.Redo.Count);
    }

    /// <summary>
    /// The steps' objects found and their data read, from what was taken (<see cref="Sources"/>), then the world let
    /// go: each object once, what it is, where and its data, and its place among them by id.
    /// </summary>
    private static (List<KeptThing> Things, Dictionary<int, int> Index) Find(Sources o)
    {
        try
        {
            var found = WorldScene.FindThings(o.World, o.Deleted, o.Added, o.AddedById.GetValueOrDefault, o.Ids);
            var index = new Dictionary<int, int>();
            var things = new List<KeptThing>();
            foreach (int id in o.Ids)
            {
                if (!found.TryGetValue(id, out var t))
                {
                    continue;
                }
                byte[]? bytes = null;
                try
                {
                    bytes = ObjectData.Bytes(o.World, o.AddedById.GetValueOrDefault, id, o.Builder);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
                {
                    Log.CaughtBrief(ex, $"reading object {id}'s data for the history (none is kept)");
                }
                index[id] = things.Count;
                things.Add(new KeptThing(t.Prefab, t.Piece, t.Position, t.Rotation, t.Scale, bytes));
            }
            return (things, index);
        }
        finally
        {
            o.Held.Dispose();
        }
    }

    /// <summary>
    /// Writes a snapshot's history file (replacing the file in one move, so a crash leaves the old one); with no
    /// steps the file is removed.
    /// </summary>
    private static void Save(Snapshot s)
    {
        if (s.Objects == null)
        {
            Remove(s.Path);
            return;
        }
        var (things, index) = Find(s.Objects);
        Directory.CreateDirectory(Folder);
        string temp = s.Path + ".new";
        try
        {
            WriteFile(s, things, index, temp);
            File.Move(temp, s.Path, overwrite: true);
        }
        catch (Exception) when (File.Exists(temp))
        {
            // Not left half-written beside the history (the error goes on).
            File.Delete(temp);
            throw;
        }
    }
}
