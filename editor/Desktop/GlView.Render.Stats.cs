using Avalonia.Threading;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The frame-rate report: the frames over the last second, the log line per frame, and the OpenGL errors the test driver checks.
public sealed partial class GlView
{
    /// <summary>Counts a frame for the frame rate and the log, and makes a report twice a second.</summary>
    private void Measure(long now, double work, bool camMoved, int pw, int ph)
    {
        Interlocked.Exchange(ref _lastFrameAt, now);
        _frames.Add((now, work));
        while (_frames.Count > 0 && now - _frames[0].At > 1000)
        {
            _frames.RemoveAt(0);
        }
        Perf?.Frame(now, work, camMoved, busy: now - _wokeAt < 1000, $"view {pw}×{ph} px · {_gl.GetStringS(StringName.Renderer)} · {Scene?.Name} · {Scene?.Things.Count} objects · area {Scene?.ZonesX}×{Scene?.ZonesZ} zones");
        if (now - _statsAt >= 500)
        {
            _statsAt = now;
            var st = new Stats(_frames.Count, _frames.Average(f => f.Work), Scene?.Things.Count ?? 0, _batches.Count, _batches.Sum(b => b.Instances), _pending);
            LastStats = st;
            Dispatcher.UIThread.Post(() => StatsChanged?.Invoke(st));
        }
    }

    /// <summary>Frames drawn over the last second.</summary>
    internal int FramesDrawn => _frames.Count;

    /// <summary>The test driver checks drawing raised no OpenGL error (counted only when driven).</summary>
    internal int GlErrors { get; private set; }

    /// <summary>Counts (and says) the OpenGL errors of the frame, only when the test driver runs.</summary>
    private void CountGlErrors()
    {
        if (!Options.Driver)
        {
            return;
        }
        for (var e = _gl.GetError(); e != GLEnum.NoError; e = _gl.GetError())
        {
            GlErrors++;
            Options.Say($"OpenGL error {e}");
        }
    }
}
