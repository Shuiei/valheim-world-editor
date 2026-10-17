using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// The streams that write the console output and errors into the log file, with the time before each line.
/// </summary>
public static partial class Log
{
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
