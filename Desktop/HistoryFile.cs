using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The history kept on disk, so it is there again when the editor is closed and the world opened later
/// (history/ of the data folder, one file per world). It is written after each save or Apply live, so its
/// steps are what the world holds. Ground points and zones are kept by world position; objects by their
/// kind and exact place (their ids change at every save), each with its full data, so one that is gone
/// when the world is opened again can be brought back by an undo or redo, chest contents and all.
/// </summary>
public static class HistoryFile
{
    /// <summary>The first four bytes of the file's (gzipped) contents ("VWEH").</summary>
    private const int Magic = 0x48455756; // "VWEH"
    /// <summary>The file layout's version; a file of another version is not read.</summary>
    private const int Version = 1;

    /// <summary>Where the history files are: history/ of the data folder.</summary>
    public static string Folder => Path.Combine(AppSettings.DataDir, "history");

    /// <summary>A world on this computer by its folder; a live one by where it runs, its name and seed.</summary>
    public static string KeyOf(WorldSession w) => w.IsLive
        ? $"live\n{w.Label}\n{w.World.Name}\n{w.World.Seed}"
        : $"file\n{Path.GetFullPath(w.World.Directory)}";

    /// <summary>
    /// The history file of a world key (<c>KeyOf</c>): a short hash of it, so any key makes a safe file name.
    /// </summary>
    public static string PathOf(string key)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return Path.Combine(Folder, hash + ".history");
    }

    /// <summary>
    /// What a world on disk looks like: its latest save (index file name, time and size). A game that
    /// saved the world since makes another. Null for a live world (it changes all the time).
    /// </summary>
    public static string? Stamp(WorldSession w)
    {
        if (w.IsLive || !Directory.Exists(w.World.Directory))
        {
            return null;
        }
        // The newest save; none left (removed while the world is open): no stamp.
        string? latest = Directory.GetFiles(w.World.Directory, "_main.*.chunks")
            .Select(f => (File: f, Parts: Path.GetFileName(f).Split('.')))
            .Where(p => p.Parts.Length == 3 && int.TryParse(p.Parts[1], out _))
            .OrderBy(p => int.Parse(p.Parts[1], System.Globalization.CultureInfo.InvariantCulture))
            .Select(p => p.File)
            .LastOrDefault();
        if (latest == null)
        {
            return null;
        }
        var info = new FileInfo(latest);
        return $"{info.Name}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
    }

    /// <summary>
    /// Removes the world's history file, after the writes queued for it; a file that cannot be removed is left as it
    /// is.
    /// </summary>
    public static void Delete(WorldSession w)
    {
        Flush();
        Remove(PathOf(KeyOf(w)));
    }

    /// <summary>Removes a history file; one that cannot be removed is left as it is.</summary>
    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // ---- Writing.

    /// <summary>
    /// An object one of the steps needs, as written: what it is, where, and its saved data (null when it could not be
    /// read).
    /// </summary>
    private sealed record KeptThing(int Prefab, bool Piece, Vector3 Position, Vector3 Rotation, float Scale, byte[]? Bytes);

    /// <summary>
    /// One step as written: its fields at the time it was taken (its flags change later), with its points and zones
    /// already in world positions' frame and its objects by id (those not found are left out when written).
    /// </summary>
    private sealed record KeptStep(string Label, long Ticks, bool Removed, bool Applied, int RevertOf, EditSession.Frame Frame, int[] Points,
        Ground.State Before, Ground.State After, (int X, int Z)[] Zones, (int Id, bool Before, bool After)[] Things,
        (ZoneReset Reset, bool Before, bool After)[] Resets);

    /// <summary>
    /// What finding the steps' objects and reading their data needs, taken with the steps: the world, held so a live
    /// merge leaves it as it is until they are read (<see cref="WorldSave.Hold"/>), and the edits' objects and the
    /// builder as they were (the edits go on changing on the window's thread).
    /// </summary>
    /// <param name="World">The world, held.</param>
    /// <param name="Held">The world's hold, let go once the objects are read.</param>
    /// <param name="Ids">Every object the steps need, once.</param>
    /// <param name="Deleted">The saved objects deleted then.</param>
    /// <param name="Added">The objects added then and not deleted again.</param>
    /// <param name="AddedById">The steps' added objects (deleted again or not) by id.</param>
    /// <param name="Builder">The builder chosen then.</param>
    private sealed record Sources(WorldSave World, IDisposable Held, List<int> Ids, HashSet<int> Deleted, List<NewObject> Added,
        Dictionary<int, NewObject> AddedById, long Builder);

    /// <summary>
    /// Everything a history file holds, taken from the world and the steps at one time, so it can be written later
    /// on another thread. No steps: the file is removed instead (and nothing is held).
    /// </summary>
    private sealed record Snapshot(string Path, string Key, string? Stamp, long SavedAt, Sources? Objects, List<KeptStep> Steps, int UndoCount, int RedoCount)
    {
        /// <summary>Lets the world go (again: only the first time counts).</summary>
        public void Release() => Objects?.Held.Dispose();
    }

    /// <summary>
    /// Writes the world's undo and redo steps (replacing the file in one move, so a crash leaves the old one);
    /// with no steps the file is removed. Waits for the writes <see cref="WriteLater"/> has queued first.
    /// </summary>
    public static void Write(WorldSession w, EditSession.Kept k)
    {
        Flush();
        var snapshot = Take(w, k);
        try
        {
            Save(snapshot);
        }
        finally
        {
            snapshot.Release();
        }
    }

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
                    Console.WriteLine($"History: could not keep it on disk: {ex.Message}");
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
                    Console.WriteLine($"History: no data kept for object {id}: {ex.Message}");
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

    /// <summary>Writes a snapshot to a file (the temporary one Save then moves in place).</summary>
    /// <param name="s">The snapshot.</param>
    /// <param name="things">The steps' objects, found (<see cref="Find"/>).</param>
    /// <param name="index">Each found object's place among <paramref name="things"/>, by id.</param>
    /// <param name="temp">The file.</param>
    private static void WriteFile(Snapshot s, List<KeptThing> things, Dictionary<int, int> index, string temp)
    {
        using var file = File.Create(temp);
        using var gz = new GZipStream(file, CompressionLevel.Fastest);
        using var o = new BinaryWriter(gz, Encoding.UTF8);
        o.Write(Magic);
        o.Write(Version);
        o.Write(s.Key);
        o.Write(s.Stamp != null);
        o.Write(s.Stamp ?? "");
        o.Write(s.SavedAt);
        o.Write(things.Count);
        foreach (var t in things)
        {
            o.Write(t.Prefab);
            o.Write(t.Piece);
            WriteVector(o, t.Position);
            WriteVector(o, t.Rotation);
            o.Write(t.Scale);
            o.Write(t.Bytes?.Length ?? -1);
            if (t.Bytes != null)
            {
                o.Write(t.Bytes);
            }
        }
        o.Write(s.UndoCount);
        o.Write(s.RedoCount);
        foreach (var c in s.Steps)
        {
            o.Write(c.Label);
            o.Write(c.Ticks);
            o.Write(c.Removed);
            o.Write(c.Applied);
            o.Write(c.RevertOf);
            // Each step's points and zones are in the frame of the area it was made in.
            var f = c.Frame;
            o.Write(c.Points.Length);
            foreach (int p in c.Points)
            {
                o.Write(p % f.W + f.Ox);
                o.Write(p / f.W + f.Oz);
            }
            WriteState(o, c.Before);
            WriteState(o, c.After);
            o.Write(c.Zones.Length);
            foreach (var (x, z) in c.Zones)
            {
                o.Write(x + f.X0);
                o.Write(z + f.Z0);
            }
            var kept = c.Things.Where(t => index.ContainsKey(t.Id)).ToList();
            o.Write(kept.Count);
            foreach (var (id, before, after) in kept)
            {
                o.Write(index[id]);
                o.Write(before);
                o.Write(after);
            }
            o.Write(c.Resets.Length);
            foreach (var (reset, before, after) in c.Resets)
            {
                o.Write(reset.X);
                o.Write(reset.Z);
                o.Write(reset.KeepBuildings);
                o.Write(reset.Ground);
                o.Write(before);
                o.Write(after);
            }
        }
    }

    /// <summary>Writes a vector as three floats.</summary>
    private static void WriteVector(BinaryWriter o, Vector3 v)
    {
        o.Write(v.X);
        o.Write(v.Y);
        o.Write(v.Z);
    }

    /// <summary>Reads a vector written by <c>WriteVector</c>.</summary>
    private static Vector3 ReadVector(BinaryReader i) => new(i.ReadSingle(), i.ReadSingle(), i.ReadSingle());

    /// <summary>
    /// Writes a step's ground values; the point count is written by the caller, so it is not repeated here.
    /// </summary>
    private static void WriteState(BinaryWriter o, Ground.State s)
    {
        foreach (float f in s.Level)
        {
            o.Write(f);
        }

        foreach (float f in s.Smooth)
        {
            o.Write(f);
        }

        o.Write(s.Mod);
        foreach (float f in s.Paint)
        {
            o.Write(f);
        }

        o.Write(s.PMod);
        foreach (float f in s.Lift)
        {
            o.Write(f);
        }
    }

    /// <summary>Reads ground values written by <c>WriteState</c> for <paramref name="n"/> points.</summary>
    private static Ground.State ReadState(BinaryReader i, int n)
    {
        // Each array is made once its bytes are read: a damaged count is refused at the end of the file, not
        // allocated first.
        float[] Floats(int count)
        {
            byte[] raw = i.ReadExactBytes(checked(count * 4));
            float[] a = new float[count];
            for (int k = 0; k < count; k++)
            {
                a[k] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(k * 4));
            }
            return a;
        }
        float[] level = Floats(n);
        float[] smooth = Floats(n);
        byte[] mod = i.ReadExactBytes(n);
        float[] paint = Floats(checked(n * 4));
        byte[] pmod = i.ReadExactBytes(n);
        float[] lift = Floats(n);
        return new Ground.State(level, smooth, mod, paint, pmod, lift);
    }

    /// <summary>A count from the file: never negative.</summary>
    /// <exception cref="InvalidDataException">A negative count (a damaged file).</exception>
    private static int ReadCount(BinaryReader i) =>
        i.ReadInt32() is int n and >= 0 ? n : throw new InvalidDataException("a negative count");

    /// <summary>
    /// A list's first capacity for a count from the file: the count, up to a few thousand (a damaged count past what
    /// the file holds is not allocated up front; the list grows with what is read).
    /// </summary>
    private static int Capacity(int count) => Math.Min(count, 4096);

    // ---- Reading.

    /// <summary>
    /// What was kept for this world, fitted to it as it is now: objects found again by kind and place,
    /// the others ready to come back.
    /// </summary>
    /// <param name="Kept">The undo and redo steps, with the objects' ids in the world as it is now.</param>
    /// <param name="Steps">How many steps were kept.</param>
    /// <param name="Changed">The world may differ from when they were kept (live, or a save made since).</param>
    /// <param name="SavedAt">When the history was written, local time.</param>
    public sealed record Restored(EditSession.Kept Kept, int Steps, bool Changed, DateTime SavedAt)
    {
        /// <summary>
        /// Not read from a file: the open world's history, fitted to a save the game made since, when the pending
        /// changes were brought onto it (WorldSession.Replay).
        /// </summary>
        public bool Replayed { get; init; }
    }

    /// <summary>The history kept for the world.</summary>
    /// <returns>Null when nothing was kept (or the file cannot be read).</returns>
    public static Restored? Read(WorldSession w)
    {
        Flush();
        string path = PathOf(KeyOf(w));
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return ReadFile(w, path);
        }
        // A damaged file (cut, or changed by hand: counts, ticks or indices out of range) is left out:
        // the world still opens, without the earlier history.
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException or ArgumentException
            or IndexOutOfRangeException or OutOfMemoryException or KeyNotFoundException or FormatException)
        {
            Console.WriteLine($"History: could not read {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads a history file and fits it to the world: objects found again by kind and place (to the centimetre),
    /// the others kept as gone objects an undo or redo brings back, and the points on one grid holding them all.
    /// </summary>
    /// <returns>Null when the file is for another world or layout version, or holds no steps.</returns>
    private static Restored? ReadFile(WorldSession w, string path)
    {
        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var i = new BinaryReader(gz, Encoding.UTF8);
        if (i.ReadInt32() != Magic || i.ReadInt32() != Version || i.ReadString() != KeyOf(w))
        {
            return null;
        }
        bool hasStamp = i.ReadBoolean();
        string stamp = i.ReadString();
        var savedAt = new DateTime(i.ReadInt64(), DateTimeKind.Utc);
        bool changed = !hasStamp || stamp != Stamp(w);
        // The world's objects by kind and place (to the centimetre: a save keeps them exactly).
        var world = new ObjectPlaces();
        foreach (var (id, prefab, p, _, _) in w.World.Objects)
        {
            world.Add(id, prefab, p);
        }
        foreach (var (id, prefab, p, _) in w.World.Pieces)
        {
            world.Add(id, prefab, p);
        }
        int count = ReadCount(i);
        var ids = new List<int>(Capacity(count));
        var gone = new List<NewObject>();
        for (int n = 0; n < count; n++)
        {
            int prefab = i.ReadInt32();
            i.ReadBoolean();
            var position = ReadVector(i);
            var rotation = ReadVector(i);
            float scale = i.ReadSingle();
            int length = i.ReadInt32();
            byte[]? bytes = length >= 0 ? i.ReadExactBytes(length) : null;
            if (world.Take(prefab, position) is int found)
            {
                ids.Add(found);
            }
            else
            {
                // Not in the world now (deleted, or added and undone): an undo or redo brings it back.
                var o = new NewObject(w.NextId(), prefab, position, rotation, scale, Fresh: bytes == null, Raw: bytes);
                ids.Add(o.Id);
                gone.Add(o);
            }
        }
        int undoCount = ReadCount(i), redoCount = ReadCount(i);
        var changes = new List<(EditSession.Change C, int RevertOf)>();
        var points = new List<(int X, int Z)[]>();
        var thingIds = new List<int[]>();
        for (int n = 0; n < undoCount + redoCount; n++)
        {
            string label = i.ReadString();
            var time = new DateTime(i.ReadInt64(), DateTimeKind.Local);
            bool removed = i.ReadBoolean(), applied = i.ReadBoolean();
            int revertOf = i.ReadInt32();
            int np = ReadCount(i);
            var pts = new List<(int X, int Z)>(Capacity(np));
            for (int k = 0; k < np; k++)
            {
                pts.Add((i.ReadInt32(), i.ReadInt32()));
            }
            var before = ReadState(i, np);
            var after = ReadState(i, np);
            int nz = ReadCount(i);
            var zones = new List<(int X, int Z)>(Capacity(nz));
            for (int k = 0; k < nz; k++)
            {
                zones.Add((i.ReadInt32(), i.ReadInt32()));
            }
            int nt = ReadCount(i);
            var things = new List<(int Index, bool Before, bool After)>(Capacity(nt));
            var tids = new List<int>(Capacity(nt));
            for (int k = 0; k < nt; k++)
            {
                int t = i.ReadInt32();
                tids.Add(ids[t]);
                things.Add((k, i.ReadBoolean(), i.ReadBoolean()));
            }
            int nr = ReadCount(i);
            var resets = new List<(ZoneReset, bool, bool)>(Capacity(nr));
            for (int k = 0; k < nr; k++)
            {
                resets.Add((new ZoneReset(i.ReadInt32(), i.ReadInt32(), i.ReadBoolean(), i.ReadBoolean()), i.ReadBoolean(), i.ReadBoolean()));
            }
            // Points are fitted below, once the grid holding all of them is known.
            var c = new EditSession.Change(label, [], before, after, zones)
            {
                Things = [.. things],
                Resets = [.. resets],
                Time = time,
                Removed = removed,
                Applied = applied || n < undoCount,
                Earlier = true,
            };
            changes.Add((c, revertOf));
            points.Add([.. pts]);
            thingIds.Add([.. tids]);
        }
        if (changes.Count == 0)
        {
            return null;
        }
        var all = points.SelectMany(p => p).ToList();
        int ox = all.Count > 0 ? all.Min(p => p.X) : 0, oz = all.Count > 0 ? all.Min(p => p.Z) : 0;
        int width = all.Count > 0 ? all.Max(p => p.X) - ox + 1 : 1;
        var fitted = new List<EditSession.Change>();
        var keptIds = new Dictionary<EditSession.Change, int[]>();
        for (int n = 0; n < changes.Count; n++)
        {
            var (c, revertOf) = changes[n];
            var f = c with
            {
                Points = [.. points[n].Select(p => (p.Z - oz) * width + (p.X - ox))],
                RevertOf = revertOf >= 0 && revertOf < n ? fitted[revertOf] : null,
            };
            fitted.Add(f);
            keptIds[f] = thingIds[n];
        }
        w.Edits.KeepGone(gone);
        var kept = new EditSession.Kept([.. fitted.Take(undoCount)], [.. fitted.Skip(undoCount)], ox, oz, width, 0, 0, keptIds);
        return new Restored(kept, changes.Count, changed, savedAt.ToLocalTime());
    }
}
