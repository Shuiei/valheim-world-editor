using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>Writes a snapshot to a file (the temporary one Save then moves in place), or to memory (<see cref="Carry"/>).</summary>
    /// <param name="s">The snapshot.</param>
    /// <param name="things">The steps' objects, found (<see cref="Find"/>).</param>
    /// <param name="index">Each found object's place among <paramref name="things"/>, by id.</param>
    /// <param name="to">Where it goes; left open.</param>
    private static void WriteTo(Snapshot s, List<KeptThing> things, Dictionary<int, int> index, Stream to)
    {
        using var gz = new GZipStream(to, CompressionLevel.Fastest, leaveOpen: true);
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
            // Version 3: whether the step came from an earlier session.
            o.Write(c.Earlier);
            // Each step's points and zones are in the frame of the area it was made in.
            var f = c.Frame;
            o.Write(c.Points.Length);
            foreach (int p in c.Points)
            {
                o.Write(p % f.W + f.Ox);
                o.Write(p / f.W + f.Oz);
            }
            // Version 3: the generated ground at each point, which the ground values are on.
            foreach (float b in c.Bases)
            {
                o.Write(b);
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
            // Version 2: the hidden objects (mods' data holders), as the things.
            var hidden = c.Hidden.Where(t => index.ContainsKey(t.Id)).ToList();
            o.Write(hidden.Count);
            foreach (var (id, before, after) in hidden)
            {
                o.Write(index[id]);
                o.Write(before);
                o.Write(after);
            }
        }
        // Version 3: the No limit ground of the zones the files hold as ground discs.
        WriteLifted(o, s.Lifted);
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
        float[] level = ReadFloats(i, n);
        float[] smooth = ReadFloats(i, n);
        byte[] mod = i.ReadExactBytes(n);
        float[] paint = ReadFloats(i, checked(n * 4));
        byte[] pmod = i.ReadExactBytes(n);
        float[] lift = ReadFloats(i, n);
        return new Ground.State(level, smooth, mod, paint, pmod, lift);
    }

    /// <summary>
    /// Reads <paramref name="count"/> floats. The array is made once their bytes are read: a damaged count is refused
    /// at the end of the file, not allocated first.
    /// </summary>
    private static float[] ReadFloats(BinaryReader i, int count)
    {
        byte[] raw = i.ReadExactBytes(checked(count * 4));
        float[] a = new float[count];
        for (int k = 0; k < count; k++)
        {
            a[k] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(k * 4));
        }
        return a;
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
            using var file = File.OpenRead(path);
            return ReadFrom(w, file, new Fitting(BaseHeights(w), new Dictionary<(int, int), ZoneReset>(), Carried: false));
        }
        // A damaged file (cut, or changed by hand: counts, ticks or indices out of range) is left out:
        // the world still opens, without the earlier history.
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException or ArgumentException
            or IndexOutOfRangeException or OutOfMemoryException or KeyNotFoundException or FormatException)
        {
            Log.Caught(ex, "reading the world's history file (the world opens without the earlier history)");
            return null;
        }
    }
}
