using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing a frame: the passes in order, the lower 3D resolution's picture, the frame rate and its log, and the
// test driver's pictures and frame-rate measure.
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
        if (s != null && _sceneDirty)
        {
            _sceneDirty = false;
            // Another area: the last one's objects go (the models stay on the graphics card, unless
            // they are past the budget).
            DropObjects();
            if (_gpuBytes > GpuBudget)
            {
                DropModels();
            }
            BuildTerrain(s);
            StartModels(s);
        }
        if (s != null && _look == null && _lookFiles is { } lf && _terrainVao != 0)
        {
            var made = new GameLookGl();
            try
            {
                made.Init(_gl, Link, lf, s);
                _look = made;
            }
            catch (Exception ex)
            {
                made.Delete();
                _lookFiles = null;
                Status?.Invoke(Said.Of("world.msg.lookSetupFailed", ex.Message));
            }
        }
        if (s != null && _thingsReset)
        {
            _thingsReset = false;
            DropObjects();
            StartModels(s);
        }
        if (s != null)
        {
            Group[] dirty;
            lock (_dirtyGroups)
            {
                dirty = [.. _dirtyGroups.Where(g => g.Ready)];
                _dirtyGroups.ExceptWith(dirty);
            }
            foreach (var g in dirty)
            {
                Rebuild(s, g);
            }
            if (_shapesDirty || _shapesGeneration != ModExtensions.Generation)
            {
                RebuildShapes(s);
            }
            // Ground that changed moves the overlays lying on it (zone borders, rings): built again then
            // too, a few times a second at most while a brush stroke goes on.
            if (_overlaysDirty && _terrainVao != 0 && (!_overlaysGroundOnly || now - _overlaysBuiltAt >= 200))
            {
                _overlaysDirty = _overlaysGroundOnly = false;
                _overlaysBuiltAt = now;
                Overlays.Built o;
                lock (s.Things)
                {
                    o = Overlays.Build(s, s.Modifiers, i => NameOf(s.Things[i].Prefab));
                }
                UploadOverlays(o);
                Dispatcher.UIThread.Post(() => OverlaysBuilt?.Invoke(o));
            }
            else if (_overlaysDirty)
            {
                // Waiting for the 200 ms: another frame then.
                RequestNextFrameRendering();
            }
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
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, low ? _lowFbo : (uint)fb);
        _gl.Viewport(0, 0, (uint)rw, (uint)rh);
        _gl.ClearColor(0.42f, 0.58f, 0.74f, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        Matrix4x4 view, proj;
        Vector3 eye;
        lock (_camLock)
        {
            if (_eye == EyeMode.Orbit)
            {
                eye = _target + _distance * new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
                view = Matrix4x4.CreateLookAt(eye, _target, Vector3.UnitY);
                GameLookGl.FogStart = _distance;
            }
            else
            {
                GameLookGl.FogStart = 0;
                eye = _eyePos;
                view = Matrix4x4.CreateLookAt(eye, eye + Forward, Vector3.UnitY);
            }
        }
        proj = Perspective(60 * MathF.PI / 180, pw / (float)ph, 0.5f, 6000);
        // Half a tool line's width per metre from the eye: about 1.25 screen pixels, and never under
        // 0.9 pixel of the 3D view's own resolution (thinner lines break up when it is scaled up).
        _linePx = 2 * MathF.Tan(30 * MathF.PI / 180) * MathF.Max(1.25f / Math.Max(1, ph), 0.9f / Math.Max(1, rh));
        var vp = view * proj;
        _lastViewProj = vp;
        _lastEye = eye;
        bool camMoved = view != _lastView;
        _lastView = view;
        // The brush: where it is on the ground, a step of the stroke while the button is held, and the
        // changed ground to the graphics card.
        if (s != null)
        {
            _hover = (Tool != null || Mode is ToolMode.Shape or ToolMode.Mountain or ToolMode.Path || Mode == ToolMode.Place && Place?.Tool.Mode == PlaceTool.Modes.Brush) && _pointer is Point at ? GroundAt(s, vp, at, _surfaceSize) : null;
            if (_brushDown && _hover is { } hv)
            {
                s.Session?.StrokeStep(hv.X, hv.Z, dt);
            }
            if (s.Session?.TakeDirty() is { } d && _terrainVao != 0)
            {
                UpdateTerrain(s, d);
                // The mountain preview and the overlays stand on the ground as it is now.
                _previewKey = null;
                if (!_overlaysDirty)
                {
                    _overlaysDirty = _overlaysGroundOnly = true;
                }
            }
        }
        var sun = GameLookGl.SunDirView;
        float time = _clock.ElapsedMilliseconds / 1000f;
        // The game's look, unless switched off in View.
        var look = _gameLookOn ? _look : null;
        if (s != null && _terrainVao != 0)
        {
            if (look != null)
            {
                look.DrawSky(vp, eye, time);
                look.UseTerrain(vp, eye, time, slope: _slope, contour: ContourStep, limit: _limit);
                // The game's mesh is seen from both sides (look.js: DoubleSide).
                _gl.Disable(EnableCap.CullFace);
            }
            else
            {
                _gl.Enable(EnableCap.CullFace);
                _gl.UseProgram(_terrainProg);
                _gl.UniformMatrix4(_u.At(_terrainProg, "uViewProj"), 1, false, (float*)&vp);
                _gl.Uniform3(_u.At(_terrainProg, "uSun"), sun.X, sun.Y, sun.Z);
            }
            _gl.BindVertexArray(_terrainVao);
            _gl.DrawElements(PrimitiveType.Triangles, _terrainIndexCount, DrawElementsType.UnsignedInt, (void*)0);

            _gl.UseProgram(_objectProg);
            _gl.UniformMatrix4(_u.At(_objectProg, "uViewProj"), 1, false, (float*)&vp);
            _gl.Uniform3(_u.At(_objectProg, "uSun"), sun.X, sun.Y, sun.Z);
            void V3(string n, Vector3 v) => _gl.Uniform3(_u.At(_objectProg, n), v.X, v.Y, v.Z);
            V3("uSunColor", GameLookGl.SunColor);
            V3("uAmbient", GameLookGl.Ambient);
            V3("uFogColor", GameLookGl.FogColor);
            V3("uSunFogColor", GameLookGl.SunFogColor);
            _gl.Uniform1(_u.At(_objectProg, "uFogDensity"), GameLookGl.FogDensity);
            _gl.Uniform1(_u.At(_objectProg, "uFogStart"), GameLookGl.FogStart);
            V3("uEye", eye);
            var u = MaterialUniforms.Of(_u, _objectProg);
            _gl.Uniform1(_u.At(_objectProg, "uMap"), 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
            bool seeThrough = _seeThrough;
            int uSee = _u.At(_objectProg, "uSeeThrough");
            _gl.Uniform2(_u.At(_objectProg, "uCut"), CutY != null ? 1f : 0f, CutY ?? 0f);
            if (_cellScene == s)
            {
                SeeCells(vp, eye);
            }
            int drawn = 0;
            _drawCalls = 0;
            // Opaque first; see-through buildings after, blended over everything and not hiding
            // what is behind them; see-through materials (glass) last (DrawGlass).
            foreach (bool pass in seeThrough ? new[] { false, true } : [false])
            {
                if (pass)
                {
                    GlBlend.Over(_gl);
                    _gl.DepthMask(false);
                    _gl.Uniform1(uSee, 1f);
                }
                foreach (var b in _batches)
                {
                    if (!_shown[(int)b.Kind] || seeThrough && (b.Kind == ObjectKind.Buildings) != pass || !pass && b.Material.Transparent)
                    {
                        continue;
                    }
                    UseMaterial(b, u);
                    _gl.BindVertexArray(b.Vao);
                    drawn += DrawCells(b);
                }
                if (pass)
                {
                    _gl.Uniform1(uSee, 0f);
                    _gl.DepthMask(true);
                    _gl.Disable(EnableCap.Blend);
                }
            }

            if (_shown[(int)ObjectKind.Dungeons])
            {
                DrawRooms(s);
            }
            if (_shown[(int)ObjectKind.Ruins])
            {
                DrawLocations(s);
            }
            _drawnInstances = drawn + DrawGlass(u, seeThrough);
            // The water before the previews and the tools' marks, so they show over it.
            if (ShowWater)
            {
                if (look != null)
                {
                    look.DrawWater(vp, eye, time);
                }
                else
                {
                    _gl.Disable(EnableCap.CullFace);
                    GlBlend.Over(_gl);
                    _gl.DepthMask(false);
                    _gl.UseProgram(_waterProg);
                    _gl.UniformMatrix4(_u.At(_waterProg, "uViewProj"), 1, false, (float*)&vp);
                    _gl.BindVertexArray(_waterVao);
                    _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
                    _gl.DepthMask(true);
                    _gl.Disable(EnableCap.Blend);
                }
            }
            _ghostsDrawn = DrawGhosts(s);
            DrawOverlays(vp);
            DrawSelection(vp);
            DrawHoverObject(vp);
            DrawBrush(s, vp);
            DrawLasso(s, vp);
            DrawGizmo(s, vp);
            DrawMeasure(s, vp);
            DrawPlayers(s, vp);
            DrawPath(s, vp);
            DrawArea(s, vp);
            DrawPlace(s, vp);
            DrawDungeon(s, vp);
            // Over everything, the water too.
            DrawSpawnMarkers(s, vp);
            if (ShowNewMarkers)
            {
                DrawNewMarkers(s, vp, rh);
            }
        }
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
        if (low)
        {
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _lowFbo);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)fb);
            _gl.BlitFramebuffer(0, 0, rw, rh, 0, 0, pw, ph, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        }
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

    /// <summary>The smaller picture for a lower 3D resolution (made again when the size changes).</summary>
    private uint _lowFbo, _lowColor, _lowDepth;
    /// <summary>The smaller picture's size in pixels.</summary>
    private int _lowW, _lowH;

    /// <summary>A size the smaller picture could not be made at (incomplete), not tried again; null when none.</summary>
    private (int W, int H)? _lowFailed;

    /// <summary>
    /// The smaller picture, made again when <paramref name="w"/> × <paramref name="h"/> is not its size.
    /// </summary>
    /// <returns>False when the graphics card does not take it (incomplete; logged once a size): draw at full size.</returns>
    private bool LowResolution(int w, int h)
    {
        if (_lowFbo != 0 && w == _lowW && h == _lowH)
        {
            return true;
        }
        if (_lowFailed == (w, h))
        {
            return false;
        }
        DropLowResolution();
        (_lowW, _lowH) = (w, h);
        _lowFbo = _own.Framebuffer();
        _lowColor = _own.Renderbuffer();
        _lowDepth = _own.Renderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _lowColor);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)w, (uint)h);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _lowDepth);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)w, (uint)h);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _lowFbo);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _lowColor);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _lowDepth);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Console.Error.WriteLine($"The {w}×{h} picture for a lower 3D resolution is incomplete ({status}): drawn at full resolution.");
            DropLowResolution();
            _lowFailed = (w, h);
            return false;
        }
        return true;
    }

    /// <summary>Deletes the smaller picture, if there is one.</summary>
    private void DropLowResolution()
    {
        if (_lowFbo == 0)
        {
            return;
        }
        _own.DeleteFramebuffer(_lowFbo);
        _own.DeleteRenderbuffer(_lowColor);
        _own.DeleteRenderbuffer(_lowDepth);
        _lowFbo = _lowColor = _lowDepth = 0;
        _lowW = _lowH = 0;
    }

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

    /// <summary>
    /// When the area and its models were all in, on the view's clock (-1 until then). --shot and --bench
    /// (Options) wait for it: once every model is loaded, save a picture, then turn the camera for the given
    /// time and print the frame rates, then close the app.
    /// </summary>
    private long _loadedAt = -1;
    /// <summary>The area <c>_loadedAt</c> was measured for (a picture of another area waits for it to load).</summary>
    private WorldScene? _loadedScene;
    /// <summary>
    /// The test driver: its pictures (once the area and its models are in) and its frame-rate measure
    /// (the camera turns on its own for a while, then the rates are given back).
    /// </summary>
    private unsafe void Automate(long now, int pw, int ph)
    {
        if (_picture == null && _bench == null)
        {
            return;
        }
        if (_loadedAt < 0)
        {
            if (Scene != null && _terrainVao != 0 && _pending == 0 && _ready.IsEmpty)
            {
                _loadedAt = now;
            }
            RequestNextFrameRendering();
            return;
        }
        if (_picture is { } req && now - _loadedAt > 500 && _pending == 0)
        {
            _picture = null;
            try
            {
                GlPicture.Save(_gl, pw, ph, req.Path);
                req.Done.SetResult();
            }
            catch (Exception ex)
            {
                req.Done.SetException(ex);
            }
        }
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

    /// <summary>The test driver's pictures: the next frame once the area and its models are in.</summary>
    private volatile GlPicture.Request? _picture;

    /// <summary>When the last frame was drawn to its end, on the view's clock (-1: none yet).</summary>
    private long _lastFrameAt = -1;

    /// <summary>
    /// What a picture the test driver asked for waits on, in words (for its error when it times out): the frames, the
    /// area, the ground and the models.
    /// </summary>
    internal string PictureWaiting()
    {
        long last = Interlocked.Read(ref _lastFrameAt);
        string frames = last < 0 ? "no frame drawn yet" : $"the last frame ended {_clock.ElapsedMilliseconds - last} ms ago";
        return $"{frames}; {(Scene == null ? "no area" : "an area")} shown, its ground {(_terrainVao != 0 ? "on" : "not on")} the graphics card, "
            + $"{Volatile.Read(ref _pending)} model(s) being read, {_ready.Count} read and not uploaded, {(_loadedAt < 0 ? "not all in yet" : "all in")}";
    }

    /// <summary>Saves a picture of the view to <paramref name="path"/> once the area and its models are in.</summary>
    internal Task Picture(string path)
    {
        var r = new GlPicture.Request { Path = path };
        // Measured again for this area: the picture waits until it and its models are in.
        if (Scene != _loadedScene)
        {
            _loadedAt = -1;
            _loadedScene = Scene;
        }
        _picture = r;
        Wake();
        return r.Done.Task;
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
