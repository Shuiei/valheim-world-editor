namespace TerrainEditor.App;

/// <summary>
/// Recording the log's lines for a test that listens (the work it starts is listened to too).
/// </summary>
public static partial class Log
{
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
    /// <summary>The test listening in this flow of work, if any: see <see cref="Listen"/>.</summary>
    private static readonly AsyncLocal<Listener?> Listening = new();
}
