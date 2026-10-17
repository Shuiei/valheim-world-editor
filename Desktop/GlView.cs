using System.Numerics;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 3D view: the ground, the water and every object and building piece of a WorldScene, drawn with
/// OpenGL (OpenGL 3.3, or OpenGL ES 3.0 where Avalonia gives that, e.g. ANGLE on Windows). Objects are
/// drawn with GPU instancing: one draw per model part for all its copies. Models load on worker threads
/// and appear as they arrive. Drawn only while something happens (the camera moves, keys, the mouse),
/// a few times a second otherwise.
/// </summary>
public sealed partial class GlView : OpenGlControlBase
{
    /// <summary>The OpenGL calls, set up when the context is made.</summary>
    private GL _gl = null!;
    /// <summary>Every OpenGL object this view made, deleted with its context (see GlObjects).</summary>
    private GlObjects _own = null!;
    /// <summary>Its programs' uniform locations, asked once each; made again with the context.</summary>
    private GlUniforms _u = null!;
    /// <summary>True on OpenGL ES (the shaders get its header).</summary>
    private bool _es;

    /// <summary>The models of the area's objects, null when there are none (plain boxes).</summary>
    internal ModelStore? Models { get; private set; }
    /// <summary>
    /// A status line to show (OpenGL version, game look loaded or not…); may be raised from a worker thread.
    /// </summary>
    public event Action<string>? Status;

    // ---- View switches: which kinds of objects are drawn, and the water.
    /// <summary>Which kinds of objects are drawn, by kind.</summary>
    private readonly bool[] _shown = [.. ObjectKinds.All.Select(ObjectKinds.ShownAtFirst)];
    /// <summary>Whether objects of this kind are drawn.</summary>
    public bool IsShown(ObjectKind k) => _shown[(int)k];
    /// <summary>Shows or hides objects of a kind.</summary>
    public void SetShown(ObjectKind k, bool on)
    {
        _shown[(int)k] = on;
        // Which markers are drawn changes, not which things are spawn points or nests: _spawns is kept.
        _marksDirty = true;
        Wake();
    }
    /// <summary>Whether the water is drawn.</summary>
    private volatile bool _showWater = true;
    /// <summary>Whether the water is drawn.</summary>
    public bool ShowWater { get => _showWater; set { _showWater = value; Wake(); } }

    /// <summary>
    /// View, Look: the game's look (its terrain shader, sky and sea, the models) or plain colours and
    /// boxes, as the web editor's switch; see-through buildings (players' pieces drawn faint, to see
    /// what is inside or behind them); and how many pixels the 3D view draws (Sharp: the screen's;
    /// Balanced: one per point; Fast: three quarters of that), fewer pixels for more frames.
    /// </summary>
    public bool GameLookOn
    {
        get => _gameLookOn;
        set
        {
            if (_gameLookOn != value)
            {
                _gameLookOn = value;
                // The models or their boxes: the objects are made again.
                _thingsReset = true;
                Wake();
            }
        }
    }
    /// <summary>The game's look on (backs <c>GameLookOn</c>).</summary>
    private volatile bool _gameLookOn = true;
    /// <summary>Whether the game's look is there to switch on (copied from the game, set up for this area).</summary>
    public bool GameLookLoaded => _look != null;
    /// <summary>Players' pieces drawn faint, to see what is inside or behind them.</summary>
    public bool SeeThroughBuildings { get => _seeThrough; set { _seeThrough = value; Wake(); } }
    /// <summary>Backs <c>SeeThroughBuildings</c>.</summary>
    private volatile bool _seeThrough;
    /// <summary>How many pixels the 3D view draws: the screen's, one per point, or three quarters of that.</summary>
    public enum Resolution
    {
        /// <summary>One pixel per screen pixel.</summary>
        Sharp,
        /// <summary>One pixel per point (fewer on a scaled screen).</summary>
        Balanced,
        /// <summary>Three quarters of Balanced.</summary>
        Fast,
    }
    /// <summary>How many pixels the 3D view draws (see <c>PixelScale</c>).</summary>
    public Resolution Resolution3D { get => _resolution; set { _resolution = value; Wake(); } }
    /// <summary>Backs <c>Resolution3D</c>.</summary>
    private volatile Resolution _resolution = Resolution.Sharp;

    /// <summary>Pixels drawn per screen point for a resolution, on a screen of this scaling.</summary>
    internal static double PixelScale(Resolution r, double screen) => r switch
    {
        Resolution.Balanced => Math.Min(screen, 1),
        Resolution.Fast => Math.Min(screen, 1) * 0.75,
        _ => screen,
    };

