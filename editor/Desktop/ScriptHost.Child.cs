using System.Diagnostics;
using System.Text;
using TerrainEditor.App;

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
        // Not a command injection (the scanners' rule is wrong here): the program is this editor or the dotnet running
        // it, and the arguments are its assembly's path and a fixed switch, passed one by one with no shell. No user or
        // script text is on the command line: the script goes through the input pipe.
        using var p = Process.Start(start) ?? throw new ScriptException("", "Crash", $"the script's process ({file}) did not start"); // nosemgrep
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
        // On a worker: a pipe holds little, and the process reads as it comes. Buffered: a pipe write is a system
        // call, and a snapshot holds hundreds of thousands of small values. The input stays open (closing it is the
        // stop signal), so the buffer is flushed and left, not disposed.
        var sending = Task.Run(() =>
        {
            var buffered = new BufferedStream(p.StandardInput.BaseStream, 1 << 16);
            using var w = new BinaryWriter(buffered, Encoding.UTF8, leaveOpen: true);
            WriteRequest(w, image, pdb, snap);
            w.Flush();
            buffered.Flush();
        }, CancellationToken.None);
        try
        {
            try
            {
                // Stop is heard while the request is still going out too (a process that stopped reading it).
                await sending.WaitAsync(cancel);
            }
            catch (IOException e)
            {
                // The process died before it read the whole script: said with its exit below.
                Log.CaughtBrief(e, "sending the script to its process");
                unsent = e.Message;
            }
            await p.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            await Stop(p);
            await Sent(sending);
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
        int exit = p.ExitCode;
        string report = unsent != null ? $"the script could not be sent ({unsent})\n{errors}" : errors.ToString();
        // On a worker, as the script ran: snapping thousands of new pieces takes a while, and the window must stay
        // free for Stop, which the snapping checks.
        return await Task.Run(() =>
        {
            var ch = ReadAnswer(answer, exit, report);
            SnapPieces(snap, ch, cancel);
            return ch;
        }, CancellationToken.None);
    }

    /// <summary>
    /// Waits for a request still being sent when the script was stopped: it ends once the process has (Stop), with
    /// the pipe broken or closed under it, which is the stop itself and not an error of its own.
    /// </summary>
    private static async Task Sent(Task sending)
    {
        try
        {
            await sending;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The process is gone, or its input was closed by Stop: the request no longer matters (brief: the stop's
            // own doing).
            Log.CaughtBrief(e, "finishing sending a stopped script");
        }
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

    /// <summary>A block of bytes: its length, then the bytes.</summary>
    private static void Block(BinaryWriter w, ReadOnlySpan<byte> bytes)
    {
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    /// <summary>Reads a block of bytes (Block).</summary>
    private static byte[] Block(BinaryReader r) => Exactly(r, r.ReadInt32());
}
