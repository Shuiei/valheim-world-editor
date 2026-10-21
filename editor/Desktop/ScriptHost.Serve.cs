using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The script's process side: reading the request, running the script, and writing its answer.
public static partial class ScriptHost
{
    /// <summary>
    /// <c>--script-host</c>: runs the script given on the input (WriteRequest) and answers on the output (WriteAnswer:
    /// the changes, the script's error, or stopped). The input closing meanwhile is the editor's Stop: the script's
    /// loops end at their next Check. Nothing else of the editor starts.
    /// </summary>
    /// <returns>
    /// The process's exit code: 0 once it has answered (the answer flushed). The caller ends the process with it, so
    /// that a thread the script left running cannot keep it alive.
    /// </returns>
    internal static int Serve()
    {
        // Buffered: a pipe read is a system call, and the request holds hundreds of thousands of small values.
        using var input = new BufferedStream(Console.OpenStandardInput(), 1 << 16);
        using var output = Console.OpenStandardOutput();
        byte[] image;
        byte[]? pdb;
        Snapshot snap;
        using (var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
        {
            (image, pdb, snap) = ReadRequest(r);
        }
        // Not disposed: the input's watcher cancels it when the editor closes the input, which it may do after the
        // answer, as this process ends.
        var stop = new CancellationTokenSource();
        var watcher = new Thread(() =>
        {
            try
            {
                while (input.ReadByte() >= 0)
                {
                }
            }
            catch (IOException e)
            {
                // The pipe broke: the editor is gone, which is a stop too.
                Log.CaughtBrief(e, "waiting for the editor's stop");
            }
            stop.Cancel();
        })
        { IsBackground = true, Name = "script stop" };
        watcher.Start();
        // Only the answer goes on the output: a script's own Console output goes to the error output (shown with a
        // crash), not in front of the answer.
        Console.SetOut(Console.Error);
        using var w = new BinaryWriter(new BufferedStream(output), Encoding.UTF8);
        try
        {
            WriteAnswer(w, Execute(image, pdb, snap, stop.Token));
        }
        catch (OperationCanceledException)
        {
            // The user's Stop: not an error, and the editor writes the line for it.
            w.Write((byte)Answer.Stopped);
        }
        catch (Exception ex)
        {
            // The script's own error, as the editor would have caught it: reported with its line (and logged by the
            // editor, which gets it as a ScriptException).
            w.Write((byte)Answer.Error);
            w.Write(Where(ex));
            w.Write(ex.GetType().Name);
            w.Write(ex.Message);
        }
        return 0;
    }

    /// <summary>Writes the script and the area for Serve.</summary>
    internal static void WriteRequest(BinaryWriter w, byte[] image, byte[]? pdb, Snapshot s)
    {
        Block(w, image);
        w.Write(pdb?.Length ?? -1);
        if (pdb != null)
        {
            w.Write(pdb);
        }
        w.Write(s.W);
        w.Write(s.H);
        w.Write(s.Ox);
        w.Write(s.Oz);
        Block(w, MemoryMarshal.AsBytes(s.Heights.AsSpan()));
        Block(w, MemoryMarshal.AsBytes(s.Original.AsSpan()));
        Block(w, MemoryMarshal.AsBytes(s.Biomes.AsSpan()));
        Block(w, s.Mask);
        Block(w, MemoryMarshal.AsBytes(s.Locked.AsSpan()));
        w.Write(s.Things.Count);
        foreach (var (index, prefab, position, kind, piece, rotation, scale) in s.Things)
        {
            w.Write(index);
            w.Write(prefab);
            Write(w, position);
            w.Write(kind);
            w.Write(piece);
            Write(w, rotation);
            w.Write(scale);
        }
        w.Write(s.Water);
        w.Write(s.Seed);
        w.Write(s.World);
        int[] creatable = [.. s.Creatable];
        Block(w, MemoryMarshal.AsBytes(creatable.AsSpan()));
    }

    /// <summary>Reads what WriteRequest wrote.</summary>
    internal static (byte[] Image, byte[]? Pdb, Snapshot Snapshot) ReadRequest(BinaryReader r)
    {
        byte[] image = Block(r);
        int pdbLength = r.ReadInt32();
        byte[]? pdb = pdbLength < 0 ? null : Exactly(r, pdbLength);
        int w = r.ReadInt32(), h = r.ReadInt32();
        float ox = r.ReadSingle(), oz = r.ReadSingle();
        float[] heights = Floats(r), original = Floats(r);
        int[] biomes = Ints(r);
        byte[] mask = Block(r);
        bool[] locked = new bool[r.ReadInt32()];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(locked.AsSpan()));
        int count = r.ReadInt32();
        var things = new List<(int Index, string Prefab, Vector3 Position, string Kind, bool Piece, Vector3 Rotation, float Scale)>(count);
        for (int i = 0; i < count; i++)
        {
            things.Add((r.ReadInt32(), r.ReadString(), Vector(r), r.ReadString(), r.ReadBoolean(), Vector(r), r.ReadSingle()));
        }
        float water = r.ReadSingle();
        int seed = r.ReadInt32();
        string world = r.ReadString();
        return (image, pdb, new Snapshot(w, h, ox, oz, heights, original, biomes, mask, locked, things, water, seed, world, [.. Ints(r)]));
    }

    /// <summary>Writes the script's changes, after the Changes byte.</summary>
    internal static void WriteAnswer(BinaryWriter w, Changes ch)
    {
        w.Write((byte)Answer.Changes);
        w.Write(ch.Heights.Count);
        foreach (var (p, height) in ch.Heights)
        {
            w.Write(p);
            w.Write(height);
        }
        w.Write(ch.Paint.Count);
        foreach (var (p, colour) in ch.Paint)
        {
            w.Write(p);
            Block(w, MemoryMarshal.AsBytes(colour.AsSpan()));
        }
        w.Write(ch.Remove.Count);
        foreach (int i in ch.Remove)
        {
            w.Write(i);
        }
        w.Write(ch.Add.Count);
        foreach (var (prefab, name, position, rotation, scale, snap) in ch.Add)
        {
            w.Write(prefab);
            w.Write(name);
            Write(w, position);
            Write(w, rotation);
            w.Write(scale);
            w.Write(snap);
        }
        w.Write(ch.Moves.Count);
        foreach (var (i, m) in ch.Moves)
        {
            w.Write(i);
            Write(w, m.Position);
            Write(w, m.Rotation);
            w.Write(m.Scale);
        }
        w.Write(ch.Output.ToString());
        w.Write(ch.NoLimit);
    }
}