    /// <summary>The size in pixels the 3D view draws at now.</summary>
    internal (int W, int H) RenderSize()
    {
        double scale = PixelScale(_resolution, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        return (Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)));
    }

    /// <summary>An empty view; the idle timer runs only while it is shown.</summary>
    public GlView()
    {
        SelectTool = new SelectTool(this);
        // Idle: a few frames a second, 20 with the game look (its water and clouds move).
        // Only while the view is shown: a running timer is held by the dispatcher, and through its Tick
        // this view, its scene and its models, so a view that kept it after its window closed was never freed.
        var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        idle.Tick += (_, _) => { if (_look != null || _clock.ElapsedMilliseconds - _idleAt >= 250) { _idleAt = _clock.ElapsedMilliseconds; RequestNextFrameRendering(); } };
        AttachedToVisualTree += (_, _) => idle.Start();
        DetachedFromVisualTree += (_, _) => idle.Stop();
    }

    /// <summary>
    /// Shows a scene: the camera over its middle, the selection cleared, the game look read again for it
    /// (on a worker thread) and its session's changes followed.
    /// </summary>
    /// <param name="scene">The area to show.</param>
    /// <param name="models">Its objects' models, null to draw them as boxes.</param>
    public void Show(WorldScene scene, ModelStore? models)
    {
        Scene = scene;
        Dungeon.Scene = scene;
        Dungeon.Changed -= Wake;
        Dungeon.Changed += Wake;
        Models = models;
        int g = (scene.H / 2) * scene.W + scene.W / 2;
        lock (_camLock)
        {
            _target = new Vector3(0, scene.Heights[g], 0);
        }
        // Another area: the selection goes now (its indices were the last one's), before anything is
        // selected in this one.
        lock (_selection)
        {
            _selection.Clear();
        }
        SelectionDone();
        _sceneDirty = true;
        _marksDirty = true;
        _spawnsDirty = true;
        // The game look's files do not depend on the area: kept from the last one unless another Valheim was chosen.
        _lookFiles = GameLookGl.Kept();
        // The game look is set up for the area's own textures (mask, heights): again for a new one. The
        // old one's textures go at the next frame (only the drawing may touch the graphics card).
        if (_look != null)
        {
            lock (_looksDropped)
            {
                _looksDropped.Add(_look);
            }
        }
        _look = null;
        // The last area's session is not followed any more: its late events hold its indices, not this scene's.
        if (_followed is { } old)
        {
            old.Changed -= Wake;
            old.ThingsChanged -= OnThingsChanged;
            old.ThingsReset -= RefreshNewMarkers;
            old.ThingsReset -= OnThingsReset;
        }
        _followed = scene.Session;
        if (scene.Session != null)
        {
            scene.Session.Changed += Wake;
            scene.Session.ThingsChanged += OnThingsChanged;
            scene.Session.ThingsReset += RefreshNewMarkers;
            scene.Session.ThingsReset += OnThingsReset;
        }
        Task.Run(() =>
        {
            try
            {
                _lookFiles = GameLookGl.Read();
                Status?.Invoke(_lookFiles == null ? "Valheim was not found: plain colours (choose its folder on the start page)." : "Game look loaded.");
            }
            catch (Exception ex)
            {
                Status?.Invoke("Game look could not be read: " + ex.Message);
            }
            Wake();
        });
        Wake();
    }
    /// <summary>The session whose changes are followed (the shown scene's), null when none.</summary>
    private EditSession? _followed;

    /// <summary>
    /// The area read again (after a save): new indices, so the selection goes at once (not at the next frame).
    /// </summary>
    private void OnThingsReset()
    {
        lock (_selection)
        {
            _selection.Clear();
        }
        SelectionDone();
        _thingsReset = true;
        Wake();
    }

    /// <summary>The game look's files as read, null until read (or when Valheim was not found).</summary>
    private volatile GameLookGl.Files? _lookFiles;
    /// <summary>The game look set up for this area, null until set up (at a frame).</summary>
    private GameLookGl? _look;
    /// <summary>
    /// Game looks of earlier areas, deleted at the next frame (only the drawing may touch the graphics card).
    /// </summary>
    private readonly List<GameLookGl> _looksDropped = [];

    /// <summary>Deletes the game looks of earlier areas (drawing thread).</summary>
    private void DropLooks()
    {
        lock (_looksDropped)
        {
            foreach (var look in _looksDropped)
            {
                look.Delete();
            }
            _looksDropped.Clear();
        }
    }

    /// <summary>
    /// Asks for frames at full speed for a second. Avalonia takes frame requests on its own (UI) thread
    /// only: from a worker thread (a model or the game look finished loading) the request goes there.
    /// </summary>
    private void Wake()
    {
        _wokeAt = _clock.ElapsedMilliseconds;
        if (Dispatcher.UIThread.CheckAccess())
        {
            RequestNextFrameRendering();
        }
        else
        {
            Dispatcher.UIThread.Post(RequestNextFrameRendering);
        }
    }

    // ---------------------------------------------------------------- GL objects
    /// <summary>The ground's, the objects' and the water's shader programs.</summary>
    private uint _terrainProg, _objectProg, _waterProg;

    /// <summary>Sets up the OpenGL calls and the shader programs.</summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _u = new GlUniforms(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        _terrainProg = Program(Shaders.TerrainVs, Shaders.TerrainFs);
        _objectProg = Program(Shaders.ObjectVs, Shaders.ObjectFs);
        _waterProg = Program(Shaders.WaterVs, Shaders.WaterFs);
        Status?.Invoke($"OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}");
    }

    /// <summary>
    /// The view left the window (the map page is shown): Avalonia drops its OpenGL context and makes a
    /// new one when it comes back. Its buffers, textures and programs are shared with Avalonia's own
    /// context, so they outlive it: all are deleted here (the context is still current), then every name
    /// is forgotten and the next frame builds them again in the new one. Keeping the names drew with
    /// names that meant nothing, or something else, in the new context.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        DropLooks();
        _look?.Delete();
        _own.DeleteAll();
        _terrainEbo = _terrainBiomeVbo = _waterVbo = _waterEbo = 0;
        _terrainProg = _objectProg = _waterProg = _lineProg = 0;
        _terrainVao = _terrainIndexCount = _waterVao = _terrainVbo = _terrainExtraVbo = _terrainCutVbo = 0;
        _ghostVbo = 0;
        _roomsMade.Forget();
        _locationsMade.Forget();
        _pathVao = _pathVbo = _pathEdgeVao = _pathEdgeVbo = _pathDotVao = _pathDotVbo = 0;
        _placeRingVao = _placeRingVbo = _hoverBoxVao = _hoverBoxVbo = _previewVao = _previewVbo = 0;
        _dungeonVao = _dungeonVbo = _dungeonPickVao = _dungeonPickVbo = _dungeonAddVao = _dungeonAddVbo = 0;
        _dungeonDoorVao = _dungeonDoorVbo = _dungeonJointVao = _dungeonJointVbo = 0;
        // A kept name drew the spawn markers into whatever the new context had given that number (an object batch:
        // models torn into spikes, an orange wedge across the view) until the view was opened again.
        _spawnMarkGl.Clear();
        _pathBandVao = _pathBandVbo = _pathSoftVao = _pathSoftVbo = _pathPreviewVao = _pathPreviewVbo = _pathPreviewSoftVao = _pathPreviewSoftVbo = 0;
        _markVao = _markVbo = _markProg = 0;
        _markCount = 0;
        _marksDirty = true;
        _placeVao = _placeVbo = _placeShapeVao = _placeShapeVbo = 0;
        _areaVao = _areaVbo = _resetVao = _resetVbo = 0;
        _measureVao = _measureVbo = _lassoVao = _lassoVbo = _ringVao = _ringVbo = _lineVao = _lineVbo = 0;
        _playersVao = _playersVbo = 0;
        _lowFbo = _lowColor = _lowDepth = 0;
        _lowW = _lowH = 0;
        _lowFailed = null;
        _batches.Clear();
        // The extensions' shapes went with the context: made again for the next one.
        _shapeBatches.Clear();
        _shapesDirty = true;
        _shapesGeneration = -1;
        _meshGl.Clear();
        _pickMeshes.Clear();
        _bothWays.Clear();
        _onGpu.Clear();
        _gpuBytes = 0;
        lock (_textures)
        {
            _textures.Clear();
            // Textures being read for the old context: read again for the new one (else every model
            // came back without its textures: trees as white leaf cards).
            _claimed.Clear();
        }
        _gizmoGl.Clear();
        _overlayGl.Clear();
        _look = null;
        _selectionDirty = true;
        _overlaysDirty = true;
        _sceneDirty = true;
    }

    /// <summary>A shader program owned by this view (deleted with its context).</summary>
    private uint Program(string vs, string fs) => _own.Program(Link(vs, fs));

    /// <summary>A program not yet anyone's (the game look keeps its own).</summary>
    private uint Link(string vs, string fs)
    {
        // GLSL ES gives samplers low precision by default: heights read from a texture (the water's uHeight) need
        // high.
        string head = _es ? "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2D;\n" : "#version 330 core\n";
        uint Compile(ShaderType t, string src)
        {
            uint s = _gl.CreateShader(t);
            _gl.ShaderSource(s, head + src);
            _gl.CompileShader(s);
            _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                // What failed goes with the error (a failing game shader would leak them each time it is tried).
                string log = _gl.GetShaderInfoLog(s);
                _gl.DeleteShader(s);
                throw new InvalidOperationException($"{t}: {log}");
            }
            return s;
        }
        uint v = Compile(ShaderType.VertexShader, vs), f;
        try
        {
            f = Compile(ShaderType.FragmentShader, fs);
        }
        catch (InvalidOperationException)
        {
            _gl.DeleteShader(v);
            throw;
        }
        uint p = _gl.CreateProgram();
        _gl.AttachShader(p, v);
        _gl.AttachShader(p, f);
        _gl.LinkProgram(p);
        _gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(p);
            _gl.DeleteProgram(p);
            _gl.DeleteShader(v);
            _gl.DeleteShader(f);
            throw new InvalidOperationException(log);
        }
        _gl.DeleteShader(v);
        _gl.DeleteShader(f);
        return p;
    }

    /// <summary>The area shown, null before one is.</summary>
    internal WorldScene? Scene { get; private set; }
}
