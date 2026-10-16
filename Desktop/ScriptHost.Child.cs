using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TerrainEditor.Desktop;

/// <summary>
/// Scripts run in a process of their own: the editor started again with <c>--script-host</c> (Serve), given the
/// compiled script and the area's snapshot on its input, answering with the changes, the script's error or "stopped"
/// on its output (RunInChild). Stop and the 2-minute limit close its input first, which ends a script at its loops'
/// next Check, and kill the process a second later when that was not enough: a call that cannot be interrupted (a
/// LINQ query over a huge range, Thread.Sleep, a deep recursion) ends with the process. A process that dies or
/// answers nothing is an error, never a wait. The format is the editor's own and has no version: both ends are the
/// same build. The process is not a sandbox: the script has the editor's rights, as any program the user runs.
/// </summary>
public static partial class ScriptHost
{
    /// <summary>
    /// A script's error as its process reported it: where it happened, the exception's kind and its message; the
    /// kind is Crash when the process died or answered nothing.
    /// </summary>
    /// <param name="where">"line 12: " when it happened in the script's own code, else empty.</param>
    /// <param name="kind">The exception's kind (ArgumentException…), or Crash when the process died.</param>
    /// <param name="message">The error's message.</param>
    /// <param name="inner">What showed the process died, if anything.</param>
    public sealed class ScriptException(string where, string kind, string message, Exception? inner = null) : Exception(message, inner)
    {
        /// <summary>"line 12: " when the error happened in the script's own code, else empty.</summary>
        public string Where { get; } = where;
        /// <summary>The exception's kind as the script threw it, or Crash when the process died.</summary>
        public string Kind { get; } = kind;
    }

    /// <summary>How long the script's process gets to end on its own after Stop, before it is killed.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(1);

    /// <summary>The first byte of the process's answer: what follows.</summary>
    private enum Answer : byte
    {
        /// <summary>The script's changes (WriteAnswer).</summary>
        Changes,
        /// <summary>The script's error: where, kind, message.</summary>
        Error,
        /// <summary>The script was stopped at a Check (its input closed).</summary>
        Stopped,
    }

    /// <summary>
    /// The program and arguments that start this editor as a script's process: the editor's own program when it runs
    /// as one (published), else the dotnet that runs it (as <c>dotnet ValheimWorldEditor.dll</c>, or inside the tests)
    /// with the editor's assembly.
    /// </summary>
    internal static (string File, string[] Arguments) ChildCommand()
    {
        string? exe = Environment.ProcessPath;
        bool dotnet = exe != null && Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (exe != null && !dotnet && Assembly.GetEntryAssembly() == typeof(ScriptHost).Assembly)
        {
            return (exe, ["--script-host"]);
        }
        string host = dotnet ? exe! : Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        return (host, [typeof(ScriptHost).Assembly.Location, "--script-host"]);
    }

