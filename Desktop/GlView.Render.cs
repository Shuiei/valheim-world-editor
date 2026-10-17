using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.OpenGL;

namespace TerrainEditor.Desktop;

// Drawing a frame: the passes in order, the lower 3D resolution's picture, the frame rate and its log, and the
// test driver's pictures and frame-rate measure. The frame is split over the GlView.Render.*.cs files.
public sealed partial class GlView
{
    /// <summary>The last frame-rate report (the test driver reads it).</summary>
    internal Stats? LastStats { get; private set; }
    /// <summary>
    /// Milliseconds on the view's clock: when it was last woken (<c>Wake</c>), the last frame, the last idle frame.
    /// </summary>
    private long _wokeAt, _lastFrame, _idleAt;

    // ---- Frame rate: shown by the window, and sampled every 0.2 s for the log when it is on.
    /// <summary>A frame-rate report.</summary>
    /// <param name="Fps">Frames over the last second.</param>
    /// <param name="WorkMs">Average time spent drawing a frame, in milliseconds.</param>
    /// <param name="Objects">The area's things.</param>
    /// <param name="Batches">Draw calls for the objects.</param>
    /// <param name="Instances">Copies drawn by those batches.</param>
    /// <param name="PendingModels">Models still being read.</param>
    public sealed record Stats(int Fps, double WorkMs, int Objects, int Batches, int Instances, int PendingModels);
    /// <summary>A new frame-rate report (twice a second while drawing).</summary>
    public event Action<Stats>? StatsChanged;
    /// <summary>The view's clock, in milliseconds.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    /// <summary>The frames of the last second: when, and how long the drawing took (ms).</summary>
    private readonly List<(long At, double Work)> _frames = [];
    /// <summary>When the last frame-rate report was made.</summary>
    private long _statsAt;
    /// <summary>The frame-rate log, null when it is off.</summary>
    public PerfLog? Perf { get; set; }

    // ---------------------------------------------------------------- drawing
    /// <summary>
    /// Draws a frame: a new area's ground and objects set up first, models that arrived uploaded (a few a frame),
    /// the camera moved by the keys, then the ground, objects, tools and water. Asks for the next frame while
    /// something happens.
    /// </summary>
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        long now = _clock.ElapsedMilliseconds;
        long start = Stopwatch.GetTimestamp();
        var s = Scene;
        DropLooks();
        PrepareScene(s);
        if (s != null)
        {
            RebuildDirty(s, now);
        }
        // A few models per frame, so the view keeps moving while they arrive.
        for (int i = 0; i < 12 && _ready.TryDequeue(out var r); i++)
        {
            Upload(r);
        }
        float dt = (now - _lastFrame) / 1000f;
        bool moving = MoveWithKeys(dt);
        _lastFrame = now;
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        int pw = Math.Max(1, (int)(Bounds.Width * scaling)), ph = Math.Max(1, (int)(Bounds.Height * scaling));
        // Fewer pixels than the screen's (3D resolution): drawn into a smaller picture, then stretched.
        var (rw, rh) = RenderSize();
        bool low = (rw < pw || rh < ph) && LowResolution(rw, rh);
        if (!low)
        {
            // Back to the screen's resolution (or the smaller picture could not be made): its picture goes.
            DropLowResolution();
            rw = pw;
            rh = ph;
        }
        BeginPicture(fb, low, rw, rh);
        var (view, eye) = CameraView();
        var proj = Perspective(60 * MathF.PI / 180, pw / (float)ph, 0.5f, 6000);
        // Half a tool line's width per metre from the eye: about 1.25 screen pixels, and never under
        // 0.9 pixel of the 3D view's own resolution (thinner lines break up when it is scaled up).
        _linePx = 2 * MathF.Tan(30 * MathF.PI / 180) * MathF.Max(1.25f / Math.Max(1, ph), 0.9f / Math.Max(1, rh));
        var vp = view * proj;
        _lastViewProj = vp;
        _lastEye = eye;
        bool camMoved = view != _lastView;
        _lastView = view;
        if (s != null)
        {
            UpdateBrush(s, vp, dt);
        }
        float time = _clock.ElapsedMilliseconds / 1000f;
        if (s != null && _terrainVao != 0)
        {
            DrawScene(s, vp, eye, time, rh);
        }
        EndPicture(low, fb, rw, rh, pw, ph);
        double work = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Measure(now, work, camMoved, rw, rh);
        Automate(now, pw, ph);
        CountGlErrors();
        // Full speed while something happens; the idle timer draws a few times a second otherwise.
        if (camMoved || moving || _brushDown || now - _wokeAt < 1000 || !_ready.IsEmpty || _pending > 0)
        {
            RequestNextFrameRendering();
        }
    }
}
