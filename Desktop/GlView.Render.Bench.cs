namespace TerrainEditor.Desktop;

// The frame-rate measure of the test driver (--bench): the camera turns for a while, then the rates are given back.
public sealed partial class GlView
{
    /// <summary>A frame-rate measure under way.</summary>
    private sealed class Bench
    {
        /// <summary>How long the camera turns.</summary>
        public required double Seconds { get; init; }
        /// <summary>When it started, on the view's clock (-1 until the first frame).</summary>
        public long From { get; set; } = -1;
        /// <summary>When each frame was drawn.</summary>
        public List<long> Frames { get; } = [];
        /// <summary>The frame rates, as a line of text.</summary>
        public TaskCompletionSource<string> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>The frame-rate measure under way, null when none.</summary>
    private volatile Bench? _bench;

    /// <summary>The camera turns on its own for this many seconds; the frame rates come back.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The seconds are negative or not a number.</exception>
    internal Task<string> Benchmark(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "the seconds must be 0 or more");
        }
        var b = new Bench { Seconds = seconds };
        _bench = b;
        Wake();
        return b.Done.Task;
    }

    /// <summary>Turns the camera for the measure, and gives back its rates once it has run long enough.</summary>
    /// <param name="now">The view's clock, in milliseconds.</param>
    /// <param name="pw">The screen's width in pixels.</param>
    /// <param name="ph">The screen's height in pixels.</param>
    private void AutomateBench(long now, int pw, int ph)
    {
        if (_bench is { } bench)
        {
            if (bench.From < 0)
            {
                bench.From = now;
            }
            bench.Frames.Add(now);
            lock (_camLock)
            {
                _yaw += 0.01f;
            }
            // Two frames at least, apart in time: the gaps between frames and the rate need them.
            if (bench.Frames.Count >= 2 && now > bench.From && now - bench.From > bench.Seconds * 1000)
            {
                _bench = null;
                var gaps = bench.Frames.Zip(bench.Frames.Skip(1), (a, b) => b - a).OrderBy(g => g).ToList();
                bench.Done.SetResult($"{bench.Frames.Count / ((now - bench.From) / 1000.0):0} fps over {(now - bench.From) / 1000.0:0.0} s, work {_frames.Average(f => f.Work):0.0} ms, gaps median {gaps[gaps.Count / 2]} ms, 95% {gaps[gaps.Count * 95 / 100]} ms, max {gaps[^1]} ms, view {pw}×{ph} px, {_drawnInstances:N0} of {_batches.Sum(b => b.Instances):N0} instances drawn in the last frame, {_drawCalls:N0} draw calls, {_batches.Count:N0} batches");
            }
            RequestNextFrameRendering();
        }
    }
}