    /// <summary>
    /// Runs the compiled script against the snapshot in a process of its own, with its new pieces snapped
    /// (SnapPieces). Cancelling ends the process: its input closed first (the script's loops end at their next
    /// Check), a kill a second later; then OperationCanceledException.
    /// </summary>
    /// <param name="image">The compiled assembly.</param>
    /// <param name="pdb">Its debug information, or null (errors then have no line).</param>
    /// <param name="snap">The area as the script sees it.</param>
    /// <param name="cancel">Stop, or the 2-minute limit.</param>
    /// <returns>What the script changed.</returns>
    /// <exception cref="ScriptException">The script's own error, with its line; or its process died (Crash).</exception>
    public static async Task<Changes> RunInChild(byte[] image, byte[]? pdb, Snapshot snap, CancellationToken cancel)
    {
        var (file, arguments) = ChildCommand();
        var start = new ProcessStartInfo(file) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in arguments)
        {
            start.ArgumentList.Add(a);
        }
        using var p = Process.Start(start) ?? throw new ScriptException("", "Crash", $"the script's process ({file}) did not start");
        // Its error output (a crash's report), read as it comes: a full pipe would block it.
        var errors = new StringBuilder();
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null && errors.Length < 2000)
            {
                errors.AppendLine(e.Data);
            }
        };
        p.BeginErrorReadLine();
        var answer = new MemoryStream();
        var reading = p.StandardOutput.BaseStream.CopyToAsync(answer, CancellationToken.None);
        string? unsent = null;
        try
        {
            try
            {
                // On a worker: a pipe holds little, and the process reads as it comes. Buffered: a pipe write is a
                // system call, and a snapshot holds hundreds of thousands of small values. The input stays open
                // (closing it is the stop signal), so the buffer is flushed and left, not disposed.
                await Task.Run(() =>
                {
                    var buffered = new BufferedStream(p.StandardInput.BaseStream, 1 << 16);
                    using var w = new BinaryWriter(buffered, Encoding.UTF8, leaveOpen: true);
                    WriteRequest(w, image, pdb, snap);
                    w.Flush();
                    buffered.Flush();
                }, CancellationToken.None);
            }
            catch (IOException e)
            {
                // The process died before it read the whole script: said with its exit below.
                unsent = e.Message;
            }
            await p.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            await Stop(p);
            throw;
        }
        finally
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
            }
        }
        await reading;
        answer.Position = 0;
        var ch = ReadAnswer(answer, p.ExitCode, unsent != null ? $"the script could not be sent ({unsent})\n{errors}" : errors.ToString());
        SnapPieces(snap, ch);
        return ch;
    }

    /// <summary>
    /// Stops the script's process: its input closed (the script's loops end at their next Check and the process
    /// answers "stopped"), and a kill of it and anything it started when it has not ended a second later.
    /// </summary>
    private static async Task Stop(Process p)
    {
        p.StandardInput.Close();
        await Task.WhenAny(p.WaitForExitAsync(), Task.Delay(Grace));
        if (!p.HasExited)
        {
            p.Kill(entireProcessTree: true);
            await p.WaitForExitAsync();
        }
    }

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
            catch (IOException)
            {
                // The pipe broke: the editor is gone, which is a stop too.
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
            w.Write((byte)Answer.Stopped);
        }
        catch (Exception ex)
        {
            // The script's own error, as the editor would have caught it: reported with its line.
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

    /// <summary>
    /// Reads the process's answer: the changes; the script's error or a stop thrown on; and no answer, or one cut
    /// short, as a Crash with the process's exit code and what it wrote on its error output.
    /// </summary>
    /// <param name="answer">What the process wrote on its output.</param>
    /// <param name="exitCode">How it ended.</param>
    /// <param name="errors">What it wrote on its error output.</param>
    internal static Changes ReadAnswer(Stream answer, int exitCode, string errors)
    {
        try
        {
            using var r = new BinaryReader(answer, Encoding.UTF8, leaveOpen: true);
            switch ((Answer)r.ReadByte())
            {
                case Answer.Changes:
                {
                    var ch = new Changes();
                    int heights = r.ReadInt32();
                    for (int i = 0; i < heights; i++)
                    {
                        ch.Heights[r.ReadInt32()] = r.ReadSingle();
                    }
                    int paints = r.ReadInt32();
                    for (int i = 0; i < paints; i++)
                    {
                        ch.Paint[r.ReadInt32()] = Floats(r);
                    }
                    int removes = r.ReadInt32();
                    for (int i = 0; i < removes; i++)
                    {
                        ch.Remove.Add(r.ReadInt32());
                    }
                    int adds = r.ReadInt32();
                    for (int i = 0; i < adds; i++)
                    {
                        ch.Add.Add((r.ReadInt32(), r.ReadString(), Vector(r), Vector(r), r.ReadSingle(), r.ReadBoolean()));
                    }
                    int moves = r.ReadInt32();
                    for (int i = 0; i < moves; i++)
                    {
                        ch.Moves[r.ReadInt32()] = (Vector(r), Vector(r), r.ReadSingle());
                    }
                    ch.Output.Append(r.ReadString());
                    ch.NoLimit = r.ReadBoolean();
                    return ch;
                }
                case Answer.Error:
                    throw new ScriptException(r.ReadString(), r.ReadString(), r.ReadString());
                case Answer.Stopped:
                    throw new OperationCanceledException("the script was stopped");
                default:
                    throw new InvalidDataException("the script's process answered something else");
            }
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException)
        {
            string said = errors.Trim();
            throw new ScriptException("", "Crash", $"the script's process ended without an answer (exit code {exitCode}){(said.Length > 0 ? ":\n" + said : "")}", e);
        }
    }

    /// <summary>A block of bytes: its length, then the bytes.</summary>
    private static void Block(BinaryWriter w, ReadOnlySpan<byte> bytes)
    {
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    /// <summary>Reads a block of bytes (Block).</summary>
    private static byte[] Block(BinaryReader r) => Exactly(r, r.ReadInt32());

    /// <summary>Reads this many bytes, all of them (a short read is the end of the stream).</summary>
    private static byte[] Exactly(BinaryReader r, int count)
    {
        byte[] bytes = new byte[count];
        r.BaseStream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Reads a block of floats (Block of their bytes).</summary>
    private static float[] Floats(BinaryReader r)
    {
        float[] a = new float[r.ReadInt32() / sizeof(float)];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(a.AsSpan()));
        return a;
    }

    /// <summary>Reads a block of ints (Block of their bytes).</summary>
    private static int[] Ints(BinaryReader r)
    {
        int[] a = new int[r.ReadInt32() / sizeof(int)];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(a.AsSpan()));
        return a;
    }

    /// <summary>Writes a vector's three floats.</summary>
    private static void Write(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }

    /// <summary>Reads a vector's three floats.</summary>
    private static Vector3 Vector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
