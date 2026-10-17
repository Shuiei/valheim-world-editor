using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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
    /// Keeps values out of the log from now on (a server's address, an account name, a password, a token):
    /// <see cref="Scrub"/> writes &lt;private&gt; in their place. Empty values and those shorter than three characters are
    /// left out, as they would take letters out of every word.
    /// </summary>
    /// <param name="values">The values; nulls are left out.</param>
    public static void Private(params ReadOnlySpan<string?> values)
    {
        lock (PrivateLock)
        {
            foreach (string? v in values)
            {
                if (v != null && v.Trim().Length >= 3)
                {
                    PrivateValues.Add(v);
                }
            }
        }
    }

    /// <summary>
    /// Text with personal data taken out, for the log: the values given to <see cref="Private"/>, web addresses,
    /// user@host names, file paths (a stack trace's source files keep only their name; a path with spaces that is not
    /// in quotes loses only its first part), IP addresses other than the local one, host names before a port, and the
    /// account's user name.
    /// </summary>
    public static string Scrub(string text)
    {
        string[] secrets;
        lock (PrivateLock)
        {
            secrets = [.. PrivateValues.OrderByDescending(v => v.Length)];
        }
        foreach (string v in secrets)
        {
            text = text.Replace(v, "<private>", StringComparison.Ordinal);
        }
        text = SourceFile().Replace(text, m => ") in " + LastPart(m.Groups[1].Value) + ":line ");
        text = WebAddress().Replace(text, "<url>");
        text = UserAtHost().Replace(text, "<user@host>");
        text = WithoutHome(text);
        text = QuotedPath().Replace(text, m => m.Groups[1].Value + "<path>" + m.Groups[1].Value);
        text = BarePath().Replace(text, m => "<path>" + m.Value[m.Value.TrimEnd('.', ':').Length..]);
        text = Ipv4().Replace(text, m => m.Value.StartsWith("127.", StringComparison.Ordinal) ? m.Value : "<ip>");
        text = Ipv6().Replace(text, m => m.Value == "[::1]" ? m.Value : "[<ip>]");
        text = HostPort().Replace(text, m => m.Groups[1].Value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? m.Value : "(<host>:" + m.Groups[2].Value + ")");
        string user = Environment.UserName;
        return user.Length >= 3 ? ReplaceWhole(text, user, "<user>", wordStart: true) : text;
    }

    /// <summary>
    /// Text with the home folder written ~ (in the other separator too on Windows, and whatever its case there), so no
    /// one's user name is in it.
    /// </summary>
    internal static string WithoutHome(string text)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/', '\\');
        if (home.Length < 2)
        {
            return text;
        }
        text = ReplaceWhole(text, home, "~", wordStart: false);
        return OperatingSystem.IsWindows() ? ReplaceWhole(text, home.Replace('\\', '/'), "~", wordStart: false) : text;
    }

    /// <summary>
    /// Records the lines <see cref="Caught"/> and <see cref="CaughtBrief"/> write in this flow of work, and in the work
    /// it starts, until disposed (they are still written): for tests, which run side by side.
    /// </summary>
    internal static Listener Listen() => new();

    /// <summary>The lines written while a test listens: see <see cref="Listen"/>.</summary>
    internal sealed class Listener : IDisposable
    {
        /// <summary>The listener this one was started inside, put back when it is disposed.</summary>
        private readonly Listener? _outer;

        /// <summary>The lines so far, guarded by itself (work started by the test may write from another thread).</summary>
        private readonly List<string> _lines = [];

        /// <summary>Starts listening in this flow of work.</summary>
        public Listener()
        {
            _outer = Listening.Value;
            Listening.Value = this;
        }

        /// <summary>The lines written so far.</summary>
        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        /// <summary>Takes a written line.</summary>
        public void Add(string text)
        {
            lock (_lines)
            {
                _lines.Add(text);
            }
        }

        /// <inheritdoc/>
        public void Dispose() => Listening.Value = _outer;
    }

    /// <summary>Values that must never reach the log: see <see cref="Private"/>.</summary>
    private static readonly HashSet<string> PrivateValues = new(StringComparer.Ordinal);

    /// <summary>Guards <see cref="PrivateValues"/>.</summary>
    private static readonly Lock PrivateLock = new();

    /// <summary>The test listening in this flow of work, if any: see <see cref="Listen"/>.</summary>
    private static readonly AsyncLocal<Listener?> Listening = new();

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

    /// <summary>The last part of a path, on either system's separators.</summary>
    private static string LastPart(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];

    /// <summary>
    /// Text with each whole <paramref name="value"/> replaced: not followed by a letter, digit, '_', '-' or a '.' that
    /// goes on with one; with <paramref name="wordStart"/>, not after one either. Case is ignored on Windows and for
    /// words.
    /// </summary>
    private static string ReplaceWhole(string text, string value, string with, bool wordStart)
    {
        StringComparison comparison = wordStart || OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var b = new StringBuilder();
        int from = 0;
        for (int i = text.IndexOf(value, comparison); i >= 0; i = text.IndexOf(value, i + value.Length, comparison))
        {
            int end = i + value.Length;
            bool goesOn = end < text.Length && (NameChar(text[end]) || (text[end] == '.' && end + 1 < text.Length && char.IsLetterOrDigit(text[end + 1])));
            if (goesOn || (wordStart && i > 0 && NameChar(text[i - 1])))
            {
                continue;
            }
            b.Append(text, from, i - from).Append(with);
            from = end;
        }
        return b.Append(text, from, text.Length - from).ToString();
    }

    /// <summary>Whether a character can be part of a name: a letter, a digit, '_' or '-'.</summary>
    private static bool NameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

    /// <summary>A stack frame's source file, in ") in /path/File.cs:line 12"; group 1 is the path.</summary>
    [GeneratedRegex(@"\) in ([^\r\n]+?):line (?=\d)")]
    private static partial Regex SourceFile();

    /// <summary>A web address (scheme://…).</summary>
    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9+.\-]*://[^\s'""<>()]+")]
    private static partial Regex WebAddress();

    /// <summary>user@host, or an email address.</summary>
    [GeneratedRegex(@"[\w.+\-]+@[\w\-]+(?:\.[\w\-]+)*")]
    private static partial Regex UserAtHost();

    /// <summary>A path in quotes (as .NET's file errors give them); group 1 is the quote.</summary>
    [GeneratedRegex(@"(['""])(?:~(?:[\\/][^'""\r\n]*)?|[A-Za-z]:[\\/][^'""\r\n]*|\\\\[^'""\r\n]+|/[^'""\r\n]+)\1")]
    private static partial Regex QuotedPath();

    /// <summary>A path not in quotes, to the first space; not one inside a word (and/or, 1/2) or already replaced.</summary>
    [GeneratedRegex(@"(?<![\w.~:/\\<>\-])(?:~(?!\w)|[A-Za-z]:[\\/]|\\\\(?=\w)|/(?=[\w.~\-]))[^\s'""<>|,;()\[\]]*")]
    private static partial Regex BarePath();

    /// <summary>An IPv4 address (not part of a version such as Version=1.0.0.0).</summary>
    [GeneratedRegex(@"(?<![\w.=])(?:\d{1,3}\.){3}\d{1,3}(?![\w.])")]
    private static partial Regex Ipv4();

    /// <summary>An IPv6 address in brackets.</summary>
    [GeneratedRegex(@"\[(?=[0-9A-Fa-f.]*:)[0-9A-Fa-f:.]+\]")]
    private static partial Regex Ipv6();

    /// <summary>"(host:port)", as socket errors end; group 1 is the host, 2 the port.</summary>
    [GeneratedRegex(@"\(([A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*):(\d{1,5})\)")]
    private static partial Regex HostPort();

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

    /// <summary>
    /// The file, written by both the output and the error streams: the time before each line. The first time the file
    /// cannot be written, that goes to the error output (<paramref name="error"/>, the one before the log started).
    /// </summary>
    private sealed class Sink(TextWriter file, TextWriter error)
    {
        /// <summary>Keeps lines from the two streams whole.</summary>
        private readonly Lock _lock = new();
        /// <summary>Whether the next text starts a line, so gets the time.</summary>
        private bool _lineStart = true;
        /// <summary>Whether the file failed to take a text already (said once: a full disk fails every line).</summary>
        private bool _failed;

        /// <summary>
        /// Writes text, the time (HH:mm:ss) before each line it starts; carriage returns are dropped (a Windows line end
        /// written in two parts made a line of its own). The first write error goes to the error output.
        /// </summary>
        public void Write(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            lock (_lock)
            {
                try
                {
                    string[] parts = text.Replace("\r", "").Split('\n');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i > 0)
                        {
                            file.Write('\n');
                            _lineStart = true;
                        }
                        if (parts[i].Length == 0)
                        {
                            continue;
                        }
                        if (_lineStart)
                        {
                            file.Write(DateTime.Now.ToString("HH:mm:ss "));
                        }
                        file.Write(parts[i]);
                        _lineStart = false;
                    }
                }
                catch (Exception e)
                {
                    // The log file is what failed: the error output only, the first time (the file is still tried with
                    // every text, in case it comes back).
                    if (!_failed)
                    {
                        _failed = true;
                        Said(() => error.WriteLine($"The log file could not be written: {e.GetType().FullName}: {OneLine(Scrub(e.Message))}"));
                    }
                }
            }
        }

        /// <summary>
        /// Writes to the error output when the log file has failed; if that fails too there is nowhere left to say it.
        /// </summary>
        private static void Said(Action write)
        {
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // Neither the log file nor the error output can be written: nowhere is left to write this failure.
            }
        }
    }

    /// <summary>
    /// A console stream that also writes into the log file. A console that cannot be written (none on Windows) is said
    /// in the log the first time, and the file still gets everything.
    /// </summary>
    private sealed class Tee(TextWriter console, Sink file) : TextWriter
    {
        /// <summary>Whether the console failed already (said once: it fails every text).</summary>
        private bool _consoleFailed;

        /// <inheritdoc/>
        public override Encoding Encoding => console.Encoding;

        /// <inheritdoc/>
        public override void Write(char value)
        {
            ToConsole(c => c.Write(value));
            file.Write(value.ToString());
        }

        /// <inheritdoc/>
        public override void Write(string? value)
        {
            ToConsole(c => c.Write(value));
            file.Write(value);
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            // Whole, not a character at a time (TextWriter's own goes through Write(char)).
            ToConsole(c => c.Write(buffer, index, count));
            file.Write(new string(buffer, index, count));
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<char> buffer)
        {
            string text = new(buffer);
            ToConsole(c => c.Write(text));
            file.Write(text);
        }

        /// <inheritdoc/>
        public override void WriteLine()
        {
            ToConsole(c => c.WriteLine());
            file.Write("\n");
        }

        /// <inheritdoc/>
        public override void WriteLine(string? value)
        {
            ToConsole(c => c.WriteLine(value));
            file.Write(value + "\n");
        }

        /// <summary>Writes to the console; the first time it fails, the log file says so.</summary>
        private void ToConsole(Action<TextWriter> write)
        {
            try
            {
                write(console);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or UnauthorizedAccessException)
            {
                if (!_consoleFailed)
                {
                    _consoleFailed = true;
                    file.Write($"The console could not be written ({e.GetType().FullName}: {OneLine(Scrub(e.Message))}); the log file still gets everything.\n");
                }
            }
        }
    }
}
