using System.Runtime.CompilerServices;
using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// Console output also goes to a log file in the data folder (a window app has no console on Windows),
/// each line with its time. Like BepInEx's LogOutput.log, the file is started over at each run: after a
/// problem it holds that run, for the person to attach to a bug report (Open log on the start page).
/// </summary>
public static partial class Log
{
    /// <summary>The log file: ValheimWorldEditor.log in the data folder.</summary>
    public static string FilePath => Path.Combine(AppSettings.DataDir, "ValheimWorldEditor.log");

    /// <summary>
    /// The log so far (it stays open for writing: read shared). When it cannot be read, a line saying why.
    /// </summary>
    public static string Read()
    {
        try
        {
            using var s = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch (Exception e)
        {
            Caught(e, "reading the log to show it");
            return $"(the log could not be read: {e.Message})";
        }
    }

    /// <summary>
    /// Writes an error that a catch handled to the log, with its stack trace: where it was caught (the source file, line
    /// and member, which the compiler fills in), what was being done, and the exception's type and message, inner ones
    /// included. Personal data is taken out first (<see cref="Scrub"/>); of the exception, only its type, message and
    /// stack trace are written, never its Data or the file names some exceptions keep.
    /// Every catch that handles an error calls this or <see cref="CaughtBrief"/>. A catch that rethrows leaves it to the
    /// one that handles the error, so each failure is written once. A cancellation the user asked for (Stop, Cancel) is
    /// not an error and is not written with these: its catch writes a plain line saying what stopped.
    /// </summary>
    /// <param name="e">What was caught.</param>
    /// <param name="doing">What was being done, to follow "while": "reading the settings".</param>
    /// <param name="file">The source file of the catch (the compiler's).</param>
    /// <param name="line">The line of the catch (the compiler's).</param>
    /// <param name="member">The member the catch is in (the compiler's).</param>
    public static void Caught(Exception e, string doing, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        => Write(Head(e, doing, file, line, member) + Scrub(StackOf(e)));

    /// <summary>
    /// As <see cref="Caught"/>, in one line without the stack trace (but with the inner exceptions' types and messages):
    /// for expected, frequent failures that would fill the log, such as a probe that finds nothing or a text the user typed
    /// that cannot be read.
    /// </summary>
    /// <param name="e">What was caught.</param>
    /// <param name="doing">What was being done, to follow "while": "parsing the typed seed".</param>
    /// <param name="file">The source file of the catch (the compiler's).</param>
    /// <param name="line">The line of the catch (the compiler's).</param>
    /// <param name="member">The member the catch is in (the compiler's).</param>
    public static void CaughtBrief(Exception e, string doing, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
    {
        var head = new StringBuilder(Head(e, doing, file, line, member));
        for (Exception? inner = e.InnerException; inner != null; inner = inner.InnerException)
        {
            head.Append(" ---> ").Append(inner.GetType().FullName).Append(": ").Append(OneLine(Scrub(inner.Message)));
        }
        Write(head.ToString());
    }

    /// <summary>
    /// The first line of a caught error: "Caught at File.cs:12 (Member) while doing: Type: message", the message on
    /// one line and scrubbed.
    /// </summary>
    private static string Head(Exception e, string doing, string file, int line, string member)
        => $"Caught at {LastPart(file)}:{line} ({member}) while {doing}: {e.GetType().FullName}: {OneLine(Scrub(e.Message))}";

    /// <summary>
    /// What <see cref="Exception.ToString"/> adds after the type and message: the inner exceptions and the stack trace.
    /// </summary>
    private static string StackOf(Exception e)
    {
        string all = e.ToString();
        string first = string.IsNullOrEmpty(e.Message) ? e.GetType().ToString() : e.GetType() + ": " + e.Message;
        return all.StartsWith(first, StringComparison.Ordinal) ? all[first.Length..] : Environment.NewLine + all;
    }

    /// <summary>Writes a line to the error output, which the log copies, and to a listening test.</summary>
    private static void Write(string text)
    {
        Console.Error.WriteLine(text);
        Listening.Value?.Add(text);
    }

    /// <summary>Text on one line: line breaks become spaces (they would also let a message forge log lines).</summary>
    private static string OneLine(string text) => text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// Starts the log (a new one each run), with what is running first: the editor's version, the system,
    /// .NET and the data folder (its home folder part as ~). Console output and errors then go both to the console and to
    /// the file. Failing to start the log is written to the error output, and the editor runs without the file.
    /// </summary>
    public static void Start(string version)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            // Versions before 1.15.5 wrote log.txt.
            File.Delete(Path.Combine(AppSettings.DataDir, "log.txt"));
            var file = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { AutoFlush = true };
            file.WriteLine($"Valheim World Editor {version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription} " +
                $"({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}), .NET {Environment.Version}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            file.WriteLine($"Data folder: {WithoutHome(AppSettings.DataDir)}");
            var sink = new Sink(file, Console.Error);
            Console.SetOut(new Tee(Console.Out, sink));
            Console.SetError(new Tee(Console.Error, sink));
        }
        catch (Exception e)
        {
            // There is no log file to write this into: the error output only.
            Console.Error.WriteLine($"The log could not be started: {e.GetType().FullName}: {OneLine(Scrub(e.Message))}");
        }
    }
}
