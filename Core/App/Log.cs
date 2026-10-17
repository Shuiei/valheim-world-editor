
namespace TerrainEditor.App;

/// <summary>
/// Console output also goes to a log file in the data folder (a window app has no console on Windows),
/// each line with its time. Like BepInEx's LogOutput.log, the file is started over at each run: after a
/// problem it holds that run, for the person to attach to a bug report (Open log on the start page).
/// </summary>
public static class Log
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
            return $"(the log could not be read: {e.Message})";
        }
    }

    /// <summary>
    /// Starts the log (a new one each run), with what is running first: the editor's version, the system,
    /// .NET and the data folder. Console output and errors then go both to the console and to the file.
    /// Failing to start the log is ignored.
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
            file.WriteLine($"Data folder: {AppSettings.DataDir}");
            var sink = new Sink(file);
            Console.SetOut(new Tee(Console.Out, sink));
            Console.SetError(new Tee(Console.Error, sink));
        }
        catch
        {
            // The editor runs without a log file if it cannot be started.
        }
    }

    /// <summary>The file, written by both the output and the error streams: the time before each line.</summary>
    private sealed class Sink(TextWriter file)
    {
        /// <summary>Keeps lines from the two streams whole.</summary>
        private readonly Lock _lock = new();
        /// <summary>Whether the next text starts a line, so gets the time.</summary>
        private bool _lineStart = true;

        /// <summary>
        /// Writes text, the time (HH:mm:ss) before each line it starts; carriage returns are dropped (a Windows line end
        /// written in two parts made a line of its own); write errors are ignored.
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
                catch
                {
                    // A log file that cannot be written cannot report its own failure; the console still gets the text.
                }
            }
        }
    }

    /// <summary>
    /// A console stream that also writes into the log file; a console that cannot be written (none on
    /// Windows) is ignored.
    /// </summary>
    private sealed class Tee(TextWriter console, Sink file) : TextWriter
    {
        /// <inheritdoc/>
        public override System.Text.Encoding Encoding => console.Encoding;

        /// <inheritdoc/>
        public override void Write(char value)
        {
            try
            {
                console.Write(value);
            }
            catch
            {
                // The console may be closed or redirected; the log file still gets the text.
            }
            file.Write(value.ToString());
        }

        /// <inheritdoc/>
        public override void Write(string? value)
        {
            try
            {
                console.Write(value);
            }
            catch
            {
                // The console may be closed or redirected; the log file still gets the text.
            }
            file.Write(value);
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            // Whole, not a character at a time (TextWriter's own goes through Write(char)).
            try { console.Write(buffer, index, count); } catch { }
            file.Write(new string(buffer, index, count));
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<char> buffer)
        {
            try { console.Write(buffer); } catch { }
            file.Write(new string(buffer));
        }

        /// <inheritdoc/>
        public override void WriteLine()
        {
            try { console.WriteLine(); } catch { }
            file.Write("\n");
        }

        /// <inheritdoc/>
        public override void WriteLine(string? value)
        {
            try
            {
                console.WriteLine(value);
            }
            catch
            {
                // The console may be closed or redirected; the log file still gets the text.
            }
            file.Write(value + "\n");
        }
    }
}
