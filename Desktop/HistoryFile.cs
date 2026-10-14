using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Editing;

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

    /// <summary>Removes the world's history file; a file that cannot be removed is left as it is.</summary>
    public static void Delete(WorldSession w)
    {
        try
        {
            File.Delete(PathOf(KeyOf(w)));
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
    /// Writes the world's undo and redo steps (replacing the file in one move, so a crash leaves the old one);
    /// with no steps the file is removed.
    /// </summary>
    public static void Write(WorldSession w, EditSession.Kept k)
    {
        var all = k.Undo.Concat(k.Redo).ToList();
        if (all.Count == 0)
        {
            Delete(w);
            return;
        }
        // Every object the steps need, once: what it is, where, and its data.
        var ids = k.Ids.Values.SelectMany(v => v).Distinct().ToList();
        var found = WorldScene.FindThings(w.World, w.Edits, ids);
        var index = new Dictionary<int, int>();
        var things = new List<KeptThing>();
        foreach (int id in ids)
        {
            if (!found.TryGetValue(id, out var t))
            {
                continue;
            }
            byte[]? bytes = null;
            try
            {
                bytes = ObjectData.Bytes(w.World, w.Edits, id);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
            {
                Console.WriteLine($"History: no data kept for object {id}: {ex.Message}");
            }
            index[id] = things.Count;
            things.Add(new KeptThing(t.Prefab, t.Piece, t.Position, t.Rotation, t.Scale, bytes));
        }
        var order = all.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i);
        string path = PathOf(KeyOf(w));
        Directory.CreateDirectory(Folder);
        string temp = path + ".new";
        using (var file = File.Create(temp))
        using (var gz = new GZipStream(file, CompressionLevel.Fastest))
        using (var o = new BinaryWriter(gz, Encoding.UTF8))
        {
            o.Write(Magic);
            o.Write(Version);
            o.Write(KeyOf(w));
            string? stamp = Stamp(w);
            o.Write(stamp != null);
            o.Write(stamp ?? "");
            o.Write(DateTime.UtcNow.Ticks);
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
            o.Write(k.Undo.Count);
            o.Write(k.Redo.Count);
            foreach (var c in all)
            {
                o.Write(c.Label);
                o.Write(c.Time.Ticks);
                o.Write(c.Removed);
                o.Write(c.Applied);
                o.Write(c.RevertOf != null && order.TryGetValue(c.RevertOf, out int r) ? r : -1);
                // Each step's points and zones are in the frame of the area it was made in.
                var f = k.FrameOf(c);
                o.Write(c.Points.Length);
                foreach (int p in c.Points)
                {
                    o.Write(p % f.W + f.Ox);
                    o.Write(p / f.W + f.Oz);
                }
                WriteState(o, c.Before);
                WriteState(o, c.After);
                o.Write(c.Zones.Count);
                foreach (var (x, z) in c.Zones)
                {
                    o.Write(x + f.X0);
                    o.Write(z + f.Z0);
                }
                int[] cids = k.Ids[c];
                var kept = Enumerable.Range(0, cids.Length).Where(n => index.ContainsKey(cids[n])).ToList();
                o.Write(kept.Count);
                foreach (int n in kept)
                {
                    o.Write(index[cids[n]]);
                    o.Write(c.Things[n].Before);
                    o.Write(c.Things[n].After);
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
        File.Move(temp, path, overwrite: true);
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
        float[] Floats(int count)
        {
            float[] a = new float[count];
            for (int k = 0; k < count; k++)
            {
                a[k] = i.ReadSingle();
            }
            return a;
        }
        float[] level = Floats(n);
        float[] smooth = Floats(n);
        byte[] mod = i.ReadBytes(n);
        float[] paint = Floats(n * 4);
        byte[] pmod = i.ReadBytes(n);
        float[] lift = Floats(n);
        return new Ground.State(level, smooth, mod, paint, pmod, lift);
    }

    // ---- Reading.

    /// <summary>
    /// What was kept for this world, fitted to it as it is now: objects found again by kind and place,
    /// the others ready to come back.
    /// </summary>
    /// <param name="Kept">The undo and redo steps, with the objects' ids in the world as it is now.</param>
    /// <param name="Steps">How many steps were kept.</param>
    /// <param name="Changed">The world may differ from when they were kept (live, or a save made since).</param>
    /// <param name="SavedAt">When the history was written, local time.</param>
    public sealed record Restored(EditSession.Kept Kept, int Steps, bool Changed, DateTime SavedAt);

    /// <summary>The history kept for the world.</summary>
    /// <returns>Null when nothing was kept (or the file cannot be read).</returns>
    public static Restored? Read(WorldSession w)
    {
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
        static (int, long, long, long) Place(int prefab, Vector3 p) => (prefab, (long)MathF.Round(p.X * 100), (long)MathF.Round(p.Y * 100), (long)MathF.Round(p.Z * 100));
        var world = new Dictionary<(int, long, long, long), Queue<int>>();
        void Add(int id, int prefab, Vector3 p)
        {
            var key = Place(prefab, p);
            if (!world.TryGetValue(key, out var q))
            {
                world[key] = q = new Queue<int>();
            }
            q.Enqueue(id);
        }
        foreach (var (id, prefab, p, _, _) in w.World.Objects)
        {
            Add(id, prefab, p);
        }
        foreach (var (id, prefab, p, _) in w.World.Pieces)
        {
            Add(id, prefab, p);
        }
        int count = i.ReadInt32();
        int[] ids = new int[count];
        var gone = new List<NewObject>();
        for (int n = 0; n < count; n++)
        {
            int prefab = i.ReadInt32();
            i.ReadBoolean();
            var position = ReadVector(i);
            var rotation = ReadVector(i);
            float scale = i.ReadSingle();
            int length = i.ReadInt32();
            byte[]? bytes = length >= 0 ? i.ReadBytes(length) : null;
            if (world.TryGetValue(Place(prefab, position), out var q) && q.Count > 0)
            {
                ids[n] = q.Dequeue();
            }
            else
            {
                // Not in the world now (deleted, or added and undone): an undo or redo brings it back.
                var o = new NewObject(w.NextId(), prefab, position, rotation, scale, Fresh: bytes == null, Raw: bytes);
                ids[n] = o.Id;
                gone.Add(o);
            }
        }
        int undoCount = i.ReadInt32(), redoCount = i.ReadInt32();
        var changes = new List<(EditSession.Change C, int RevertOf)>();
        var points = new List<(int X, int Z)[]>();
        var thingIds = new List<int[]>();
        for (int n = 0; n < undoCount + redoCount; n++)
        {
            string label = i.ReadString();
            var time = new DateTime(i.ReadInt64(), DateTimeKind.Local);
            bool removed = i.ReadBoolean(), applied = i.ReadBoolean();
            int revertOf = i.ReadInt32();
            int np = i.ReadInt32();
            var pts = new (int X, int Z)[np];
            for (int k = 0; k < np; k++)
            {
                pts[k] = (i.ReadInt32(), i.ReadInt32());
            }
            var before = ReadState(i, np);
            var after = ReadState(i, np);
            int nz = i.ReadInt32();
            var zones = new List<(int X, int Z)>();
            for (int k = 0; k < nz; k++)
            {
                zones.Add((i.ReadInt32(), i.ReadInt32()));
            }
            int nt = i.ReadInt32();
            var things = new (int Index, bool Before, bool After)[nt];
            int[] tids = new int[nt];
            for (int k = 0; k < nt; k++)
            {
                int t = i.ReadInt32();
                tids[k] = ids[t];
                things[k] = (k, i.ReadBoolean(), i.ReadBoolean());
            }
            int nr = i.ReadInt32();
            var resets = new (ZoneReset, bool, bool)[nr];
            for (int k = 0; k < nr; k++)
            {
                resets[k] = (new ZoneReset(i.ReadInt32(), i.ReadInt32(), i.ReadBoolean(), i.ReadBoolean()), i.ReadBoolean(), i.ReadBoolean());
            }
            // Points are fitted below, once the grid holding all of them is known.
            var c = new EditSession.Change(label, [], before, after, zones)
            {
                Things = things,
                Resets = resets,
                Time = time,
                Removed = removed,
                Applied = applied || n < undoCount,
                Earlier = true,
            };
            changes.Add((c, revertOf));
            points.Add(pts);
            thingIds.Add(tids);
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
