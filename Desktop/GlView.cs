using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 3D view: the ground, the water and every object and building piece of a WorldScene, drawn with
/// OpenGL (OpenGL 3.3, or OpenGL ES 3.0 where Avalonia gives that, e.g. ANGLE on Windows). Objects are
/// drawn with GPU instancing: one draw per model part for all its copies. Models load on worker threads
/// and appear as they arrive. Drawn only while something happens (the camera moves, keys, the mouse),
/// a few times a second otherwise.
/// </summary>
public sealed class GlView : OpenGlControlBase
{
    /// <summary>The OpenGL calls, set up when the context is made.</summary>
    private GL _gl = null!;
    /// <summary>Every OpenGL object this view made, deleted with its context (see GlObjects).</summary>
    private GlObjects _own = null!;
    /// <summary>True on OpenGL ES (the shaders get its header).</summary>
    private bool _es;

    /// <summary>The models of the area's objects, null when there are none (plain boxes).</summary>
    internal ModelStore? Models { get; private set; }

    /// <summary>The last frame-rate report (the test driver reads it).</summary>
    internal Stats? LastStats { get; private set; }

    // ---- Camera: orbit around a target point (view space), like the web editor's.
    /// <summary>Guards the camera's values, which the drawing thread and the UI thread both use.</summary>
    private readonly Lock _camLock = new();
    /// <summary>The point the orbit camera turns around, in view space.</summary>
    private Vector3 _target;
    /// <summary>The orbit camera's distance (metres), turn and tilt (radians).</summary>
    private float _distance = 140, _yaw = 0.8f, _pitch = 0.9f;
    /// <summary>Walk and Fly (F): the camera itself at _eyePos, looking along yaw and pitch (positive: down).</summary>
    public enum EyeMode
    {
        /// <summary>Turning around a target point.</summary>
        Orbit,
        /// <summary>On the ground, 6 m/s (Shift: 12).</summary>
        Walk,
        /// <summary>Free in the air, 15 m/s (Shift: 45), kept above the ground.</summary>
        Fly,
    }
    /// <summary>How the camera moves now.</summary>
    private EyeMode _eye = EyeMode.Orbit;
    /// <summary>Walk and Fly: where the camera is, in view space.</summary>
    private Vector3 _eyePos;
    /// <summary>How the camera moves now (F changes it).</summary>
    public EyeMode Eye { get { lock (_camLock) { return _eye; } } }
    /// <summary>Raised on the UI thread when F changes how the camera moves.</summary>
    public event Action<EyeMode>? EyeChanged;
    /// <summary>A player's eye height over the ground, in metres.</summary>
    private const float EyeHeight = 1.8f;
    /// <summary>Walk and Fly: the way the camera looks, from yaw and pitch.</summary>
    private Vector3 Forward => new(-MathF.Sin(_yaw) * MathF.Cos(_pitch), -MathF.Sin(_pitch), -MathF.Cos(_yaw) * MathF.Cos(_pitch));

    /// <summary>F: the usual view, then walking at a player's eye height, then flying, then back.</summary>
    internal void CycleEye()
    {
        lock (_camLock)
        {
            switch (_eye)
            {
                case EyeMode.Orbit:
                    _eye = EyeMode.Walk;
                    _eyePos = _target;
                    _pitch = 0.05f;
                    KeepOverGround(walk: true);
                    break;
                case EyeMode.Walk:
                    _eye = EyeMode.Fly;
                    break;
                default:
                    // Back around the point ahead, on the ground.
                    _eye = EyeMode.Orbit;
                    var ahead = _eyePos + new Vector3(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw)) * 20;
                    _target = ahead;
                    FollowGround();
                    _pitch = 0.9f;
                    _distance = Math.Max(20, Vector3.Distance(_eyePos, _target));
                    break;
            }
        }
        var e = Eye;
        Dispatcher.UIThread.Post(() => EyeChanged?.Invoke(e));
        Wake();
    }

    /// <summary>
    /// Above this the game takes a place for indoors (Character.InInterior): dungeons are built there,
    /// 5000 m over their entrances. The camera does not keep to the ground up there.
    /// </summary>
    internal const float InteriorHeight = 3000f;

    /// <summary>
    /// Walking: the eyes 1.8 m over the ground (or the water); flying: never below that. Not in a
    /// dungeon (the ground is far below).
    /// </summary>
    private void KeepOverGround(bool walk)
    {
        var s = Scene;
        if (s == null || _eyePos.Y > InteriorHeight)
        {
            return;
        }
        float floor = Math.Max(Picking.HeightAt(s, _eyePos.X, _eyePos.Z), s.Water) + EyeHeight;
        _eyePos.Y = walk ? floor : Math.Max(_eyePos.Y, floor);
    }
    /// <summary>The keys held now (moving the camera with them).</summary>
    private readonly HashSet<Key> _keys = [];
    /// <summary>Where a mouse drag started, null when none is on.</summary>
    private Point? _dragFrom;
    /// <summary>The mouse button the drag is made with.</summary>
    private PointerUpdateKind _dragButton;
    /// <summary>
    /// Milliseconds on the view's clock: when it was last woken (<c>Wake</c>), the last frame, the last idle frame.
    /// </summary>
    private long _wokeAt, _lastFrame, _idleAt;
    /// <summary>The camera of the last frame, to tell whether it moved.</summary>
    private Matrix4x4 _lastView;

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
    /// <summary>
    /// A status line to show (OpenGL version, game look loaded or not…); may be raised from a worker thread.
    /// </summary>
    public event Action<string>? Status;
    /// <summary>The view's clock, in milliseconds.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    /// <summary>The frames of the last second: when, and how long the drawing took (ms).</summary>
    private readonly List<(long At, double Work)> _frames = [];
    /// <summary>When the last frame-rate report was made.</summary>
    private long _statsAt;
    /// <summary>The frame-rate log, null when it is off.</summary>
    public PerfLog? Perf { get; set; }

    // ---- View switches: which kinds of objects are drawn, and the water.
    /// <summary>Which kinds of objects are drawn, by kind.</summary>
    private readonly bool[] _shown = [.. ObjectKinds.All.Select(ObjectKinds.ShownAtFirst)];
    /// <summary>Whether objects of this kind are drawn.</summary>
    public bool IsShown(ObjectKind k) => _shown[(int)k];
    /// <summary>Shows or hides objects of a kind.</summary>
    public void SetShown(ObjectKind k, bool on)
    {
        _shown[(int)k] = on;
        RefreshNewMarkers();
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

    /// <summary>
    /// Alt + wheel: the tools that turn take it (the window decides: true when taken); otherwise it zooms.
    /// The direction is that of the , and . keys: towards you (down) as . and away (up) as ,.
    /// </summary>
    internal Func<Key, bool, bool>? AltWheel { get; set; }
    /// <summary>
    /// Ctrl + wheel (up: 1, down: -1; with Shift): what the tool does with it (true), else the view zooms.
    /// </summary>
    internal Func<int, bool, bool>? CtrlWheel { get; set; }
    /// <summary>The wheel's turns not used yet (a touchpad gives fractions of a notch).</summary>
    private double _wheelSum;
    /// <summary>How many objects of each kind the area has (once the models' names are known).</summary>
    public event Action<Dictionary<ObjectKind, int>>? KindCounts;

    // ---- Overlays (zone borders at first) and the ground's measuring colours (game look only).
    /// <summary>Which overlays are drawn, by layer: the zone borders at first.</summary>
    private readonly bool[] _overlay = [.. Overlays.All.Select(l => l == Overlays.Layer.Borders)];
    /// <summary>Whether an overlay is drawn.</summary>
    public bool IsOverlayShown(Overlays.Layer l) => _overlay[(int)l];
    /// <summary>Shows or hides an overlay (Flatten takes its edge along).</summary>
    public void SetOverlay(Overlays.Layer l, bool on)
    {
        _overlay[(int)l] = on;
        if (l == Overlays.Layer.Flatten)
        {
            _overlay[(int)Overlays.Layer.FlattenEdge] = on;
        }
        Wake();
    }
    /// <summary>Backs <c>SlopeColours</c>.</summary>
    private volatile bool _slope;

    /// <summary>Ground coloured by slope (game look only).</summary>
    public bool SlopeColours { get => _slope; set { _slope = value; Wake(); } }
    /// <summary>The height between contour lines on the ground, in metres; 0 for none (game look only).</summary>
    public float ContourStep { get; set { field = value; Wake(); } }
    /// <summary>The red on ground at the game's ±8 m limit can be hidden (View: Limit marks).</summary>
    private volatile bool _limit = true;
    /// <summary>The red on ground at the game's ±8 m limit (View: Limit marks).</summary>
    public bool LimitMarks { get => _limit; set { _limit = value; Wake(); } }
    /// <summary>How many wards, crafting stations, flattened places and locations the overlays show.</summary>
    public event Action<Overlays.Built>? OverlaysBuilt;
    /// <summary>The overlays as last built.</summary>
    private Overlays.Built? _overlays;
    /// <summary>Each overlay's lines on the graphics card.</summary>
    private readonly Dictionary<Overlays.Layer, (uint Vao, int Count, uint Vbo)> _overlayGl = [];

    /// <summary>For tests: the camera (orbit: turn, tilt, distance and target).</summary>
    internal (float Yaw, float Pitch, float Distance, Vector3 Target) Camera { get { lock (_camLock) { return (_yaw, _pitch, _distance, _target); } } }
    /// <summary>For tests: where the camera is in Walk and Fly.</summary>
    internal Vector3 EyePosition { get { lock (_camLock) { return _eyePos; } } }
    /// <summary>For tests: move as if the keys were held for dt seconds.</summary>
    internal bool Step(float dt) => MoveWithKeys(dt);
    /// <summary>For tests: the keys held.</summary>
    internal Key[] KeysHeld { get { lock (_keys) { return [.. _keys]; } } }

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
        _lookFiles = null;
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
        if (scene.Session != null)
        {
            scene.Session.Changed += Wake;
            scene.Session.ThingsChanged += OnThingsChanged;
            scene.Session.ThingsReset += RefreshNewMarkers;
            // Read again after a save: new indices, so the selection goes at once (not at the next frame).
            scene.Session.ThingsReset += () =>
            {
                lock (_selection)
                {
                    _selection.Clear();
                }
                SelectionDone();
                _thingsReset = true;
                Wake();
            };
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
    /// <summary>The ground's and the water's vertex arrays and buffers, and the ground's index count.</summary>
    private uint _terrainVao, _terrainIndexCount, _waterVao, _terrainVbo, _terrainExtraVbo;
    /// <summary>More of the ground's and the water's buffers.</summary>
    private uint _terrainEbo, _terrainBiomeVbo, _waterVbo, _waterEbo;
    /// <summary>The scene changed: the ground and the water are built again at the next frame.</summary>
    private bool _sceneDirty;

    /// <summary>One draw: a model part (mesh and material) with all its copies, as instances.</summary>
    private sealed class Batch
    {
        /// <summary>The View kind it belongs to (hidden with it).</summary>
        public ObjectKind Kind;
        /// <summary>Its vertex array, and the buffer of its instances' matrices.</summary>
        public uint Vao, InstanceVbo;
        /// <summary>Indices per copy, and how many copies.</summary>
        public int IndexCount, Instances;
        /// <summary>Its texture, 0 for none.</summary>
        public uint Texture;
        /// <summary>Its material (colour, cut-out, two-sided…).</summary>
        public ModelStore.MaterialData Material = null!;
        /// <summary>
        /// Its instances by area cell (<see cref="CellSize"/>): they are sorted by cell, each cell a run of them, so
        /// only the cells in view are drawn.
        /// </summary>
        public (int Cell, int Start, int Count)[] Cells = [];
        /// <summary>The model's size (its box's diagonal, metres): small ones are not drawn far away.</summary>
        public float Size = 1;
        /// <summary>The instance its vertex array's instance attributes point at now.</summary>
        public int PointedAt;
    }
    /// <summary>Every batch drawn.</summary>
    private readonly List<Batch> _batches = [];

    /// <summary>
    /// The things of one kind of object (a building piece a player placed apart from the same piece in a
    /// ruin): one model, drawn with one batch per model part. Its batches' instances are rebuilt when one
    /// of its things changes (deleted, moved, added, or shown elsewhere while being moved).
    /// </summary>
    private sealed class Group
    {
        /// <summary>Prefab, piece or not, tamed or not.</summary>
        public required (int Prefab, bool Piece, bool Tamed) Key { get; init; }
        /// <summary>The View kind of its things.</summary>
        public required ObjectKind Kind { get; init; }
        /// <summary>Its things, as indices into the scene's things.</summary>
        public readonly List<int> Things = [];
        /// <summary>
        /// The same, to ask whether one is in (a list's Contains made opening an area with tens of
        /// thousands of one piece take seconds: every object asked it).
        /// </summary>
        public readonly HashSet<int> Members = [];
        /// <summary>The model's root scale, applied to every copy.</summary>
        public Vector3 RootScale = Vector3.One;
        /// <summary>The model's box in its own frame (for picking), known once read.</summary>
        public (Vector3 Min, Vector3 Max)? Box;
        /// <summary>Its batches, each with the part's own matrix within the model.</summary>
        public readonly List<(Batch Batch, Matrix4x4 Pre)> Batches = [];
        /// <summary>Its batches are made (the model read, or the box set up in its place).</summary>
        public bool Ready;
        /// <summary>A model was read (else the batches are the stand-in box).</summary>
        public bool HasModel;
    }
    /// <summary>By prefab, piece or not and tamed or not: tamed creatures are their own View kind.</summary>
    private readonly Dictionary<(int, bool, bool), Group> _groups = [];
    /// <summary>Groups whose instances are built again at the next frame.</summary>
    private readonly HashSet<Group> _dirtyGroups = [];
    /// <summary>Things shown somewhere else than where they are, while a move is being made (Select tool).</summary>
    private Dictionary<int, WorldScene.Thing> _previews = [];
    /// <summary>The things were read again (rebuild every group); the overlays are built again.</summary>
    private volatile bool _thingsReset, _overlaysDirty;
    /// <summary>
    /// The overlays wait for the ground only (not for objects, which show at once): built again 200 ms
    /// after the last time at the most.
    /// </summary>
    private bool _overlaysGroundOnly;
    /// <summary>When the overlays were last built, on the view's clock.</summary>
    private long _overlaysBuiltAt;
    /// <summary>
    /// Each mesh on the graphics card, by name: its vertex buffer, and an index buffer and count per part.
    /// </summary>
    private readonly Dictionary<string, (uint Vbo, uint[] Ebos, int[] Counts)> _meshGl = [];
    /// <summary>
    /// Meshes on the graphics card (read by the loaders on worker threads), and about how much the
    /// models and their textures take there. Past the budget, opening another area lets them all go
    /// (that area's are read again): they piled up over a long session, gigabytes with Valheim open.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _onGpu = new();
    /// <summary>About how many bytes the models and their textures take on the graphics card.</summary>
    private long _gpuBytes;
    /// <summary>The graphics card budget in bytes (1 GiB); VWE_GPU_BUDGET (bytes): another budget, for the tests.</summary>
    private static readonly long GpuBudget = long.TryParse(Environment.GetEnvironmentVariable("VWE_GPU_BUDGET"), out long b) ? b : 1L << 30;
    /// <summary>Textures on the graphics card, by name.</summary>
    private readonly Dictionary<string, uint> _textures = [];
    /// <summary>Models read on worker threads, waiting to go to the graphics card (on the drawing thread).</summary>
    private sealed record ReadyModel(Group Group, ModelStore.Model? Model, Dictionary<string, ModelStore.MeshData> Meshes,
        Dictionary<string, ModelStore.MaterialData> Materials, Dictionary<string, ModelStore.ImageData?> Images);
    /// <summary>Models read, waiting for the drawing thread.</summary>
    private readonly ConcurrentQueue<ReadyModel> _ready = new();
    /// <summary>Models still being read.</summary>
    private int _pending;
    /// <summary>The stand-in box's vertex and index buffers.</summary>
    private uint _boxVbo, _boxEbo;

    /// <summary>Sets up the OpenGL calls and the shader programs.</summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
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
        _terrainVao = _terrainIndexCount = _waterVao = _terrainVbo = _terrainExtraVbo = 0;
        _boxVbo = _boxEbo = _ghostVbo = 0;
        _pathVao = _pathVbo = _pathEdgeVao = _pathEdgeVbo = _pathDotVao = _pathDotVbo = 0;
        _placeRingVao = _placeRingVbo = _hoverBoxVao = _hoverBoxVbo = 0;
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
        _batches.Clear();
        _meshGl.Clear();
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
        string head = _es ? "#version 300 es\nprecision highp float;\nprecision highp int;\n" : "#version 330 core\n";
        uint Compile(ShaderType t, string src)
        {
            uint s = _gl.CreateShader(t);
            _gl.ShaderSource(s, head + src);
            _gl.CompileShader(s);
            _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                throw new InvalidOperationException($"{t}: {_gl.GetShaderInfoLog(s)}");
            }
            return s;
        }
        uint p = _gl.CreateProgram();
        uint v = Compile(ShaderType.VertexShader, vs), f = Compile(ShaderType.FragmentShader, fs);
        _gl.AttachShader(p, v);
        _gl.AttachShader(p, f);
        _gl.LinkProgram(p);
        _gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            throw new InvalidOperationException(_gl.GetProgramInfoLog(p));
        }
        _gl.DeleteShader(v);
        _gl.DeleteShader(f);
        return p;
    }

    // ---- The ground: one point per metre, normals from the neighbours, colour from the biome.
    /// <summary>Position, normal and colour of rows z0..z1 (9 floats a point).</summary>
    private static float[] TerrainRows(WorldScene s, int z0, int z1)
    {
        int w = s.W, h = s.H;
        float[] v = new float[w * (z1 - z0 + 1) * 9];
        float H(int x, int z) => s.Heights[Math.Clamp(z, 0, h - 1) * w + Math.Clamp(x, 0, w - 1)];
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = 0; gx < w; gx++)
            {
                int g = gz * w + gx, o = ((gz - z0) * w + gx) * 9;
                var n = Vector3.Normalize(new Vector3(H(gx - 1, gz) - H(gx + 1, gz), 2f, -(H(gx, gz - 1) - H(gx, gz + 1))));
                var c = Shaders.BiomeColor(s.Biomes[g]);
                v[o] = gx - (w - 1) / 2f; v[o + 1] = s.Heights[g]; v[o + 2] = -(gz - (h - 1) / 2f);
                v[o + 3] = n.X; v[o + 4] = n.Y; v[o + 5] = n.Z;
                v[o + 6] = c.X; v[o + 7] = c.Y; v[o + 8] = c.Z;
            }
        }
        return v;
    }

    /// <summary>Mask uv, ocean depth and limit tint of rows z0..z1 (4 floats a point).</summary>
    private static float[] ExtraRows(WorldScene s, int z0, int z1)
    {
        int w = s.W, h = s.H;
        float[] extra = new float[w * (z1 - z0 + 1) * 4];
        for (int g = z0 * w, o = 0; g < (z1 + 1) * w; g++, o += 4)
        {
            extra[o] = (g % w + 0.5f) / w;
            extra[o + 1] = (g / w + 0.5f) / h;
            extra[o + 2] = s.OceanDepth[g];
            extra[o + 3] = s.Limit[g];
        }
        return extra;
    }

    /// <summary>After an edit: the changed rows (and their neighbours, whose normals move) to the graphics card.</summary>
    private unsafe void UpdateTerrain(WorldScene s, int z0, int z1)
    {
        z0 = Math.Max(0, z0 - 1);
        z1 = Math.Min(s.H - 1, z1 + 1);
        float[] v = TerrainRows(s, z0, z1), extra = ExtraRows(s, z0, z1);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainVbo);
        fixed (float* p = v)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, z0 * s.W * 36, (nuint)(v.Length * 4), p);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainExtraVbo);
        fixed (float* p = extra)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, z0 * s.W * 16, (nuint)(extra.Length * 4), p);
        }
        _look?.UpdateRows(s, z0, z1);
    }

    /// <summary>The last area's ground and sea.</summary>
    private void DeleteTerrain()
    {
        _own.DeleteVertexArray(_terrainVao);
        _own.DeleteVertexArray(_waterVao);
        foreach (uint b in new[] { _terrainVbo, _terrainEbo, _terrainBiomeVbo, _terrainExtraVbo, _waterVbo, _waterEbo })
        {
            _own.DeleteBuffer(b);
        }
        _terrainVao = _waterVao = _terrainVbo = _terrainEbo = _terrainBiomeVbo = _terrainExtraVbo = _waterVbo = _waterEbo = 0;
    }

    /// <summary>
    /// The ground (one point per metre, with the game's terrain shader's extra values) and the sea, on the graphics
    /// card.
    /// </summary>
    private unsafe void BuildTerrain(WorldScene s)
    {
        DeleteTerrain();
        int w = s.W, h = s.H;
        float[] v = TerrainRows(s, 0, h - 1);
        uint[] idx = new uint[(w - 1) * (h - 1) * 6];
        int k = 0;
        for (int gz = 0; gz < h - 1; gz++)
        {
            for (int gx = 0; gx < w - 1; gx++)
            {
                uint a = (uint)(gz * w + gx), b = a + 1, c = a + (uint)w, d = c + 1;
                // Counter-clockwise seen from above (z mirrored).
                idx[k++] = a; idx[k++] = b; idx[k++] = c;
                idx[k++] = b; idx[k++] = d; idx[k++] = c;
            }
        }
        _terrainVao = _own.VertexArray();
        _gl.BindVertexArray(_terrainVao);
        uint vbo = _terrainVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = v)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        uint ebo = _terrainEbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* p = idx)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idx.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        for (uint a = 0; a < 3; a++)
        {
            _gl.EnableVertexAttribArray(a);
            _gl.VertexAttribPointer(a, 3, VertexAttribPointerType.Float, false, 36, (void*)(a * 12));
        }
        // For the game's terrain shader: biome colour (bytes), then mask uv, ocean depth, limit tint.
        uint bc = _terrainBiomeVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, bc);
        fixed (byte* p = s.BiomeColor)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)s.BiomeColor.Length, p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.UnsignedByte, true, 4, (void*)0);
        float[] extra = ExtraRows(s, 0, h - 1);
        uint ex = _terrainExtraVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, ex);
        fixed (float* p = extra)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(extra.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, 16, (void*)8);
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 1, VertexAttribPointerType.Float, false, 16, (void*)12);
        _terrainIndexCount = (uint)idx.Length;
        // The sea: one quad over the area, at the water level.
        float hw = w / 2f + 400, hh = h / 2f + 400, y = s.Water;
        float[] q = [-hw, y, -hh, hw, y, -hh, hw, y, hh, -hw, y, hh];
        uint[] qi = [0, 2, 1, 0, 3, 2];
        _waterVao = _own.VertexArray();
        _gl.BindVertexArray(_waterVao);
        uint wv = _waterVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, wv);
        fixed (float* p = q)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(q.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        uint we = _waterEbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, we);
        fixed (uint* p = qi)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(qi.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        _gl.BindVertexArray(0);
    }

    // ---- Objects: read each kind's model on worker threads; the drawing thread uploads them.
    /// <summary>A prefab's name: the model store's, else the catalogue's display name; null when unknown.</summary>
    private string? NameOf(int prefab) => Models?.NameOf(prefab) ?? Terrain.PrefabCatalog.DisplayName(prefab);

    /// <summary>A new area's objects: every thing in its group, and every group's model read on worker threads.</summary>
    private void StartModels(WorldScene s)
    {
        lock (_groups)
        {
            _groups.Clear();
            lock (_dirtyGroups)
            {
                _dirtyGroups.Clear();
            }
            int n;
            lock (s.Things)
            {
                n = s.Things.Count;
                for (int i = 0; i < n; i++)
                {
                    GroupOf(s, i, start: false);
                }
            }
            _pending = _groups.Count;
            foreach (var g in _groups.Values.ToList())
            {
                Load(g);
            }
        }
        SendKindCounts(s);
        _overlaysDirty = true;
    }

    /// <summary>The group a thing belongs to (made, and its model read, if it is the first of its kind).</summary>
    private Group GroupOf(WorldScene s, int i, bool start = true)
    {
        var t = s.Things[i];
        var key = (t.Prefab, t.Piece, t.Tamed);
        EnsureSize(i + 1);
        if (!_groups.TryGetValue(key, out var g))
        {
            g = _groups[key] = new Group { Key = key, Kind = ObjectKinds.Of(NameOf(t.Prefab), t.Piece, t.Tamed) };
            if (start)
            {
                Interlocked.Increment(ref _pending);
                Load(g);
            }
        }
        if (g.Members.Add(i))
        {
            g.Things.Add(i);
        }
        lock (_objLock)
        {
            _kinds[i] = g.Kind;
        }
        return g;
    }

    /// <summary>A kind's group without things yet (the Place tool's ghosts), its model read if new.</summary>
    private Group KindGroup(int prefab, bool piece)
    {
        lock (_groups)
        {
            if (!_groups.TryGetValue((prefab, piece, false), out var g))
            {
                g = _groups[(prefab, piece, false)] = new Group { Key = (prefab, piece, false), Kind = ObjectKinds.Of(NameOf(prefab), piece) };
                Interlocked.Increment(ref _pending);
                Load(g);
            }
            return g;
        }
    }

    /// <summary>The buffer of the ghosts' and rooms' instances, made at first use.</summary>
    private uint _ghostVbo;
    /// <summary>The kinds the paste's ghost drew this frame (their posts are left out).</summary>
    private HashSet<int>? _pasteGhosts;
    /// <summary>
    /// The Place tool's preview, or the Paste tool's (the building where it would land), drawn with the
    /// models, see-through. Kinds whose model is not there yet keep their posts (DrawPlace, the paste's
    /// outline). Returns the prefabs drawn.
    /// </summary>
    private unsafe HashSet<int> DrawGhosts(WorldScene s)
    {
        var drawn = new HashSet<int>();
        List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown;
        if (Mode == ToolMode.Place && Place is { } pl && pl.Shown.Length > 0)
        {
            shown = [.. pl.Shown.Select(o => (Save.StableHash.Of(o.Name), o.Position, o.Rotation, o.Scale))];
        }
        else if (Mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
        {
            shown = Paste.Ghosts(pat, GridHeight, s.X0 * 64f - 32f, s.Z0 * 64f - 32f);
        }
        else
        {
            return drawn;
        }
        _pasteGhosts = Mode == ToolMode.Paste ? drawn : null;
        DrawInstances(s, shown, ghost: true, drawn);
        return drawn;
    }

    /// <summary>
    /// Models at places that are not things of the scene, one draw per model part: see-through (ghost:
    /// the Place and Paste previews) or solid (dungeon rooms, locations' parts). Kinds whose model is not read yet, or that
    /// have none, are skipped (and left out of <paramref name="drawn"/>).
    /// </summary>
    private unsafe void DrawInstances(WorldScene s, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown, bool ghost, HashSet<int>? drawn = null)
    {
        if (_ghostVbo == 0)
        {
            _ghostVbo = _own.Buffer();
        }
        _gl.UseProgram(_objectProg);
        _gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uGhost"), ghost ? 1f : 0f);
        if (ghost)
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.DepthMask(false);
        }
        int uColor = _gl.GetUniformLocation(_objectProg, "uColor"), uCut = _gl.GetUniformLocation(_objectProg, "uCutoff"),
            uHasMap = _gl.GetUniformLocation(_objectProg, "uHasMap"), uUv = _gl.GetUniformLocation(_objectProg, "uUv");
        foreach (var byKind in shown.GroupBy(o => o.Prefab))
        {
            int prefab = byKind.Key;
            bool piece = Terrain.PieceCatalog.Get(prefab)?.Tool != null;
            var g = KindGroup(prefab, piece);
            if (!g.Ready || g.Batches.Count == 0 || !ghost && !g.HasModel)
            {
                continue;
            }
            drawn?.Add(prefab);
            var mats = byKind.Select(o => Placement(s, new WorldScene.Thing(0, prefab, o.Position, o.Rotation, o.Scale, piece), g.RootScale)).ToList();
            foreach (var (b, pre) in g.Batches)
            {
                var data = mats.Select(m => pre * m).ToArray();
                _gl.BindVertexArray(b.Vao);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ghostVbo);
                fixed (Matrix4x4* p = data)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.DynamicDraw);
                }
                for (uint c = 0; c < 4; c++)
                {
                    _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(c * 16));
                }
                var m = b.Material;
                if (b.Texture == 0 && m.Map != null)
                {
                    lock (_textures)
                    {
                        _textures.TryGetValue(m.Map, out b.Texture);
                    }
                }
                if (m.DoubleSided)
                {
                    _gl.Disable(EnableCap.CullFace);
                }
                else
                {
                    _gl.Enable(EnableCap.CullFace);
                }
                _gl.Uniform4(uColor, m.Color.X, m.Color.Y, m.Color.Z, m.Color.W);
                _gl.Uniform1(uCut, m.Cutoff);
                _gl.Uniform1(uHasMap, b.Texture != 0 ? 1 : 0);
                _gl.Uniform4(uUv, m.UvTransform.X, m.UvTransform.Y, m.UvTransform.Z, m.UvTransform.W);
                _gl.BindTexture(TextureTarget.Texture2D, b.Texture);
                _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)data.Length);
                // The batch's own instances again.
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
                for (uint c = 0; c < 4; c++)
                {
                    _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(c * 16));
                }
                b.PointedAt = 0;
            }
        }
        _gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uGhost"), 0f);
        if (ghost)
        {
            _gl.DepthMask(true);
            _gl.Disable(EnableCap.Blend);
        }
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// The rooms of the area's dungeons (not objects of the save: entries in their dungeon's data, see
    /// DungeonRooms), drawn like objects; a room shown elsewhere by the Dungeon tool (moving, adding) too.
    /// </summary>
    private void DrawRooms(WorldScene s)
    {
        var shown = new List<(int, Vector3, Vector3, float)>();
        foreach (var d in DungeonRooms.Shown(s, Wake))
        {
            foreach (var r in d.Rooms)
            {
                shown.Add((r.Hash, r.Position, BlueprintFormats.ToEuler(r.Rotation), 0f));
            }
        }
        if (shown.Count > 0)
        {
            DrawInstances(s, shown, ghost: false);
        }
    }

    /// <summary>
    /// The parts of the area's locations that are not saved objects (see LocationParts), drawn like objects with the
    /// Ruins &amp; structures kind; they cannot be picked.
    /// </summary>
    private void DrawLocations(WorldScene s)
    {
        var shown = LocationParts.Of(s);
        if (shown.Count > 0)
        {
            DrawInstances(s, shown.Select(l => (l.Location, l.Position, l.Rotation, 0f)), ghost: false);
        }
    }

    /// <summary>Grows the per-thing arrays (boxes, kinds) to hold at least <paramref name="n"/> things.</summary>
    private void EnsureSize(int n)
    {
        lock (_objLock)
        {
            if (_bounds.Length >= n)
            {
                return;
            }
            int size = Math.Max(n, _bounds.Length * 3 / 2 + 16);
            Array.Resize(ref _bounds, size);
            Array.Resize(ref _known, size);
            Array.Resize(ref _boundsAt, size);
            Array.Resize(ref _kinds, size);
        }
    }

    /// <summary>Counts the things still there by kind and sends it (<c>KindCounts</c>, on the UI thread).</summary>
    private void SendKindCounts(WorldScene s)
    {
        var counts = new Dictionary<ObjectKind, int>();
        lock (s.Things)
        {
            // The things may have been read again (after a save) before the view caught up.
            EnsureSize(s.Things.Count);
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    if (!s.Things[i].Gone)
                    {
                        counts[_kinds[i]] = counts.GetValueOrDefault(_kinds[i]) + 1;
                    }
                }
            }
        }
        Dispatcher.UIThread.Post(() => KindCounts?.Invoke(counts));
    }

    /// <summary>Things appeared, went or came back (EditSession): their groups are drawn again.</summary>
    private void OnThingsChanged(IReadOnlyList<int> indices)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        lock (_groups)
        {
            lock (s.Things)
            {
                foreach (int i in indices)
                {
                    MarkDirty(GroupOf(s, i));
                }
            }
        }
        lock (_selection)
        {
            _selection.RemoveWhere(i => s.Things[i].Gone);
        }
        _selectionDirty = true;
        _overlaysDirty = true;
        _marksDirty = true;
        SendKindCounts(s);
        Wake();
    }

    /// <summary>Has a group's instances built again at the next frame.</summary>
    private void MarkDirty(Group g)
    {
        lock (_dirtyGroups)
        {
            _dirtyGroups.Add(g);
        }
    }

    /// <summary>Shows things somewhere else than where they are (a move in progress), or (null) where they are.</summary>
    internal void SetPreviews(Dictionary<int, WorldScene.Thing>? previews)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var old = _previews;
        _previews = previews ?? [];
        lock (_groups)
        {
            foreach (int i in old.Keys.Concat(_previews.Keys))
            {
                var t = s.Things[i];
                if (_groups.TryGetValue((t.Prefab, t.Piece, t.Tamed), out var g))
                {
                    MarkDirty(g);
                }
            }
        }
        _selectionDirty = true;
        Wake();
    }

    /// <summary>
    /// Reads a group's model on a worker thread; the drawing thread sends it to the graphics card. Waiting
    /// for its turn holds no thread (hundreds of kinds each blocking one starved the thread pool: the
    /// next area's loading and the map's search waited seconds behind them).
    /// </summary>
    private void Load(Group g)
    {
        Task.Run(async () =>
        {
            await LoadGate.WaitAsync();
            try
            {
                // Left already (another area opened while it waited): not read at all.
                if (!Current(g))
                {
                    return;
                }
                string? name = NameOf(g.Key.Prefab);
                var model = name != null && Models != null && _gameLookOn ? Models.LoadModel(name) : null;
                var meshes = new Dictionary<string, ModelStore.MeshData>();
                var mats = new Dictionary<string, ModelStore.MaterialData>();
                var images = new Dictionary<string, ModelStore.ImageData?>();
                Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
                if (model != null)
                {
                    foreach (var part in model.Parts)
                    {
                        // A mesh already on the graphics card is not read again: only its box is needed.
                        var bounds = _onGpu.ContainsKey(part.Mesh) ? Models!.BoundsOf(part.Mesh) : null;
                        if (bounds == null && Models!.LoadMesh(part.Mesh) is { } md)
                        {
                            meshes.TryAdd(part.Mesh, md);
                            bounds = md.Bounds;
                        }
                        if (bounds is { } box)
                        {
                            // The object's box, for picking: the parts' meshes.
                            var (a, b) = Picking.Transform(box.Min, box.Max, part.Matrix);
                            lo = Vector3.Min(lo, a);
                            hi = Vector3.Max(hi, b);
                        }
                        if (!mats.ContainsKey(part.Material))
                        {
                            var mat = Models!.Material(part.Material);
                            mats[part.Material] = mat;
                            if (mat.Map != null && !images.ContainsKey(mat.Map))
                            {
                                lock (_textures)
                                {
                                    if (_textures.ContainsKey(mat.Map) || _claimed.Contains(mat.Map))
                                    {
                                        continue;
                                    }
                                    _claimed.Add(mat.Map);
                                }
                                images[mat.Map] = Models.LoadTexture(mat.Map);
                            }
                        }
                    }
                }
                if (lo.X > hi.X)
                {
                    // No model: the stand-in box.
                    (lo, hi) = (new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 2, 0.5f));
                }
                g.RootScale = model?.RootScale ?? Vector3.One;
                g.HasModel = model != null;
                g.Box = (lo, hi);
                _ready.Enqueue(new ReadyModel(g, model, meshes, mats, images));
            }
            catch (Exception ex)
            {
                Status?.Invoke($"Model of {g.Key.Prefab}: {ex.Message}");
                if (Current(g))
                {
                    Interlocked.Decrement(ref _pending);
                }
            }
            finally
            {
                LoadGate.Release();
            }
            Wake();
        });
    }
    /// <summary>
    /// How many models are read at once (half the processors, at least two). Static: one 3D view lives as long as
    /// the app (and an instance field would make the view disposable).
    /// </summary>
    private static readonly SemaphoreSlim LoadGate = new(Math.Max(2, Environment.ProcessorCount / 2));
    /// <summary>Guards the per-thing arrays (boxes, kinds), which the loaders and the drawing thread both use.</summary>
    private readonly Lock _objLock = new();

    /// <summary>A group's instances (and its things' boxes) from where its things are now.</summary>
    /// <summary>The side of the area's cells objects are drawn by (metres): only cells in view are drawn.</summary>
    internal const float CellSize = 128;
    /// <summary>
    /// A model is not drawn in cells farther than this many times its size (a 1 m chest past 400 m is two or three
    /// pixels), and never nearer than <see cref="MinCullDistance"/>.
    /// </summary>
    internal const float CullPerMetre = 400;
    /// <summary>Models are drawn within this distance whatever their size.</summary>
    internal const float MinCullDistance = 150;
    /// <summary>The scene the cells were made for (another one makes them again).</summary>
    private WorldScene? _cellScene;
    /// <summary>The cells across and down the area.</summary>
    private int _cellsX, _cellsZ;
    /// <summary>
    /// Each cell's box in view space: the boxes of every object put in it (grown only, until the area is read
    /// again), so a tall or wide one standing over the cell's edge is still drawn when it shows.
    /// </summary>
    private (Vector3 Min, Vector3 Max)[] _cellBounds = [];
    /// <summary>This frame: each cell in view or not, and its distance from the eye.</summary>
    private bool[] _cellSeen = [];
    /// <summary>This frame: each cell's distance from the eye (0 inside it).</summary>
    private float[] _cellDistance = [];

    /// <summary>The cells for this scene, made when it is new.</summary>
    private void EnsureCells(WorldScene s)
    {
        if (_cellScene == s)
        {
            return;
        }
        _cellScene = s;
        _cellsX = Math.Max(1, (int)MathF.Ceiling(s.W / CellSize));
        _cellsZ = Math.Max(1, (int)MathF.Ceiling(s.H / CellSize));
        _cellBounds = new (Vector3, Vector3)[_cellsX * _cellsZ];
        Array.Fill(_cellBounds, (new Vector3(float.MaxValue), new Vector3(float.MinValue)));
        _cellSeen = new bool[_cellBounds.Length];
        _cellDistance = new float[_cellBounds.Length];
    }

    /// <summary>The cell a view-space point is in (points outside go to the nearest edge cell).</summary>
    private int CellOf(WorldScene s, Vector3 p)
    {
        int x = Math.Clamp((int)MathF.Floor((p.X + (s.W - 1) / 2f) / CellSize), 0, _cellsX - 1);
        int z = Math.Clamp((int)MathF.Floor((p.Z + (s.H - 1) / 2f) / CellSize), 0, _cellsZ - 1);
        return z * _cellsX + x;
    }

    /// <summary>
    /// Which cells the camera sees this frame (their box against the view's six planes), and how far each is.
    /// </summary>
    private void SeeCells(Matrix4x4 vp, Vector3 eye)
    {
        // The planes of a row-vector projection: the fourth column plus or minus each of the others.
        Span<Vector4> planes =
        [
            new(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41),
            new(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41),
            new(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42),
            new(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42),
            new(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43),
            new(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43),
        ];
        for (int c = 0; c < _cellBounds.Length; c++)
        {
            var (min, max) = _cellBounds[c];
            bool seen = min.X <= max.X;
            for (int k = 0; k < 6 && seen; k++)
            {
                var pl = planes[k];
                // The box's corner farthest along the plane's normal: outside when even it is behind.
                var far = new Vector3(pl.X >= 0 ? max.X : min.X, pl.Y >= 0 ? max.Y : min.Y, pl.Z >= 0 ? max.Z : min.Z);
                seen = pl.X * far.X + pl.Y * far.Y + pl.Z * far.Z + pl.W >= 0;
            }
            _cellSeen[c] = seen;
            _cellDistance[c] = seen ? Vector3.Distance(eye, Vector3.Clamp(eye, min, max)) : float.MaxValue;
        }
    }

    private unsafe void Rebuild(WorldScene s, Group g)
    {
        EnsureCells(s);
        var list = new List<(int Cell, Matrix4x4 M, Vector3 Tint)>();
        var support = _support;
        var previews = _previews;
        lock (s.Things)
        {
            lock (_objLock)
            {
                foreach (int i in g.Things)
                {
                    var t = previews.TryGetValue(i, out var p) ? p : s.Things[i];
                    var m = Placement(s, t, g.RootScale);
                    // Without its model's box yet: a metre around it (the stand-in box).
                    (Vector3 Min, Vector3 Max) bounds = g.Box is { } box ? Picking.Transform(box.Min, box.Max, m) : (m.Translation - Vector3.One, m.Translation + Vector3.One);
                    if (g.Box != null)
                    {
                        _bounds[i] = bounds;
                        _boundsAt[i] = t.Position;
                    }
                    _known[i] = !t.Gone && g.Box != null;
                    if (!t.Gone)
                    {
                        int cell = CellOf(s, m.Translation);
                        var (cmin, cmax) = _cellBounds[cell];
                        _cellBounds[cell] = (Vector3.Min(cmin, bounds.Min), Vector3.Max(cmax, bounds.Max));
                        list.Add((cell, m, support != null && support.TryGetValue(i, out float sv) ? SupportTint(sv) : Vector3.Zero));
                    }
                }
            }
        }
        // By cell, so each cell's instances are one run of the buffer.
        list.Sort((a, b) => a.Cell.CompareTo(b.Cell));
        var cells = new List<(int Cell, int Start, int Count)>();
        for (int k = 0; k < list.Count; k++)
        {
            if (cells.Count > 0 && cells[^1].Cell == list[k].Cell)
            {
                cells[^1] = cells[^1] with { Count = cells[^1].Count + 1 };
            }
            else
            {
                cells.Add((list[k].Cell, k, 1));
            }
        }
        float size = g.Box is { } gb ? Vector3.Distance(gb.Min, gb.Max) * MathF.Max(g.RootScale.X, MathF.Max(g.RootScale.Y, g.RootScale.Z)) : 1;
        var data = new Matrix4x4[list.Count];
        foreach (var (b, pre) in g.Batches)
        {
            for (int k = 0; k < list.Count; k++)
            {
                var w = pre * list[k].M;
                (w.M14, w.M24, w.M34) = (list[k].Tint.X, list[k].Tint.Y, list[k].Tint.Z);
                data[k] = w;
            }
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
            fixed (Matrix4x4* ptr = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), ptr, BufferUsageARB.DynamicDraw);
            }
            b.Instances = data.Length;
            b.Cells = [.. cells];
            b.Size = size;
        }
    }

    /// <summary>Instances drawn in the last frame (after culling), for the frame-rate measure.</summary>
    private int _drawnInstances;
    /// <summary>Draw calls of objects in the last frame, for the frame-rate measure.</summary>
    private int _drawCalls;
    /// <summary>
    /// Batches with at most this many instances are drawn whole when any of their cells shows: splitting them
    /// costs more calls than the instances it saves.
    /// </summary>
    internal const int SmallBatch = 2048;

    /// <summary>
    /// Draws a batch's instances in the cells in view and near enough for its size (its vertex array bound), each
    /// run of neighbouring cells at once. OpenGL ES 3.0 and OpenGL 4.0 cannot start at an instance, so the
    /// instance attributes are pointed at the run's first one. Returns how many instances were drawn.
    /// </summary>
    private unsafe int DrawCells(Batch b)
    {
        if (b.Instances == 0)
        {
            return 0;
        }
        bool culled = _cellSeen.Length == _cellsX * _cellsZ && _cellScene != null;
        float reach = MathF.Max(MinCullDistance, b.Size * CullPerMetre);
        int start = -1, count = 0, drawn = 0;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        void Flush()
        {
            if (count == 0)
            {
                return;
            }
            if (b.PointedAt != start)
            {
                for (uint c = 0; c < 4; c++)
                {
                    _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)((start * 64L) + (c * 16)));
                }
                b.PointedAt = start;
            }
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
            _drawCalls++;
            drawn += count;
            count = 0;
        }
        bool Shows(int cell) => !culled || cell >= _cellSeen.Length || _cellSeen[cell] && _cellDistance[cell] <= reach;
        if (b.Instances <= SmallBatch || b.Cells.Length == 1)
        {
            foreach (var (cell, _, _) in b.Cells)
            {
                if (Shows(cell))
                {
                    start = 0;
                    count = b.Instances;
                    Flush();
                    break;
                }
            }
            return drawn;
        }
        foreach (var (cell, first, n) in b.Cells)
        {
            bool shows = Shows(cell);
            if (!shows)
            {
                Flush();
                continue;
            }
            if (count > 0 && start + count == first)
            {
                count += n;
            }
            else
            {
                Flush();
                start = first;
                count = n;
            }
        }
        Flush();
        return drawn;
    }

    /// <summary>Every model and texture off the graphics card (after DropObjects: no batch uses them).</summary>
    private void DropModels()
    {
        foreach (var (vbo, ebos, _) in _meshGl.Values)
        {
            _own.DeleteBuffer(vbo);
            foreach (uint e in ebos)
            {
                _own.DeleteBuffer(e);
            }
        }
        _meshGl.Clear();
        _onGpu.Clear();
        lock (_textures)
        {
            foreach (uint t in _textures.Values)
            {
                _own.DeleteTexture(t);
            }
            _textures.Clear();
            _claimed.Clear();
        }
        Status?.Invoke($"Models let go from the graphics card ({_gpuBytes >> 20} MB): this area's are read again.");
        _gpuBytes = 0;
    }

    /// <summary>Everything read again (after a save): the old batches go.</summary>
    private void DropObjects()
    {
        foreach (var b in _batches)
        {
            _own.DeleteVertexArray(b.Vao);
            _own.DeleteBuffer(b.InstanceVbo);
        }
        _batches.Clear();
        // The cells' boxes are made again with the objects.
        _cellScene = null;
        while (_ready.TryDequeue(out var dropped))
        {
            UploadTextures(dropped);
        }
        // (The selection went when the area was shown or read again, not here: one made since, as the
        // map's "Edit in 3D" makes at once, is the new area's.)
        _selectionDirty = true;
        lock (_objLock)
        {
            Array.Clear(_known);
        }
    }
    /// <summary>Textures a loader has taken to read (guarded by <c>_textures</c>), so no other reads them too.</summary>
    private readonly HashSet<string> _claimed = [];

    /// <summary>
    /// Where an object goes, in view space: Unity's rotation (Euler degrees, applied z, x, then y)
    /// mirrored in z, like the web editor's objects.js.
    /// </summary>
    private static Matrix4x4 Placement(WorldScene s, WorldScene.Thing t, Vector3 rootScale)
    {
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y * D, t.Rotation.X * D, t.Rotation.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var scale = t.Scale > 0 ? new Vector3(t.Scale) : rootScale;
        var pos = new Vector3(t.Position.X - s.Cx, t.Position.Y, -(t.Position.Z - s.Cz));
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(pos);
    }

    /// <summary>
    /// A loaded model's textures, then its batches. A model read for an area (or a look) left since
    /// keeps only its textures: its group is gone, and its things' indices are another scene's.
    /// </summary>
    private unsafe void Upload(ReadyModel r)
    {
        UploadTextures(r);
        if (Current(r.Group))
        {
            UploadModel(r);
        }
    }

    /// <summary>The group is still the view's (not one of an area left since).</summary>
    private bool Current(Group g)
    {
        lock (_groups)
        {
            return _groups.TryGetValue(g.Key, out var now) && now == g;
        }
    }

    /// <summary>
    /// A loaded model's textures. Also for a model dropped before it was drawn (a new area): its loader
    /// claimed them, so no other loader reads them.
    /// </summary>
    private unsafe void UploadTextures(ReadyModel r)
    {
        foreach (var (file, img) in r.Images)
        {
            if (img == null || _textures.ContainsKey(file))
            {
                continue;
            }
            uint tex = _own.Texture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            fixed (byte* p = img.Rgba)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Srgb8Alpha8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
            _gl.GenerateMipmap(TextureTarget.Texture2D);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            // With its mipmaps, a third more.
            _gpuBytes += (long)img.Width * img.Height * 4 * 4 / 3;
            lock (_textures)
            {
                _textures[file] = tex;
            }
        }
    }

    /// <summary>A loaded model's batches (or the stand-in box when there is no model), then its instances.</summary>
    private unsafe void UploadModel(ReadyModel r)
    {
        var g = r.Group;
        if (r.Model == null)
        {
            // No model copied for this kind: a box stands in, as in the web editor (spawn points and nests in their
            // markers' colours).
            var tint = g.Kind switch
            {
                ObjectKind.SpawnPoints => SpawnColour(false),
                ObjectKind.Nests => NestColour,
                _ => new Vector4(0.35f, 0.3f, 0.25f, 1),
            };
            AddBatch(g, BoxMesh(), 0, new ModelStore.MaterialData(tint, null, 0, false, new Vector4(1, 1, 0, 0)),
                Matrix4x4.CreateScale(1, 2, 1) * Matrix4x4.CreateTranslation(0, 1, 0));
        }
        else
        {
            foreach (var part in r.Model.Parts)
            {
                // The mesh was read with this model, or is already on the graphics card for another.
                var md = r.Meshes.GetValueOrDefault(part.Mesh);
                if (md == null && !_meshGl.ContainsKey(part.Mesh))
                {
                    continue;
                }
                var mesh = MeshGl(part.Mesh, md);
                if (part.Sub >= mesh.Counts.Length)
                {
                    continue;
                }
                AddBatch(g, mesh, part.Sub, r.Materials[part.Material], part.Matrix);
            }
        }
        g.Ready = true;
        if (Scene is { } s)
        {
            Rebuild(s, g);
        }
        Interlocked.Decrement(ref _pending);
    }

    /// <summary>
    /// A mesh on the graphics card: its vertex buffer and one index buffer per part, uploaded the first time
    /// (<paramref name="md"/> may be null only when it is already there). The model store then forgets the mesh.
    /// </summary>
    private unsafe (uint Vbo, uint[] Ebos, int[] Counts) MeshGl(string id, ModelStore.MeshData? md)
    {
        if (_meshGl.TryGetValue(id, out var m))
        {
            return m;
        }
        // No vertex array bound: binding the index buffers below would rewire whichever one is (another
        // mesh's batch then draws with these triangles: shards everywhere).
        _gl.BindVertexArray(0);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = md!.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(md.Vertices.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        uint[] ebos = new uint[md.Submeshes.Length];
        int[] counts = new int[md.Submeshes.Length];
        for (int s = 0; s < ebos.Length; s++)
        {
            ebos[s] = _own.Buffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebos[s]);
            fixed (uint* p = md.Submeshes[s])
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(md.Submeshes[s].Length * 4), p, BufferUsageARB.StaticDraw);
            }
            counts[s] = md.Submeshes[s].Length;
        }
        _gpuBytes += md.Vertices.Length * 4L + md.Submeshes.Sum(x => x.Length * 4L);
        _onGpu[id] = true;
        Models?.Forget(id);
        return _meshGl[id] = (vbo, ebos, counts);
    }

    /// <summary>The stand-in box's mesh: a unit cube centred on 0, flat normals per face.</summary>
    private (uint, uint[], int[]) BoxMesh()
    {
        const string Id = "\0box";
        if (_meshGl.TryGetValue(Id, out var m))
        {
            return m;
        }
        var verts = new List<float>();
        var idx = new List<uint>();
        // A unit cube centred on 0, flat normals per face.
        Vector3[] n = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (var f in n)
        {
            var u = MathF.Abs(f.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(f, u);
            uint b = (uint)(verts.Count / 8);
            foreach (var (a, c) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var p = (f + u * a + v * c) * 0.5f;
                verts.AddRange([p.X, p.Y, p.Z, f.X, f.Y, f.Z, 0f, 0f]);
            }
            idx.AddRange([b, b + 1, b + 2, b, b + 2, b + 3]);
        }
        return MeshGl(Id, new ModelStore.MeshData([.. verts], [[.. idx]]));
    }

    /// <summary>
    /// A batch for one part of a group's model: the mesh part, its material, and the part's matrix within the model.
    /// </summary>
    private unsafe void AddBatch(Group g, (uint Vbo, uint[] Ebos, int[] Counts) mesh, int sub, ModelStore.MaterialData mat, Matrix4x4 pre)
    {
        var b = new Batch { Kind = g.Kind, Material = mat, IndexCount = mesh.Counts[sub], Instances = 0 };
        lock (_textures)
        {
            b.Texture = mat.Map != null && _textures.TryGetValue(mat.Map, out uint t) ? t : 0;
        }
        b.Vao = _own.VertexArray();
        _gl.BindVertexArray(b.Vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Vbo);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Ebos[sub]);
        b.InstanceVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        for (uint c = 0; c < 4; c++)
        {
            _gl.EnableVertexAttribArray(3 + c);
            _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(c * 16));
            _gl.VertexAttribDivisor(3 + c, 1);
        }
        _gl.BindVertexArray(0);
        _batches.Add(b);
        g.Batches.Add((b, pre));
    }

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
                Status?.Invoke("Game look could not be set up: " + ex.Message);
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
            // Ground that changed moves the overlays lying on it (zone borders, rings): built again then
            // too, a few times a second at most while a brush stroke goes on.
            if (_overlaysDirty && _terrainVao != 0 && (!_overlaysGroundOnly || now - _overlaysBuiltAt >= 200))
            {
                _overlaysDirty = _overlaysGroundOnly = false;
                _overlaysBuiltAt = now;
                Overlays.Built o;
                lock (s.Things)
                {
                    o = _overlays = Overlays.Build(s, s.Modifiers, i => NameOf(s.Things[i].Prefab));
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
        bool low = rw < pw || rh < ph;
        if (low)
        {
            LowResolution(rw, rh);
        }
        else
        {
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
                UpdateTerrain(s, d.Z0, d.Z1);
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
                _gl.UniformMatrix4(_gl.GetUniformLocation(_terrainProg, "uViewProj"), 1, false, (float*)&vp);
                _gl.Uniform3(_gl.GetUniformLocation(_terrainProg, "uSun"), sun.X, sun.Y, sun.Z);
            }
            _gl.BindVertexArray(_terrainVao);
            _gl.DrawElements(PrimitiveType.Triangles, _terrainIndexCount, DrawElementsType.UnsignedInt, (void*)0);

            _gl.UseProgram(_objectProg);
            _gl.UniformMatrix4(_gl.GetUniformLocation(_objectProg, "uViewProj"), 1, false, (float*)&vp);
            _gl.Uniform3(_gl.GetUniformLocation(_objectProg, "uSun"), sun.X, sun.Y, sun.Z);
            void V3(string n, Vector3 v) => _gl.Uniform3(_gl.GetUniformLocation(_objectProg, n), v.X, v.Y, v.Z);
            V3("uSunColor", GameLookGl.SunColor);
            V3("uAmbient", GameLookGl.Ambient);
            V3("uFogColor", GameLookGl.FogColor);
            V3("uSunFogColor", GameLookGl.SunFogColor);
            _gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uFogDensity"), GameLookGl.FogDensity);
            _gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uFogStart"), GameLookGl.FogStart);
            V3("uEye", eye);
            int uColor = _gl.GetUniformLocation(_objectProg, "uColor"), uCut = _gl.GetUniformLocation(_objectProg, "uCutoff"),
                uHasMap = _gl.GetUniformLocation(_objectProg, "uHasMap"), uUv = _gl.GetUniformLocation(_objectProg, "uUv");
            _gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uMap"), 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
            bool seeThrough = _seeThrough;
            int uSee = _gl.GetUniformLocation(_objectProg, "uSeeThrough");
            _gl.Uniform2(_gl.GetUniformLocation(_objectProg, "uCut"), CutY != null ? 1f : 0f, CutY ?? 0f);
            if (_cellScene == s)
            {
                SeeCells(vp, eye);
            }
            int drawn = 0;
            _drawCalls = 0;
            // Opaque first; see-through buildings after, blended over everything and not hiding
            // what is behind them.
            foreach (bool pass in seeThrough ? new[] { false, true } : [false])
            {
                if (pass)
                {
                    _gl.Enable(EnableCap.Blend);
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    _gl.DepthMask(false);
                    _gl.Uniform1(uSee, 1f);
                }
                foreach (var b in _batches)
                {
                    if (!_shown[(int)b.Kind] || seeThrough && (b.Kind == ObjectKind.Buildings) != pass)
                    {
                        continue;
                    }
                    var m = b.Material;
                    if (b.Texture == 0 && m.Map != null)
                    {
                        lock (_textures)
                        {
                            _textures.TryGetValue(m.Map, out b.Texture);
                        }
                    }
                    if (m.DoubleSided)
                    {
                        _gl.Disable(EnableCap.CullFace);
                    }
                    else
                    {
                        _gl.Enable(EnableCap.CullFace);
                    }
                    _gl.Uniform4(uColor, m.Color.X, m.Color.Y, m.Color.Z, m.Color.W);
                    _gl.Uniform1(uCut, m.Cutoff);
                    _gl.Uniform1(uHasMap, b.Texture != 0 ? 1 : 0);
                    _gl.Uniform4(uUv, m.UvTransform.X, m.UvTransform.Y, m.UvTransform.Z, m.UvTransform.W);
                    _gl.BindTexture(TextureTarget.Texture2D, b.Texture);
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
            _drawnInstances = drawn;

            if (_shown[(int)ObjectKind.Dungeons])
            {
                DrawRooms(s);
            }
            if (_shown[(int)ObjectKind.Ruins])
            {
                DrawLocations(s);
            }
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
                    _gl.Enable(EnableCap.Blend);
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    _gl.DepthMask(false);
                    _gl.UseProgram(_waterProg);
                    _gl.UniformMatrix4(_gl.GetUniformLocation(_waterProg, "uViewProj"), 1, false, (float*)&vp);
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

    /// <summary>The smaller picture, made again when <paramref name="w"/> × <paramref name="h"/> is not its size.</summary>
    private void LowResolution(int w, int h)
    {
        if (_lowFbo != 0 && w == _lowW && h == _lowH)
        {
            return;
        }
        if (_lowFbo != 0)
        {
            _own.DeleteFramebuffer(_lowFbo);
            _own.DeleteRenderbuffer(_lowColor);
            _own.DeleteRenderbuffer(_lowDepth);
        }
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
    }

    /// <summary>Counts a frame for the frame rate and the log, and makes a report twice a second.</summary>
    private void Measure(long now, double work, bool camMoved, int pw, int ph)
    {
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
            if (now - bench.From > bench.Seconds * 1000)
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
    internal Task<string> Benchmark(double seconds)
    {
        var b = new Bench { Seconds = seconds };
        _bench = b;
        Wake();
        return b.Done.Task;
    }

    /// <summary>OpenGL's perspective (depth from -1 to 1), as a System.Numerics (row vector) matrix.</summary>
    internal static Matrix4x4 Perspective(float fovY, float aspect, float near, float far)
    {
        float f = 1 / MathF.Tan(fovY / 2);
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) / (near - far), -1,
            0, 0, 2 * far * near / (near - far), 0);
    }

    // ---------------------------------------------------------------- camera input
    /// <summary>
    /// Moves the camera by the keys held (WASD or arrows; Space and C up and down when flying; Shift faster when
    /// walking or flying) for <paramref name="dt"/> seconds (at most 0.1). True when it moved.
    /// </summary>
    private bool MoveWithKeys(float dt)
    {
        Vector3 d = Vector3.Zero;
        bool fast;
        float up = 0;
        lock (_keys)
        {
            if (_keys.Contains(Key.W) || _keys.Contains(Key.Up))
            {
                d.Z -= 1;
            }

            if (_keys.Contains(Key.S) || _keys.Contains(Key.Down))
            {
                d.Z += 1;
            }

            if (_keys.Contains(Key.A) || _keys.Contains(Key.Left))
            {
                d.X -= 1;
            }

            if (_keys.Contains(Key.D) || _keys.Contains(Key.Right))
            {
                d.X += 1;
            }

            if (_keys.Contains(Key.Space))
            {
                up += 1;
            }

            if (_keys.Contains(Key.C))
            {
                up -= 1;
            }

            fast = _keys.Contains(Key.LeftShift) || _keys.Contains(Key.RightShift);
        }
        lock (_camLock)
        {
            if (_eye == EyeMode.Fly && up != 0)
            {
                d.Y = up;
            }
            if (d == Vector3.Zero || dt <= 0)
            {
                return false;
            }
            dt = Math.Min(dt, 0.1f);
            float c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
            var flat = new Vector3(d.X * c + d.Z * sn, 0, -d.X * sn + d.Z * c);
            if (_eye == EyeMode.Orbit)
            {
                _target += flat * Math.Max(10, _distance) * dt;
                FollowGround();
            }
            else
            {
                // Walking 6 m/s (Shift: running 12); flying 15 (Shift: 45).
                float speed = _eye == EyeMode.Walk ? (fast ? 12 : 6) : (fast ? 45 : 15);
                _eyePos += (flat + new Vector3(0, d.Y, 0)) * speed * dt;
                KeepOverGround(walk: _eye == EyeMode.Walk);
            }
        }
        return true;
    }

    /// <summary>The target point stays on the ground (or the water) under it; in a dungeon, at its height.</summary>
    private void FollowGround()
    {
        var s = Scene;
        if (s == null || _target.Y > InteriorHeight)
        {
            return;
        }
        int gx = Math.Clamp((int)MathF.Round(_target.X + (s.W - 1) / 2f), 0, s.W - 1), gz = Math.Clamp((int)MathF.Round(-_target.Z + (s.H - 1) / 2f), 0, s.H - 1);
        _target.Y = Math.Max(s.Heights[gz * s.W + gx], s.Water);
    }

    /// <summary>
    /// Mouse and keys. The OpenGL picture is not something Avalonia can hit-test (pointer events go
    /// through it), so a transparent surface laid over the view takes them and hands them here; keys
    /// come from the window, whatever has the focus.
    /// </summary>
    public void Attach(Control surface, Window window)
    {
        surface.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(surface);
            _pressAt = p.Position;
            _dragFrom = p.Position;
            _dragButton = p.Properties.PointerUpdateKind;
            _pointer = p.Position;
            _surfaceSize = surface.Bounds.Size;
            e.Pointer.Capture(surface);
            // The eyedropper waits: this click is its, not the tool's (nor is the release).
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && PickObjectOnce is { } pickOnce)
            {
                PickObjectOnce = null;
                _dragFrom = null;
                _pickSwallow = true;
                pickOnce(ObjectAt(p.Position, _surfaceSize));
                Wake();
                return;
            }
            // Space + left drag and Shift + right drag slide the view, whatever the tool (the web
            // editor's; handy without a middle button).
            bool space;
            lock (_keys)
            {
                space = _keys.Contains(Key.Space);
            }
            if (_eye == EyeMode.Orbit && (_dragButton == PointerUpdateKind.LeftButtonPressed && space || _dragButton == PointerUpdateKind.RightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
            {
                Panning = true;
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Place && Place != null)
            {
                _dragFrom = null;
                _placeDown = true;
                var m = e.KeyModifiers;
                Place.Down(p.Position, _surfaceSize, m.HasFlag(KeyModifiers.Shift), m.HasFlag(KeyModifiers.Control), m.HasFlag(KeyModifiers.Alt), e.ClickCount);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Paste)
            {
                _dragFrom = null;
                if (GridAt(p.Position, _surfaceSize) is { } at)
                {
                    PasteClicked?.Invoke(at);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Area)
            {
                _dragFrom = null;
                _areaDown = true;
                Area.Down(GridAt(p.Position, _surfaceSize), e.ClickCount);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Path)
            {
                _dragFrom = null;
                _pathDown = true;
                var mods = e.KeyModifiers;
                Path.Down(GridAt(p.Position, _surfaceSize), p.Position, g => ScreenOfGrid(g), mods.HasFlag(KeyModifiers.Control), mods.HasFlag(KeyModifiers.Alt),
                    mods.HasFlag(KeyModifiers.Shift), GridHeight);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode is ToolMode.Shape or ToolMode.Mountain)
            {
                _dragFrom = null;
                if (Scene is { } sc && GroundAt(sc, _lastViewProj, p.Position, _surfaceSize) is { } g)
                {
                    ShapeClicked?.Invoke(g.X, g.Z);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Dungeon)
            {
                _dragFrom = null;
                if (WorldRay(p.Position, _surfaceSize) is { } ray)
                {
                    Dungeon.Hover(ray.O, ray.D);
                }
                Dungeon.Click();
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Measure)
            {
                _dragFrom = null;
                if (WorldAt(p.Position, _surfaceSize) is { } w)
                {
                    Tape.Down(w);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool == null && SelectMode)
            {
                _dragFrom = null;
                _selectDown = true;
                // Ctrl + click (or Shift, Ctrl + zone): added to what is selected; Shift + click: the row
                // from the last object clicked.
                SelectTool.Range = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Control);
                SelectTool.Down(p.Position, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.ClickCount);
                if (e.ClickCount == 2)
                {
                    // A double click on an object: its inspector (a chest's contents, a sign's text...).
                    Dispatcher.UIThread.Post(() => InspectAsked?.Invoke());
                }
                Wake();
                return;
            }
            // With a brush, the left button paints (the middle one still slides the view); Alt + click picks
            // the ground's height (Alt + Shift: for the Mask).
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                _dragFrom = null;
                if (WorldAt(p.Position, _surfaceSize) is { } w)
                {
                    BrushAltClick?.Invoke(w.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                }
                return;
            }
            // Stamp once: a click of Raise or Lower puts the whole stamp in.
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool.Raise or BrushTool.Lower && Scene is { Session.Brush: { StampOnce: true, Shape: BrushShape.Stamp } } ss)
            {
                _dragFrom = null;
                if (GroundAt(ss, _lastViewProj, p.Position, _surfaceSize) is { } at)
                {
                    StampClicked?.Invoke(at.X, at.Z);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool tool && Scene is { Session: { } session } s)
            {
                _dragFrom = null;
                if (GroundAt(s, _lastViewProj, p.Position, _surfaceSize) is { } at)
                {
                    session.BeginStroke(tool, at.X, at.Z);
                    _brushDown = true;
                }
            }
            Wake();
        };
        surface.PointerReleased += (_, e) =>
        {
            if (_pickSwallow)
            {
                _pickSwallow = false;
                _pressAt = null;
                _dragFrom = null;
                e.Pointer.Capture(null);
                return;
            }
            // A left click (not a drag) picks the object under the pointer.
            var at = e.GetPosition(surface);
            if (_pathDown)
            {
                _pathDown = false;
                Path.Up();
            }
            if (_areaDown)
            {
                _areaDown = false;
                Area.Up();
            }
            if (_placeDown)
            {
                _placeDown = false;
                Place?.Up(at, surface.Bounds.Size);
            }
            if (_selectDown)
            {
                _selectDown = false;
                SelectTool.Up(at, surface.Bounds.Size, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control));
            }
            else if (_brushDown)
            {
                _brushDown = false;
                string message = Scene?.Session?.EndStroke() ?? "";
                StrokeEnded?.Invoke(message);
            }
            else if (Mode == ToolMode.View && !Panning && _dragButton == PointerUpdateKind.LeftButtonPressed && _pressAt is Point from && Point.Distance(from, at) < 5)
            {
                Pick(at, surface.Bounds.Size, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control),
                    e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Control));
            }
            _pressAt = null;
            _dragFrom = null;
            Panning = false;
            e.Pointer.Capture(null);
            Wake();
        };
        surface.PointerMoved += (_, e) =>
        {
            _pointer = e.GetPosition(surface);
            _surfaceSize = surface.Bounds.Size;
            if (_pathDown)
            {
                Path.Moved(GridAt(_pointer.Value, _surfaceSize));
            }
            if (_areaDown)
            {
                Area.Moved(GridAt(_pointer.Value, _surfaceSize));
            }
            if (Mode == ToolMode.Paste)
            {
                Paste.At = GridAt(_pointer.Value, _surfaceSize);
            }
            if (Mode == ToolMode.Place)
            {
                Place?.Moved(_pointer.Value, _surfaceSize);
            }
            if (_selectDown)
            {
                SelectTool.Moved(_pointer.Value, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Control));
            }
            else if (SelectMode && Tool == null)
            {
                SelectTool.Hover(_pointer.Value, _surfaceSize);
                // The object a click would pick (none over a move handle).
                SetHoverObject(SelectTool.OverHandle ? null : ObjectAt(_pointer.Value, _surfaceSize));
            }
            else if (Mode == ToolMode.Dungeon && WorldRay(_pointer.Value, _surfaceSize) is { } ray)
            {
                Dungeon.Hover(ray.O, ray.D);
            }
            else if (Mode == ToolMode.Measure && WorldAt(_pointer.Value, _surfaceSize) is { } w)
            {
                Tape.Move(w);
            }
            Drag(_pointer.Value);
        };
        surface.PointerExited += (_, _) =>
        {
            _pointer = null;
            SetHoverObject(null);
            Wake();
        };
        surface.PointerWheelChanged += (_, e) =>
        {
            // Ctrl or Alt + wheel: one step per notch. Smooth-scrolling wheels and touchpads send parts of a
            // notch (and some systems send Shift's as sideways): added up until a whole one.
            bool ctrlW = e.KeyModifiers.HasFlag(KeyModifiers.Control), altW = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
            // Ctrl: building lifts the piece; Alt: the tools that turn things turn them. Else the view zooms.
            bool wants = ctrlW ? (Mode == ToolMode.Place && Place?.Tool.Building == true || Mode == ToolMode.Paste) && CtrlWheel != null
                : altW && AltWheel != null && Mode is ToolMode.Place or ToolMode.Paste or ToolMode.Select;
            if (wants)
            {
                double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
                bool shiftW = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                _wheelSum += delta;
                bool tried = false, used = false;
                while (Math.Abs(_wheelSum) >= 0.999)
                {
                    int dir = _wheelSum > 0 ? 1 : -1;
                    _wheelSum -= dir;
                    tried = true;
                    used |= ctrlW ? CtrlWheel!(dir, shiftW) : AltWheel!(dir > 0 ? Key.OemComma : Key.OemPeriod, shiftW);
                }
                // Nothing to turn (or lift): the wheel zooms as usual.
                if (!tried || used)
                {
                    e.Handled = true;
                    Wake();
                    return;
                }
                _wheelSum = 0;
            }
            else
            {
                _wheelSum = 0;
            }
            lock (_camLock)
            {
                if (_eye == EyeMode.Orbit)
                {
                    _distance = Math.Clamp(_distance * MathF.Pow(0.88f, (float)e.Delta.Y), 3, 3000);
                }
            }
            Wake();
        };
        // Space is the view's (held: Space + left drag slides, flying goes up), not the focused
        // button's, which it would press; typing keeps it.
        window.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Space && e.Source is not TextBox)
            {
                lock (_keys)
                {
                    _keys.Add(Key.Space);
                }
                e.Handled = true;
                Wake();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Space && e.Source is not TextBox)
            {
                lock (_keys)
                {
                    _keys.Remove(Key.Space);
                }
                e.Handled = true;
                Wake();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
            {
                CycleEye();
                return;
            }
            lock (_keys)
            {
                _keys.Add(e.Key);
            }
            Wake();
        };
        window.KeyUp += (_, e) =>
        {
            lock (_keys)
            {
                _keys.Remove(e.Key);
            }
            Wake();
        };
        // Keys held when the window loses the focus would keep moving the camera.
        window.Deactivated += (_, _) => { lock (_keys) { _keys.Clear(); } };
    }

    /// <summary>Where the mouse button went down, to tell a click from a drag.</summary>
    private Point? _pressAt;

    /// <summary>The drag slides the view (Space + left, Shift + right).</summary>
    internal bool Panning { get; private set; }

    /// <summary>The area shown, null before one is.</summary>
    internal WorldScene? Scene { get; private set; }
    /// <summary>
    /// The Select tool takes the left button (see SelectTool); otherwise it slides the view and clicks pick.
    /// </summary>
    public SelectTool SelectTool { get; }

    /// <summary>The tool in use; leaving Select commits a move under way.</summary>
    public ToolMode Mode { get; set { if (value != ToolMode.Select) { SelectTool.Commit(); } field = value; Wake(); } }
    /// <summary>True while the Select tool is in use.</summary>
    public bool SelectMode => Mode == ToolMode.Select;
    /// <summary>The Measure tool's tape.</summary>
    public MeasureTool Tape { get; } = new();

    // ---- The Dungeon tool: its state, and what it shows (the free openings, the room selected or
    // pointed at, and the room a click would add, see-through).
    /// <summary>
    /// The Dungeon tool: its state, and what it shows (the free openings, the room selected or pointed at, and
    /// the room a click would add, see-through).
    /// </summary>
    public DungeonTool Dungeon { get; } = new();

    /// <summary>
    /// The Dungeon tool's lines on the graphics card: openings, the picked room, the room to add, door joints.
    /// </summary>
    private uint _dungeonVao, _dungeonVbo, _dungeonPickVao, _dungeonPickVbo, _dungeonAddVao, _dungeonAddVbo, _dungeonDoorVao, _dungeonDoorVbo, _dungeonJointVao, _dungeonJointVbo;

    /// <summary>
    /// The Dungeon tool's marks: free openings (green), joints that can take a door (blue with one, orange without),
    /// the room selected or pointed at, and the room a click adds (yellow, red when it would not fit).
    /// </summary>
    private void DrawDungeon(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Dungeon || Dungeon.Dungeon is not { } d)
        {
            return;
        }
        Vector3 V(Vector3 w) => new(w.X - s.Cx, w.Y, -(w.Z - s.Cz));
        // Free openings: a square across each, 2 m wide, 3 m high, standing on its floor point.
        var ends = new List<float>();
        var hovered = new List<float>();
        var free = Dungeon.FreeEnds;
        for (int i = 0; i < free.Count; i++)
        {
            var e = free[i];
            var side = Vector3.Transform(Vector3.UnitX, e.Rotation);
            var up = Vector3.UnitY;
            Vector3[] c = [e.Position - side, e.Position + side, e.Position + side + up * 3, e.Position - side + up * 3];
            var list = Dungeon.HoverEnd == i ? hovered : ends;
            for (int k = 0; k < 4; k++)
            {
                var a = V(c[k]);
                var b = V(c[(k + 1) % 4]);
                list.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            }
            var m = V(e.Position + up * 1.5f);
            var n = V(e.Position + up * 1.5f + Vector3.Transform(Vector3.UnitZ, e.Rotation) * -1.5f);
            list.AddRange([m.X, m.Y, m.Z, n.X, n.Y, n.Z]);
        }
        if (ends.Count > 0)
        {
            DrawLines(ref _dungeonVao, ref _dungeonVbo, [.. ends], vp, new Vector4(0.35f, 0.95f, 0.45f, 0.95f), blend: true, width: 2f);
        }
        // Joints that can take a door: a small square, blue with a door, orange without; a click puts one
        // or takes it away.
        var withDoor = new List<float>();
        var noDoor = new List<float>();
        var joints = Dungeon.DoorJoints;
        for (int i = 0; i < joints.Count; i++)
        {
            var j = joints[i];
            var side = Vector3.Transform(Vector3.UnitX, j.Rotation) * 0.6f;
            var up = Vector3.UnitY;
            Vector3[] c = [j.Position + up * 0.9f - side, j.Position + up * 0.9f + side, j.Position + up * 2.1f + side, j.Position + up * 2.1f - side];
            var list = Dungeon.HoverJoint == i ? hovered : Dungeon.DoorsAt(j).Count > 0 ? withDoor : noDoor;
            for (int k = 0; k < 4; k++)
            {
                var a = V(c[k]);
                var b = V(c[(k + 1) % 4]);
                list.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            }
        }
        if (withDoor.Count > 0)
        {
            DrawLines(ref _dungeonDoorVao, ref _dungeonDoorVbo, [.. withDoor], vp, new Vector4(0.4f, 0.7f, 1f, 0.95f), blend: true, width: 2f);
        }
        if (noDoor.Count > 0)
        {
            DrawLines(ref _dungeonJointVao, ref _dungeonJointVbo, [.. noDoor], vp, new Vector4(1f, 0.6f, 0.2f, 0.9f), blend: true, width: 1.5f);
        }
        // The room selected (white) or pointed at (faint), as its box.
        var boxes = new List<float>();
        foreach (int i in new[] { Dungeon.Selected, Dungeon.HoverRoom }.OfType<int>().Distinct().Where(i => i < d.Rooms.Count))
        {
            BoxLines(boxes, Dungeons.Box(d.Rooms[i]), V);
        }
        if (boxes.Count > 0)
        {
            DrawLines(ref _dungeonPickVao, ref _dungeonPickVbo, [.. boxes], vp, new Vector4(1, 0.9f, 0.6f, 0.9f), blend: true, width: 1.5f);
        }
        // The room a click adds: its model see-through, its box green, or red when it would not fit.
        if (Dungeon.Preview is { } p)
        {
            DrawInstances(s, [(p.Hash, p.Position, BlueprintFormats.ToEuler(p.Rotation), 0f)], ghost: true);
            BoxLines(hovered, Dungeons.Box(p), V);
        }
        if (hovered.Count > 0)
        {
            bool ok = Dungeon.Preview is not { } pp || Dungeon.Problem(pp) == null;
            DrawLines(ref _dungeonAddVao, ref _dungeonAddVbo, [.. hovered], vp, ok ? new Vector4(1, 0.95f, 0.3f, 1) : new Vector4(1, 0.3f, 0.25f, 1), blend: true, width: 2.5f);
        }
    }

    /// <summary>
    /// The twelve edges of a turned box (centre, half size at least 25 cm, turn) as line pairs, through
    /// <paramref name="view"/>.
    /// </summary>
    private static void BoxLines(List<float> into, (Vector3 C, Vector3 H, Quaternion R) box, Func<Vector3, Vector3> view)
    {
        Vector3 h = new(Math.Max(box.H.X, 0.25f), Math.Max(box.H.Y, 0.25f), Math.Max(box.H.Z, 0.25f));
        Vector3 Corner(int k) => view(box.C + Vector3.Transform(new Vector3((k & 1) == 0 ? -h.X : h.X, (k & 2) == 0 ? -h.Y : h.Y, (k & 4) == 0 ? -h.Z : h.Z), box.R));
        foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
        {
            var p = Corner(a);
            var q = Corner(b);
            into.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        }
    }
    /// <summary>Shape tool: a click on the ground (grid point).</summary>
    public event Action<float, float>? ShapeClicked;
    /// <summary>The radius the Shape tool's outline shows, in metres.</summary>
    public float ShapeRadius { get; set; } = 16;

    /// <summary>
    /// The Mountain tool's shape (metres added east and north of the middle), drawn under the pointer as
    /// a mesh on the ground before clicking; null: none.
    /// </summary>
    internal Func<float, float, float>? MountainPreview
    {
        get; set
        {
            field = value;
            _previewKey = null;
            RequestNextFrameRendering();
        }
    }
    /// <summary>The Path tool's state (its points, width, soft edge).</summary>
    public PathTool Path { get; } = new();
    /// <summary>Stamp once: a click on the ground (grid point).</summary>
    public event Action<float, float>? StampClicked;
    /// <summary>Alt + click with a brush: the ground's height there (shift: Alt + Shift).</summary>
    public event Action<float, bool>? BrushAltClick;
    /// <summary>The Area tool's state (its outline).</summary>
    public AreaTool Area { get; } = new();
    /// <summary>The Place tool's mouse (set by the window).</summary>
    public PlaceInput? Place { get; set; }
    /// <summary>The left button went down with the Place tool.</summary>
    private bool _placeDown;
    /// <summary>The Paste tool's state (what is pasted, and where).</summary>
    public PasteTool Paste { get; } = new();
    /// <summary>Paste tool: a click on the ground (grid point).</summary>
    public event Action<Vector2>? PasteClicked;
    /// <summary>The left button went down with the Area tool.</summary>
    private bool _areaDown;
    /// <summary>The left button went down with the Path tool.</summary>
    private bool _pathDown;

    /// <summary>Where a grid point (on the ground, lifted a little) is on the view, or null behind the camera.</summary>
    internal Point? ScreenOfGrid(Vector2 g, float lift = 0.6f)
    {
        var s = Scene;
        var size = _surfaceSize.Width > 0 ? _surfaceSize : Bounds.Size;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        float x = g.X - (s.W - 1) / 2f, z = -(g.Y - (s.H - 1) / 2f);
        var q = Vector4.Transform(new Vector4(x, Picking.HeightAt(s, x, z) + lift, z, 1), _lastViewProj);
        if (q.W <= 0)
        {
            return null;
        }
        return new Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }

    /// <summary>The grid point of the ground under a point of the view (null: none).</summary>
    internal Vector2? GridAt(Point at, Size size) => Scene is { } s && GroundAt(s, _lastViewProj, at, size) is { } g ? new Vector2(g.X, g.Z) : null;

    /// <summary>The ground's height at a grid point (0 with no area).</summary>
    internal float GridHeight(Vector2 g)
    {
        var s = Scene;
        return s == null ? 0 : Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Y - (s.H - 1) / 2f));
    }
    /// <summary>The left button went down with the Select tool.</summary>
    private bool _selectDown;

    /// <summary>The world point (x east, height, z north) of the ground under a point of the view, or null.</summary>
    internal Vector3? WorldAt(Point at, Size size)
    {
        var s = Scene;
        if (s == null || GroundAt(s, _lastViewProj, at, size) is not { } g)
        {
            return null;
        }
        float wx = s.X0 * 64f - 32f + g.X, wz = s.Z0 * 64f - 32f + g.Z;
        return new Vector3(wx, Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Z - (s.H - 1) / 2f)), wz);
    }

    /// <summary>Where the camera was at the last frame, in view space.</summary>
    private Vector3 _lastEye;
    /// <summary>
    /// The orbit camera on world point (x, z) at the ground, turned yaw° around it, looking down pitch°
    /// (the documentation's pictures, through the driver).
    /// height: around that height instead (inside a dungeon).
    /// </summary>
    internal void Orbit(float x, float z, float yawDegrees, float pitchDegrees, float distance, float? height = null)
    {
        if (Scene is not { } s)
        {
            return;
        }
        lock (_camLock)
        {
            var t = new Vector3(x - s.Cx, 0, -(z - s.Cz));
            int gx = Math.Clamp((int)MathF.Round(t.X + (s.W - 1) / 2f), 0, s.W - 1), gz = Math.Clamp((int)MathF.Round(-t.Z + (s.H - 1) / 2f), 0, s.H - 1);
            t.Y = height ?? Math.Max(s.Heights[gz * s.W + gx], s.Water);
            _target = t;
            _yaw = yawDegrees * MathF.PI / 180;
            _pitch = Math.Clamp(pitchDegrees * MathF.PI / 180, 0.05f, 1.55f);
            _distance = Math.Clamp(distance, 3, 3000);
        }
        Wake();
    }

    /// <summary>The camera turned onto a world point, close (the map's search: the object found).</summary>
    internal void Focus(Vector3 world, float distance = 35)
    {
        if (Scene is not { } s)
        {
            return;
        }
        lock (_camLock)
        {
            _target = new Vector3(world.X - s.Cx, world.Y, -(world.Z - s.Cz));
            _distance = distance;
        }
        Wake();
    }

    /// <summary>The camera with its target in world metres: kept when another area opens (null: no area).</summary>
    internal (float Yaw, float Pitch, float Distance, Vector3 World)? WorldCamera
    {
        get
        {
            if (Scene is not { } s)
            {
                return null;
            }
            lock (_camLock)
            {
                return (_yaw, _pitch, _distance, new Vector3(_target.X + s.Cx, _target.Y, -_target.Z + s.Cz));
            }
        }
        set
        {
            if (Scene is not { } s || value is not var (yaw, pitch, distance, world))
            {
                return;
            }
            lock (_camLock)
            {
                (_yaw, _pitch, _distance) = (yaw, pitch, distance);
                _target = new Vector3(world.X - s.Cx, world.Y, -(world.Z - s.Cz));
            }
            Wake();
        }
    }

    /// <summary>
    /// Live: the players online, in world metres (blue posts with a beam up; their names are labels
    /// the window places over the view).
    /// </summary>
    internal IReadOnlyList<(string Name, Vector3 World)> Players
    {
        get; set
        {
            field = value;
            Wake();
        }
    } = [];

    /// <summary>The players' posts on the graphics card.</summary>
    private uint _playersVao, _playersVbo;

    /// <summary>A player's post: a body 1.9 m tall (rings and sides) and a 40 m beam, in view space.</summary>
    internal static float[] PlayerPosts(WorldScene s, IEnumerable<(string Name, Vector3 World)> players)
    {
        var data = new List<float>();
        void Seg(Vector3 p, Vector3 q) => data.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        foreach (var (_, w) in players)
        {
            var c = new Vector3(w.X - s.Cx, w.Y, -(w.Z - s.Cz));
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * MathF.Tau / 12, a1 = (i + 1) * MathF.Tau / 12;
                var p0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * 0.45f;
                var p1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * 0.45f;
                Seg(c + p0, c + p1);
                Seg(c + p0 + new Vector3(0, 1.9f, 0), c + p1 + new Vector3(0, 1.9f, 0));
                if (i % 3 == 0)
                {
                    Seg(c + p0, c + p0 + new Vector3(0, 1.9f, 0));
                }
            }
            Seg(c, c + new Vector3(0, 40, 0));
        }
        return [.. data];
    }

    /// <summary>The live players' posts, light blue.</summary>
    private void DrawPlayers(WorldScene s, Matrix4x4 vp)
    {
        if (Players.Count == 0)
        {
            return;
        }
        DrawLines(ref _playersVao, ref _playersVbo, PlayerPosts(s, Players), vp, new Vector4(0.31f, 0.76f, 1, 1));
    }

    /// <summary>For tests (nothing is drawn headless): the camera the mouse is read with.</summary>
    internal void SetCamera(Vector3 eye, Vector3 target, float aspect)
    {
        _lastEye = eye;
        _lastViewProj = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * Perspective(60 * MathF.PI / 180, aspect, 0.5f, 6000);
    }
    /// <summary>The camera's view × projection of the last frame (or <c>SetCamera</c>).</summary>
    internal Matrix4x4 ViewProj => _lastViewProj;

    /// <summary>The ray from the camera through a point of the view (view space).</summary>
    internal (Vector3 O, Vector3 D) RayAt(Point at, Size size) =>
        Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));

    /// <summary>
    /// Where the Select tool's handles go: the middle of the selection (following a move in progress), in
    /// view space, and their scale (they keep their size on screen). Null when nothing is selected.
    /// </summary>
    internal (Vector3 Centre, float Scale)? GizmoAt()
    {
        var s = Scene;
        if (s == null || !SelectMode || Tool != null)
        {
            return null;
        }
        var previews = _previews;
        Vector3 sum = Vector3.Zero;
        int n = 0;
        lock (s.Things)
        {
            foreach (int i in Selected)
            {
                var t = previews.TryGetValue(i, out var p) ? p : s.Things[i];
                if (t.Gone)
                {
                    continue;
                }
                sum += new Vector3(t.Position.X - s.Cx, t.Position.Y, -(t.Position.Z - s.Cz));
                n++;
            }
        }
        if (n == 0)
        {
            return null;
        }
        var c = sum / n;
        return (c, Vector3.Distance(_lastEye, c) * 0.07f);
    }

    /// <summary>Each move handle's triangles on the graphics card, made at first use.</summary>
    private readonly Dictionary<Gizmo.Handle, (uint Vao, int Count)> _gizmoGl = [];
    /// <summary>
    /// The Select tool's move handles over the selection, over everything; the one under the pointer lit.
    /// </summary>
    private unsafe void DrawGizmo(WorldScene s, Matrix4x4 vp)
    {
        if (GizmoAt() is not var (c, scale))
        {
            return;
        }
        if (_lineProg == 0)
        {
            _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        }
        if (_gizmoGl.Count == 0)
        {
            foreach (var h in Enum.GetValues<Gizmo.Handle>())
            {
                float[] mesh = Gizmo.Mesh(h);
                uint vao = _own.VertexArray(), vbo = _own.Buffer();
                _gl.BindVertexArray(vao);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
                fixed (float* p = mesh)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(mesh.Length * 4), p, BufferUsageARB.StaticDraw);
                }
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
                _gizmoGl[h] = (vao, mesh.Length / 3);
            }
        }
        // The unit handles placed and scaled by the matrix (the line shader only knows view × projection).
        var m = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(c) * vp;
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&m);
        int uColor = _gl.GetUniformLocation(_lineProg, "uColor");
        var hot = SelectTool.HotHandle;
        foreach (var (h, (vao, count)) in _gizmoGl)
        {
            var col = Gizmo.Color(h, hot == h);
            _gl.Uniform4(uColor, col.X, col.Y, col.Z, col.W);
            _gl.BindVertexArray(vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)count);
        }
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>The lasso changed: its lines are made again at the next frame.</summary>
    private volatile bool _lassoDirty;
    /// <summary>Tells the view the Select tool's lasso changed.</summary>
    internal void LassoChanged()
    {
        _lassoDirty = true;
        Wake();
    }

    // ---- Tools: a sculpt or paint brush (null: looking around and picking objects).
    /// <summary>The sculpt or paint brush in use; null: looking around and picking objects.</summary>
    public BrushTool? Tool { get; set { field = value; Wake(); } }
    /// <summary>A stroke finished, with what to say about it ("" when nothing).</summary>
    public event Action<string>? StrokeEnded;
    /// <summary>Where the pointer is over the view, null when it is outside.</summary>
    private Point? _pointer;
    /// <summary>The size of the surface taking the mouse, in its own units.</summary>
    private Size _surfaceSize;
    /// <summary>A brush stroke is on (the left button held).</summary>
    private volatile bool _brushDown;
    /// <summary>The brush's grid point under the pointer at the last frame, null when off the ground.</summary>
    private (float X, float Z)? _hover;
    /// <summary>For tests: the brush's grid point under the mouse.</summary>
    internal (float X, float Z)? Hover => _hover;
    /// <summary>For tests: whether the brush is painting.</summary>
    internal bool BrushDown => _brushDown;

    /// <summary>The grid point (fractional) of the ground under a point of the view, or null.</summary>
    private static (float X, float Z)? GroundAt(WorldScene s, Matrix4x4 vp, Point at, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(vp, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        if (Picking.HitGround(s, o, d) is not float t)
        {
            return null;
        }
        var p = o + d * t;
        return (p.X + (s.W - 1) / 2f, -p.Z + (s.H - 1) / 2f);
    }

    /// <summary>The ground's height at a world point.</summary>
    internal float GroundHeight(float wx, float wz)
    {
        var s = Scene;
        return s == null ? 0 : Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
    }

    /// <summary>Half a tool line's width per metre from the eye (set each frame from the view's size).</summary>
    private float _linePx = 0.0015f;

    /// <summary>
    /// Line segments (pairs of points) as thin strips facing the eye, <paramref name="width"/> times a tool line's
    /// width: they stay whole at any resolution and angle, where 1-pixel lines broke into dashes.
    /// </summary>
    private float[] Thicken(float[] seg, float width)
    {
        float[] o = new float[seg.Length / 6 * 18];
        int n = 0;
        for (int i = 0; i + 5 < seg.Length; i += 6)
        {
            var a = new Vector3(seg[i], seg[i + 1], seg[i + 2]);
            var b = new Vector3(seg[i + 3], seg[i + 4], seg[i + 5]);
            Vector3 d = b - a;
            if (d.LengthSquared() < 1e-12f)
            {
                continue;
            }
            var side = Vector3.Cross(d, (a + b) / 2 - _lastEye);
            if (side.LengthSquared() < 1e-12f)
            {
                side = Vector3.Cross(d, Vector3.UnitY);
            }
            if (side.LengthSquared() < 1e-12f)
            {
                side = Vector3.UnitX;
            }
            side = Vector3.Normalize(side);
            Vector3 sa = side * (Vector3.Distance(_lastEye, a) * _linePx * width), sb = side * (Vector3.Distance(_lastEye, b) * _linePx * width);
            foreach (var v in new[] { a - sa, a + sa, b + sb, a - sa, b + sb, b - sb })
            {
                o[n++] = v.X;
                o[n++] = v.Y;
                o[n++] = v.Z;
            }
        }
        return n == o.Length ? o : o[..n];
    }

    /// <summary>
    /// Lines (pairs of view-space points) drawn over everything in one colour, refilled every call;
    /// <paramref name="vao"/> and <paramref name="vbo"/> are made at the first.
    /// </summary>
    /// <param name="vao">The vertex array kept for these lines (0 until made).</param>
    /// <param name="vbo">Its buffer (0 until made).</param>
    /// <param name="data">The points, three floats each.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="color">The colour, with alpha (used when <paramref name="blend"/>).</param>
    /// <param name="kind">Lines, or triangles as they are.</param>
    /// <param name="blend">See-through (alpha).</param>
    /// <param name="width">For line segments, times a tool line's width (0: 1-pixel lines).</param>
    private unsafe void DrawLines(ref uint vao, ref uint vbo, float[] data, Matrix4x4 vp, Vector4 color, PrimitiveType kind = PrimitiveType.Lines, bool blend = false, float width = 1)
    {
        if (kind == PrimitiveType.Lines && width > 0)
        {
            data = Thicken(data, width);
            kind = PrimitiveType.Triangles;
        }
        if (data.Length == 0)
        {
            return;
        }
        if (_lineProg == 0)
        {
            _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        }
        if (vao == 0)
        {
            vao = _own.VertexArray();
            vbo = _own.Buffer();
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
        }
        _gl.Disable(EnableCap.DepthTest);
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform4(_gl.GetUniformLocation(_lineProg, "uColor"), color.X, color.Y, color.Z, color.W);
        _gl.BindVertexArray(vao);
        if (blend)
        {
            // See-through (alpha), on both sides (ribbons on slopes may face away).
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        bool cull = _gl.IsEnabled(EnableCap.CullFace);
        _gl.Disable(EnableCap.CullFace);
        _gl.DrawArrays(kind, 0, (uint)(data.Length / 3));
        if (cull)
        {
            _gl.Enable(EnableCap.CullFace);
        }
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>
    /// Whether a new object (id &lt; 0) is in the world already (saved, or live: applied); such objects lose their
    /// marker. Call <c>RefreshNewMarkers</c> when that changes.
    /// </summary>
    public Func<int, bool> InGame { get; set; } = _ => false;

    /// <summary>The green marks on new objects can be hidden (View: Unsaved marks).</summary>
    public bool ShowNewMarkers { get; set; } = true;

    /// <summary>
    /// The new objects not saved (or applied) yet, as the web editor marked them: a green dot of the same
    /// size on screen just above each, drawn over everything, for the kinds shown.
    /// </summary>
    internal List<Vector3> NewMarkers(WorldScene s)
    {
        var list = new List<Vector3>();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    var t = s.Things[i];
                    if (t.Id < 0 && !t.Gone && i < _kinds.Length && _shown[(int)_kinds[i]] && !InGame(t.Id))
                    {
                        list.Add(new Vector3(t.Position.X - s.Cx, t.Position.Y + 0.4f, -(t.Position.Z - s.Cz)));
                    }
                }
            }
        }
        return list;
    }

    /// <summary>
    /// The support check (the Workshop): each building piece tinted as the game's build mode shows its
    /// support (SupportTint). Per thing index: -1 full, 0..1, or -2 breaks. Null: off.
    /// </summary>
    private volatile Dictionary<int, float>? _support;

    /// <summary>Turns the support check on (values per thing index, see <c>_support</c>) or off (null).</summary>
    public void ShowSupport(Dictionary<int, float>? support)
    {
        _support = support;
        // The tints are in the instances: built again.
        lock (_groups)
        {
            foreach (var g in _groups.Values)
            {
                MarkDirty(g);
            }
        }
        Wake();
    }

    /// <summary>The support values shown, null when the check is off.</summary>
    internal Dictionary<int, float>? Support => _support;

    /// <summary>
    /// The game's colour for a support value (WearNTear.Highlight): light blue at full support (on the
    /// ground), else red to green as it gets stronger, more saturated and brighter the weaker; one that
    /// breaks as the weakest (red).
    /// </summary>
    internal static Vector3 SupportTint(float v)
    {
        if (v is > (-1.5f) and < 0)
        {
            return new Vector3(0.6f, 0.8f, 1f);
        }
        float t = Math.Clamp(v < 0 ? 0 : v, 0, 1);
        // Lerp(red, green, t) has hue 0..1/3 with full saturation and value: its HSV, then the game's S and V.
        float h = new Vector3(1 - t, t, 0) is var c && c.X >= c.Y ? c.Y / c.X / 6f : (2 - c.X / c.Y) / 6f;
        return HsvToRgb(h, 1 - 0.5f * t, 1.2f - 0.3f * t);
    }

    /// <summary>A colour from hue (0..1), saturation and value (value may go above 1).</summary>
    private static Vector3 HsvToRgb(float h, float s, float v)
    {
        float r = Math.Clamp(MathF.Abs(h * 6 - 3) - 1, 0, 1), g = Math.Clamp(2 - MathF.Abs(h * 6 - 2), 0, 1), b = Math.Clamp(2 - MathF.Abs(h * 6 - 4), 0, 1);
        return new Vector3((1 - s + s * r) * v, (1 - s + s * g) * v, (1 - s + s * b) * v);
    }

    /// <summary>How far from the camera spawn points and nests get a marker, metres.</summary>
    internal const float SpawnMarkerRange = 250;

    /// <summary>The marker colour of spawn points: orange for one that spawns once, red for one whose creature comes back.</summary>
    internal static Vector4 SpawnColour(bool respawns) => respawns ? new Vector4(0.95f, 0.2f, 0.25f, 1) : new Vector4(1f, 0.6f, 0.15f, 1);

    /// <summary>The marker colour of nests: purple.</summary>
    internal static readonly Vector4 NestColour = new(0.7f, 0.4f, 0.95f, 1);

    /// <summary>
    /// Spawn points and nests within <see cref="SpawnMarkerRange"/> of the camera, for the kinds shown: where their
    /// marker goes (above the stand-in box) and its colour (<see cref="SpawnColour"/>, <see cref="NestColour"/>).
    /// They have no model in the game (spawn points are invisible), so the marker shows where they are through
    /// anything in front.
    /// </summary>
    internal List<(Vector3 At, Vector4 Colour)> SpawnMarkers(WorldScene s)
    {
        var list = new List<(Vector3, Vector4)>();
        bool points = _shown[(int)ObjectKind.SpawnPoints], nests = _shown[(int)ObjectKind.Nests];
        if (!points && !nests)
        {
            return list;
        }
        var colours = new Dictionary<int, Vector4?>();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count && i < _kinds.Length; i++)
                {
                    var k = _kinds[i];
                    if (k is not (ObjectKind.SpawnPoints or ObjectKind.Nests) || (k == ObjectKind.SpawnPoints ? !points : !nests))
                    {
                        continue;
                    }
                    var t = s.Things[i];
                    var at = new Vector3(t.Position.X - s.Cx, t.Position.Y + 2.4f, -(t.Position.Z - s.Cz));
                    if (t.Gone || Vector3.DistanceSquared(at, _lastEye) > SpawnMarkerRange * SpawnMarkerRange)
                    {
                        continue;
                    }
                    if (!colours.TryGetValue(t.Prefab, out var c))
                    {
                        var spawn = Terrain.PrefabCatalog.SpawnOf(t.Prefab);
                        c = colours[t.Prefab] = k == ObjectKind.Nests ? NestColour : SpawnColour(spawn?.Respawns == true);
                    }
                    list.Add((at, c!.Value));
                }
            }
        }
        return list;
    }

    /// <summary>The spawn markers on the graphics card, one buffer per colour.</summary>
    private readonly Dictionary<Vector4, (uint Vao, uint Vbo)> _spawnMarkGl = [];

    /// <summary>
    /// The spawn points' and nests' markers (<c>SpawnMarkers</c>): a disc of the same size on screen above each, with
    /// a line down to the object, drawn over everything.
    /// </summary>
    private void DrawSpawnMarkers(WorldScene s, Matrix4x4 vp)
    {
        foreach (var byColour in SpawnMarkers(s).GroupBy(m => m.Colour))
        {
            var tri = new List<float>();
            var lines = new List<float>();
            foreach (var (c, _) in byColour)
            {
                Disc(tri, c, 0.008f);
                lines.AddRange([c.X, c.Y, c.Z, c.X, c.Y - 2.4f, c.Z]);
            }
            var (vao, vbo) = _spawnMarkGl.GetValueOrDefault(byColour.Key);
            DrawLines(ref vao, ref vbo, [.. tri], vp, byColour.Key, PrimitiveType.Triangles);
            DrawLines(ref vao, ref vbo, [.. lines], vp, byColour.Key, width: 2);
            _spawnMarkGl[byColour.Key] = (vao, vbo);
        }
    }

    /// <summary>
    /// Adds a little disc facing the camera to a triangle list, the same size on screen at any distance (its radius
    /// this much of the distance).
    /// </summary>
    private void Disc(List<float> tri, Vector3 c, float size)
    {
        var f = Vector3.Normalize(c - _lastEye);
        var right = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY) is var x && x.LengthSquared() > 1e-6f ? x : Vector3.UnitX);
        var up = Vector3.Cross(right, f);
        float r = Vector3.Distance(_lastEye, c) * size;
        for (int k = 0; k < 8; k++)
        {
            float a0 = k * MathF.Tau / 8, a1 = (k + 1) * MathF.Tau / 8;
            foreach (var v in new[] { c, c + r * (MathF.Cos(a0) * right + MathF.Sin(a0) * up), c + r * (MathF.Cos(a1) * right + MathF.Sin(a1) * up) })
            {
                tri.Add(v.X);
                tri.Add(v.Y);
                tri.Add(v.Z);
            }
        }
    }

    /// <summary>The new objects' markers on the graphics card, and their program.</summary>
    private uint _markVao, _markVbo, _markProg;
    /// <summary>How many markers the buffer holds.</summary>
    private int _markCount;
    /// <summary>
    /// The markers are found and sent to the graphics card again at the next frame (things changed, a kind shown or
    /// hidden, a save or apply); otherwise the buffer is drawn as it is: a large area can hold millions of new objects.
    /// </summary>
    private volatile bool _marksDirty = true;

    /// <summary>Has the new objects' markers found again at the next frame (after a save or apply, say).</summary>
    public void RefreshNewMarkers()
    {
        _marksDirty = true;
        Wake();
    }

    /// <summary>
    /// The new objects' green dots (<c>NewMarkers</c>), about 10 pixels across at any distance: one point each, kept
    /// on the graphics card until they change.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="vp">The camera's view and projection.</param>
    /// <param name="height">The drawing's height in pixels.</param>
    private unsafe void DrawNewMarkers(WorldScene s, Matrix4x4 vp, int height)
    {
        if (_markProg == 0)
        {
            _markProg = Program(Shaders.MarkVs, Shaders.MarkFs);
            _markVao = _own.VertexArray();
            _markVbo = _own.Buffer();
            _gl.BindVertexArray(_markVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _markVbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        }
        if (_marksDirty)
        {
            _marksDirty = false;
            var marks = NewMarkers(s);
            _markCount = marks.Count;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _markVbo);
            fixed (Vector3* p = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(marks))
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(marks.Count * 12), p, BufferUsageARB.StaticDraw);
            }
        }
        if (_markCount == 0)
        {
            return;
        }
        if (!_es)
        {
            // Desktop OpenGL takes the size from the shader only when asked (OpenGL ES always does).
            _gl.Enable(EnableCap.ProgramPointSize);
        }
        _gl.Disable(EnableCap.DepthTest);
        _gl.UseProgram(_markProg);
        _gl.UniformMatrix4(_gl.GetUniformLocation(_markProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform4(_gl.GetUniformLocation(_markProg, "uColor"), 0x5d / 255f, 1, 0x8a / 255f, 1);
        // As the discs they replace: 1.16% of the distance across, under the 60° field of view.
        _gl.Uniform1(_gl.GetUniformLocation(_markProg, "uSize"), MathF.Max(3, 0.0116f / (2 * MathF.Tan(MathF.PI / 6)) * height));
        _gl.BindVertexArray(_markVao);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_markCount);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>The Path tool's lines on the graphics card: centre line, edges, points.</summary>
    private uint _pathVao, _pathVbo, _pathEdgeVao, _pathEdgeVbo, _pathDotVao, _pathDotVbo;
    /// <summary>The Place tool's brush ring on the graphics card.</summary>
    private uint _placeRingVao, _placeRingVbo;
    /// <summary>
    /// The Path tool's bands on the graphics card: the ground it covers, the soft edge, and the next point's preview.
    /// </summary>
    private uint _pathBandVao, _pathBandVbo, _pathSoftVao, _pathSoftVbo, _pathPreviewVao, _pathPreviewVbo, _pathPreviewSoftVao, _pathPreviewSoftVbo;
    /// <summary>
    /// The Path tool: the ground it covers (a light band), its width's edges and the soft edge's outer
    /// line, the centre line (red) and its points; under the cursor, the width (and the soft edge) the
    /// next point brings, with the stretch from the last point to it. Lines are flat ribbons on the
    /// ground a few pixels wide, so they stay whole at any resolution and angle (1-pixel lines broke
    /// into dashes once the view was scaled up).
    /// </summary>
    private void DrawPath(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Path)
        {
            return;
        }
        List<(Vector2 P, int Seg)> curve;
        List<Vector2> pts;
        lock (Path.Points)
        {
            curve = Path.Curve();
            pts = [.. Path.Points];
        }
        Vector3 V(Vector2 g, float lift)
        {
            float x = g.X - (s.W - 1) / 2f, z = -(g.Y - (s.H - 1) / 2f);
            return new Vector3(x, Picking.HeightAt(s, x, z) + lift, z);
        }
        // Grid points every metre or so along a line (the ground's own spacing), so a ribbon follows it.
        static List<Vector2> Dense(IReadOnlyList<Vector2> line)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0)
                {
                    int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(line[i - 1], line[i])));
                    for (int k = 1; k < n; k++)
                    {
                        o.Add(Vector2.Lerp(line[i - 1], line[i], k / (float)n));
                    }
                }
                o.Add(line[i]);
            }
            return o;
        }
        // A line as a ribbon of triangles, its width a fixed share of the distance to the eye.
        void Ribbon(List<float> to, IReadOnlyList<Vector2> line, float px, float lift)
        {
            var d = Dense(line);
            for (int i = 1; i < d.Count; i++)
            {
                Vector2 a = d[i - 1], b = d[i];
                float len = Vector2.Distance(a, b);
                if (len < 1e-5f)
                {
                    continue;
                }
                Vector3 va = V(a, lift), vb = V(b, lift);
                Vector2 n = new Vector2(-(b.Y - a.Y), b.X - a.X) / len;
                float wa = Vector3.Distance(_lastEye, va) * px, wb = Vector3.Distance(_lastEye, vb) * px;
                Vector3 Off(Vector2 g, float w, Vector3 at) { var q = V(g + n * w, lift); return new Vector3(q.X, at.Y, q.Z); }
                Vector3 a0 = Off(a, -wa, va), a1 = Off(a, wa, va), b0 = Off(b, -wb, vb), b1 = Off(b, wb, vb);
                foreach (var v in new[] { a0, a1, b1, a0, b1, b0 })
                {
                    to.AddRange([v.X, v.Y, v.Z]);
                }
            }
        }
        // The line offset to one side (+1 left, -1 right) by a distance, point by point.
        static List<Vector2> Offset(List<Vector2> line, float by)
        {
            var o = new List<Vector2>(line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 dir = line[Math.Min(i + 1, line.Count - 1)] - line[Math.Max(i - 1, 0)];
                float len = dir.Length();
                o.Add(len < 1e-6f ? line[i] : line[i] + new Vector2(-dir.Y, dir.X) / len * by);
            }
            return o;
        }
        static List<Vector2> Circle(Vector2 c, float r)
        {
            int n = Math.Clamp((int)(r * 6), 32, 160);
            return [.. Enumerable.Range(0, n + 1).Select(i => c + new Vector2(MathF.Cos(i * MathF.Tau / n), MathF.Sin(i * MathF.Tau / n)) * r)];
        }
        float half = Path.Width / 2, outer = half + MathF.Max(0, Path.Soft);
        float thick = _linePx * 1.6f, thin = _linePx * 0.9f;
        var band = new List<float>();
        var edges = new List<float>();
        var soft = new List<float>();
        var line = new List<float>();
        void Stretch(List<Vector2> c)
        {
            if (c.Count < 2)
            {
                return;
            }
            // The band: strips across the width, one per metre along and across, on the ground.
            var d = Dense(c);
            int across = Math.Max(2, (int)MathF.Ceiling(Path.Width));
            var left = Offset(d, half);
            for (int i = 1; i < d.Count; i++)
            {
                Vector2 na = (left[i - 1] - d[i - 1]) / MathF.Max(half, 1e-6f), nb = (left[i] - d[i]) / MathF.Max(half, 1e-6f);
                for (int k = 0; k < across; k++)
                {
                    float u0 = -half + Path.Width * k / across, u1 = -half + Path.Width * (k + 1) / across;
                    Vector3 p00 = V(d[i - 1] + na * u0, 0.25f), p01 = V(d[i - 1] + na * u1, 0.25f), p10 = V(d[i] + nb * u0, 0.25f), p11 = V(d[i] + nb * u1, 0.25f);
                    foreach (var v in new[] { p00, p01, p11, p00, p11, p10 })
                    {
                        band.AddRange([v.X, v.Y, v.Z]);
                    }
                }
            }
            Ribbon(edges, Offset(d, half), thick, 0.35f);
            Ribbon(edges, Offset(d, -half), thick, 0.35f);
            if (Path.Soft > 0.01f)
            {
                Ribbon(soft, Offset(d, outer), thin, 0.3f);
                Ribbon(soft, Offset(d, -outer), thin, 0.3f);
            }
            Ribbon(line, c, thick, 0.45f);
        }
        Stretch([.. curve.Select(c => c.P)]);
        // The cursor: the width and soft edge a point there brings, and the stretch from the last point.
        var preview = new List<float>();
        var previewSoft = new List<float>();
        if (_hover is { } h && !Path.Drawing)
        {
            var at = new Vector2(h.X, h.Z);
            Ribbon(preview, Circle(at, half), thick, 0.35f);
            if (Path.Soft > 0.01f)
            {
                Ribbon(previewSoft, Circle(at, outer), thin, 0.3f);
            }
            if (pts.Count > 0 && Vector2.Distance(pts[^1], at) > 0.5f)
            {
                var next = new List<Vector2> { pts[^1], at };
                Ribbon(preview, Offset(Dense(next), half), thin, 0.35f);
                Ribbon(preview, Offset(Dense(next), -half), thin, 0.35f);
                Ribbon(preview, next, thin, 0.45f);
            }
        }
        // Each point: a small square that keeps its size on screen.
        var dots = new List<float>();
        void Seg(List<float> to, Vector3 a, Vector3 b) => to.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
        foreach (var p in pts)
        {
            var c = V(p, 0.6f);
            float r = Vector3.Distance(_lastEye, c) * 0.008f;
            Vector3[] k = [c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r)];
            for (int j = 0; j < 4; j++)
            {
                Seg(dots, k[j], k[(j + 1) % 4]);
            }
            Seg(dots, c - new Vector3(0, r, 0), c + new Vector3(0, r, 0));
        }
        DrawLines(ref _pathBandVao, ref _pathBandVbo, [.. band], vp, new Vector4(1, 0.45f, 0.35f, 0.16f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathSoftVao, ref _pathSoftVbo, [.. soft], vp, new Vector4(1, 0.8f, 0.7f, 0.55f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathEdgeVao, ref _pathEdgeVbo, [.. edges], vp, new Vector4(1, 0.75f, 0.65f, 0.95f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathVao, ref _pathVbo, [.. line], vp, new Vector4(1, 0.23f, 0.23f, 1), PrimitiveType.Triangles);
        DrawLines(ref _pathPreviewSoftVao, ref _pathPreviewSoftVbo, [.. previewSoft], vp, new Vector4(1, 1, 1, 0.5f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathPreviewVao, ref _pathPreviewVbo, [.. preview], vp, new Vector4(1, 1, 1, 0.9f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathDotVao, ref _pathDotVbo, [.. dots], vp, new Vector4(1, 0.69f, 0.63f, 1));
    }

    /// <summary>The Place tool's posts and its line, zone or grid on the graphics card.</summary>
    private uint _placeVao, _placeVbo, _placeShapeVao, _placeShapeVbo;
    /// <summary>The prefabs drawn as ghosts this frame: they get no post.</summary>
    private HashSet<int> _ghostsDrawn = [];
    /// <summary>
    /// The Place tool: a post at each placement the preview (or a stroke) shows, and the line, zone or
    /// grid being drawn with its points.
    /// </summary>
    private void DrawPlace(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Place || Place is not { } pl)
        {
            return;
        }
        float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
        var posts = new List<float>();
        foreach (var o in pl.Shown)
        {
            if (_ghostsDrawn.Contains(Save.StableHash.Of(o.Name)))
            {
                continue;
            }
            float x = o.Position.X - ox - (s.W - 1) / 2f, z = -(o.Position.Z - oz - (s.H - 1) / 2f), y = o.Position.Y;
            posts.AddRange([x, y, z, x, y + 1.5f, z, x - 0.35f, y + 0.6f, z, x + 0.35f, y + 0.6f, z, x, y + 0.6f, z - 0.35f, x, y + 0.6f, z + 0.35f]);
        }
        DrawLines(ref _placeVao, ref _placeVbo, [.. posts], vp, new Vector4(0.62f, 0.88f, 1, 1));
        var t = pl.Tool;
        // The brush's outline under the cursor: where a stroke places.
        if (t.Mode == PlaceTool.Modes.Brush && !t.Building && _hover is { } h)
        {
            var ring = new List<float>();
            var outline = t.Brush.Outline(96);
            for (int i = 0; i < outline.Count; i++)
            {
                foreach (var (ox2, oz2) in new[] { outline[i], outline[(i + 1) % outline.Count] })
                {
                    float x = h.X + ox2 - (s.W - 1) / 2f, z = -(h.Z + oz2 - (s.H - 1) / 2f);
                    ring.AddRange([x, Picking.HeightAt(s, x, z) + 0.3f, z]);
                }
            }
            DrawLines(ref _placeRingVao, ref _placeRingVbo, [.. ring], vp, new Vector4(0.62f, 0.88f, 1, 1), width: 1.2f);
        }
        Vector3 V(Vector2 g) { float x = g.X - (s.W - 1) / 2f, z = -(g.Y - (s.H - 1) / 2f); return new Vector3(x, Picking.HeightAt(s, x, z) + 0.3f, z); }
        var data = new List<float>();
        void Strip(IReadOnlyList<Vector2> pts)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(pts[i - 1], pts[i])));
                for (int k = 0; k < n; k++)
                {
                    var a = V(Vector2.Lerp(pts[i - 1], pts[i], k / (float)n));
                    var b = V(Vector2.Lerp(pts[i - 1], pts[i], (k + 1) / (float)n));
                    data.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
                }
            }
        }
        if (t.Mode == PlaceTool.Modes.Line)
        {
            foreach (var run in t.LineRuns())
            {
                Strip(run);
            }
        }
        else if (t.Mode == PlaceTool.Modes.Zone)
        {
            var z = pl.ShownPoints();
            if (z.Count > 1)
            {
                Strip([.. z, z[0]]);
            }
        }
        else if (t.Mode == PlaceTool.Modes.Grid && t.GridA is { } a && t.GridB is { } b)
        {
            Strip([.. new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y), a }.Select(t.Xf)]);
        }
        if (t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone)
        {
            foreach (var p in pl.ShownPoints())
            {
                var c = V(p);
                float r = Vector3.Distance(_lastEye, c) * 0.008f;
                Vector3[] k = [c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r)];
                for (int j = 0; j < 4; j++)
                {
                    data.AddRange([k[j].X, k[j].Y, k[j].Z, k[(j + 1) % 4].X, k[(j + 1) % 4].Y, k[(j + 1) % 4].Z]);
                }
            }
        }
        DrawLines(ref _placeShapeVao, ref _placeShapeVbo, [.. data], vp, new Vector4(0.62f, 0.88f, 1, 1));
    }

    /// <summary>The Area selection's (or the paste's) lines, and the reset zones', on the graphics card.</summary>
    private uint _areaVao, _areaVbo, _resetVao, _resetVbo;
    /// <summary>
    /// The Area selection (in the Area tool), following the ground, and the zones marked for reset (red, always).
    /// With the Paste tool, where the paste would go: its outlines, and a post at each object not drawn as a ghost.
    /// </summary>
    private void DrawArea(WorldScene s, Matrix4x4 vp)
    {
        Vector3 V(Vector2 g, float lift)
        {
            float x = g.X - (s.W - 1) / 2f, z = -(g.Y - (s.H - 1) / 2f);
            return new Vector3(x, Picking.HeightAt(s, x, z) + lift, z);
        }
        void Ring(List<float> to, List<Vector2> pts, bool closed, float lift)
        {
            int n = closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b)));
                for (int k = 0; k < steps; k++)
                {
                    var p = V(Vector2.Lerp(a, b, k / (float)steps), lift);
                    var q = V(Vector2.Lerp(a, b, (k + 1) / (float)steps), lift);
                    to.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
                }
            }
        }
        if (Mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
        {
            // Where the paste would go: its outlines and a small post at each object whose model is not
            // drawn as a ghost (DrawGhosts).
            var (objs, outlines) = Paste.Preview(pat, GridHeight);
            var data = new List<float>();
            foreach (var o in outlines)
            {
                Ring(data, o, true, 0.3f);
            }
            var ghosted = _pasteGhosts;
            for (int k = 0; k < objs.Count; k++)
            {
                if (ghosted != null && ghosted.Contains(Paste.Clip.Objects[k % Paste.Clip.Objects.Count].Prefab))
                {
                    continue;
                }
                var o = objs[k];
                float x = o.X - (s.W - 1) / 2f, z = -(o.Z - (s.H - 1) / 2f);
                data.AddRange([x, o.Y, z, x, o.Y + 1.2f, z, x - 0.3f, o.Y + 0.5f, z, x + 0.3f, o.Y + 0.5f, z]);
            }
            DrawLines(ref _areaVao, ref _areaVbo, [.. data], vp, new Vector4(1, 0.76f, 0.29f, 1));
        }
        if (Mode == ToolMode.Area)
        {
            var data = new List<float>();
            if (Area.Polygon() is { } poly)
            {
                Ring(data, poly, true, 0.3f);
            }
            else
            {
                List<Vector2> pts;
                lock (Area.Points)
                {
                    pts = [.. Area.Points];
                }
                if (pts.Count > 1)
                {
                    Ring(data, pts, false, 0.3f);
                }
            }
            DrawLines(ref _areaVao, ref _areaVbo, [.. data], vp, new Vector4(0.37f, 0.83f, 1, 1));
        }
        if (s.Session is { } session)
        {
            var data = new List<float>();
            foreach (var r in session.Resets)
            {
                float gx0 = (r.X - s.X0) * 64, gz0 = (r.Z - s.Z0) * 64;
                Ring(data, [new(gx0, gz0), new(gx0 + 64, gz0), new(gx0 + 64, gz0 + 64), new(gx0, gz0 + 64)], true, 0.6f);
            }
            DrawLines(ref _resetVao, ref _resetVbo, [.. data], vp, new Vector4(1, 0.31f, 0.25f, 1));
        }
    }

    /// <summary>The tape's lines on the graphics card.</summary>
    private uint _measureVao, _measureVbo;
    /// <summary>
    /// The tape: following the ground from A to B (its profile), and a cross at each end. Shown in the
    /// Measure tool, and kept once fixed.
    /// </summary>
    private void DrawMeasure(WorldScene s, Matrix4x4 vp)
    {
        var (a, b) = (Tape.A, Tape.B);
        if (a == null || Mode != ToolMode.Measure && !Tape.Fixed)
        {
            return;
        }
        var data = new List<float>();
        Vector3 V(float wx, float wz) => new(wx - s.Cx, GroundHeight(wx, wz) + 0.2f, -(wz - s.Cz));
        void Seg(Vector3 p, Vector3 q) => data.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        foreach (var e in new[] { a, b })
        {
            if (e is { } p)
            {
                var c = V(p.X, p.Z);
                Seg(c - new Vector3(0.6f, 0, 0), c + new Vector3(0.6f, 0, 0));
                Seg(c - new Vector3(0, 0, 0.6f), c + new Vector3(0, 0, 0.6f));
                Seg(c, c + new Vector3(0, 1.5f, 0));
            }
        }
        if (a is { } pa && b is { } pb)
        {
            int n = Math.Max(2, (int)MathF.Ceiling(Vector2.Distance(new Vector2(pa.X, pa.Z), new Vector2(pb.X, pb.Z))));
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                Seg(V(pa.X + (pb.X - pa.X) * t0, pa.Z + (pb.Z - pa.Z) * t0), V(pa.X + (pb.X - pa.X) * t1, pa.Z + (pb.Z - pa.Z) * t1));
            }
        }
        DrawLines(ref _measureVao, ref _measureVbo, [.. data], vp, new Vector4(1, 0.88f, 0.54f, 1));
    }

    /// <summary>The lasso's lines on the graphics card.</summary>
    private uint _lassoVao, _lassoVbo;
    /// <summary>The zone being drawn with the Select tool, on the ground.</summary>
    private unsafe void DrawLasso(WorldScene s, Matrix4x4 vp)
    {
        if (_lassoDirty)
        {
            _lassoDirty = false;
            var pts = SelectTool.Lasso?.ToList();
            var data = new List<float>();
            if (pts is { Count: > 1 })
            {
                pts.Add(pts[0]);
                float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
                for (int i = 1; i < pts.Count; i++)
                {
                    var a = pts[i - 1];
                    var b = pts[i];
                    int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b)));
                    for (int k = 0; k < n; k++)
                    {
                        foreach (float t in new[] { k / (float)n, (k + 1) / (float)n })
                        {
                            var p = Vector2.Lerp(a, b, t);
                            float x = p.X - ox - (s.W - 1) / 2f, z = -(p.Y - oz - (s.H - 1) / 2f);
                            data.AddRange([x, Picking.HeightAt(s, x, z) + 0.3f, z]);
                        }
                    }
                }
            }
            _lassoSegs = [.. data];
        }
        DrawLines(ref _lassoVao, ref _lassoVbo, _lassoSegs, vp, new Vector4(1, 0.76f, 0.29f, 1));
    }

    /// <summary>The lasso's lines, made again when it changes (<c>LassoChanged</c>).</summary>
    private float[] _lassoSegs = [];

    /// <summary>The brush ring's lines, and the mountain preview's, on the graphics card.</summary>
    private uint _ringVao, _ringVbo, _previewVao, _previewVbo;
    /// <summary>The mountain preview's lines, kept while the pointer stays on the same grid point.</summary>
    private float[] _previewSegs = [];
    /// <summary>The grid point (and area size) the mountain preview was made for; null to make it again.</summary>
    private (int X, int Z, int W, int H)? _previewKey;

    /// <summary>Whether a Mountain click at a grid point fits in the open area (what EditSession.Mountain needs).</summary>
    internal static bool MountainFits(WorldScene s, float gx, float gz, float reach) => gx - reach >= 1 && gz - reach >= 1 && gx + reach <= s.W - 2 && gz + reach <= s.H - 2;

    /// <summary>
    /// The brush's outline on the ground (and the Ring shape's inner edge), seen through what stands on it.
    /// Short segments, so a wide ring follows hills instead of cutting through them; none outside the open
    /// area (no ground there). For the Mountain tool: the mountain itself as a mesh, and the ring in red
    /// where it does not fit.
    /// </summary>
    private unsafe void DrawBrush(WorldScene s, Matrix4x4 vp)
    {
        if (_hover is not { } h || s.Session is not { } session || Tool == null && Mode is not (ToolMode.Shape or ToolMode.Mountain))
        {
            return;
        }
        float r = Tool == null ? ShapeRadius : session.Brush.Radius;
        int n = Math.Clamp((int)MathF.Ceiling(MathF.Tau * r / 1.5f), 96, 2048);
        var data = new List<float>();
        void Loop(List<(float X, float Z)> pts)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var (ax, az) = At(pts[i]);
                var (bx, bz) = At(pts[(i + 1) % pts.Count]);
                float ya = Picking.HeightAt(s, ax, az), yb = Picking.HeightAt(s, bx, bz);
                if (ya <= -1000 || yb <= -1000)
                {
                    continue;
                }
                data.AddRange([ax, ya + 0.3f, az, bx, yb + 0.3f, bz]);
            }
        }
        (float, float) At((float X, float Z) o) => (h.X + o.X - (s.W - 1) / 2f, -(h.Z + o.Z - (s.H - 1) / 2f));
        Loop(Tool == null ? [.. Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * ShapeRadius, MathF.Sin(i * MathF.Tau / n) * ShapeRadius))] : session.Brush.Outline(n));
        if (Tool != null && session.Brush.InnerOutline(n) is { } inner)
        {
            Loop(inner);
        }
        bool fits = Mode != ToolMode.Mountain || MountainFits(s, h.X, h.Z, ShapeRadius);
        DrawLines(ref _ringVao, ref _ringVbo, [.. data], vp, fits ? new Vector4(1, 1, 1, 1) : new Vector4(1, 0.35f, 0.3f, 1), width: 1.2f);
        if (Tool == null && Mode == ToolMode.Mountain && fits && MountainPreview is { } shape)
        {
            var key = ((int)MathF.Round(h.X), (int)MathF.Round(h.Z), s.W, s.H);
            if (_previewKey != key)
            {
                _previewKey = key;
                _previewSegs = MountainMesh(s, key.Item1, key.Item2, ShapeRadius, shape);
            }
            DrawLines(ref _previewVao, ref _previewVbo, _previewSegs, vp, new Vector4(1, 0.78f, 0.35f, 0.9f), width: 1f);
        }
    }

    /// <summary>
    /// The mountain as it would rise at grid point (cx, cz): lines along x and z every few metres, each
    /// point at the ground plus the shape's height; flat ground (under half a metre added) left out.
    /// </summary>
    internal static float[] MountainMesh(WorldScene s, int cx, int cz, float reach, Func<float, float, float> shape)
    {
        const int Cells = 40;
        float step = 2 * reach / Cells;
        float[] y = new float[(Cells + 1) * (Cells + 1)];
        bool[] up = new bool[y.Length];
        for (int j = 0; j <= Cells; j++)
        {
            for (int i = 0; i <= Cells; i++)
            {
                float dx = -reach + i * step, dz = -reach + j * step;
                float add = shape(dx, dz);
                float x = cx + dx - (s.W - 1) / 2f, z = -(cz + dz - (s.H - 1) / 2f);
                y[j * (Cells + 1) + i] = Picking.HeightAt(s, x, z) + add + 0.3f;
                up[j * (Cells + 1) + i] = add > 0.5f;
            }
        }
        var data = new List<float>();
        void Seg(int i0, int j0, int i1, int j1)
        {
            int a = j0 * (Cells + 1) + i0, b = j1 * (Cells + 1) + i1;
            if (!up[a] && !up[b])
            {
                return;
            }
            data.AddRange([cx - reach + i0 * step - (s.W - 1) / 2f, y[a], -(cz - reach + j0 * step - (s.H - 1) / 2f)]);
            data.AddRange([cx - reach + i1 * step - (s.W - 1) / 2f, y[b], -(cz - reach + j1 * step - (s.H - 1) / 2f)]);
        }
        for (int j = 0; j <= Cells; j++)
        {
            for (int i = 0; i <= Cells; i++)
            {
                if (i < Cells)
                {
                    Seg(i, j, i + 1, j);
                }
                if (j < Cells)
                {
                    Seg(i, j, i, j + 1);
                }
            }
        }
        return [.. data];
    }

    // ---- Selection: the objects picked (indices into the scene's things), drawn as orange boxes.
    /// <summary>Each thing's box in view space (for picking and the selection), by thing index.</summary>
    private (Vector3 Min, Vector3 Max)[] _bounds = [];
    /// <summary>Whether a thing's box is known (its model read, and it is not gone).</summary>
    private bool[] _known = [];
    /// <summary>Where each thing was when its box was made (a move's preview may be ahead of the boxes).</summary>
    private Vector3[] _boundsAt = [];
    /// <summary>Each thing's View kind, by thing index.</summary>
    private ObjectKind[] _kinds = [];
    /// <summary>The objects picked, as indices into the scene's things.</summary>
    private readonly HashSet<int> _selection = [];
    /// <summary>The camera's view × projection of the last frame, which the mouse is read with.</summary>
    private Matrix4x4 _lastViewProj;
    /// <summary>The selection changed: its boxes are made again at the next frame.</summary>
    private bool _selectionDirty;
    /// <summary>The selection changed: the things now selected.</summary>
    public event Action<IReadOnlyList<WorldScene.Thing>>? SelectionChanged;

    /// <summary>A double click in Select mode.</summary>
    public event Action? InspectAsked;
    /// <summary>The objects picked, as indices into the scene's things (a copy).</summary>
    internal IReadOnlyCollection<int> Selected { get { lock (_selection) { return [.. _selection]; } } }

    /// <summary>
    /// The Workshop's cut: nothing drawn above this height (world y), and the cursor goes through what is
    /// hidden; null: none.
    /// </summary>
    internal float? CutY
    {
        get; set
        {
            field = value;
            Wake();
        }
    }

    /// <summary>
    /// The view's ray through a point, in world space (x east, up, z north), and how far along it the
    /// ground is (null: it does not meet it).
    /// </summary>
    internal (Vector3 O, Vector3 D, float? GroundT)? WorldRay(Point at, Size size)
    {
        var s = Scene;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        d = Vector3.Normalize(d);
        float? g = Picking.HitGround(s, o, d);
        var wo = new Vector3(o.X + s.Cx, o.Y, -o.Z + s.Cz);
        var wd = new Vector3(d.X, d.Y, -d.Z);
        // A dungeon's rooms are the ground there (their floors and walls).
        if (_shown[(int)ObjectKind.Dungeons] && DungeonRooms.Shown(s, Wake) is { Count: > 0 } dungeons && RoomSurfaces.Hit(dungeons, Models, wo, wd, CutY ?? float.MaxValue) is float rt && (g == null || rt < g))
        {
            g = rt;
        }
        return (wo, wd, g);
    }

    /// <summary>
    /// The shown object under a point of the view (null: the ground or nothing is nearer), like the web
    /// editor: the nearest box the ray enters, unless the ground is hit first (half a metre of slack).
    /// </summary>
    internal int? ObjectAt(Point at, Size size)
    {
        var s = Scene;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        float best = float.MaxValue;
        int? hit = null;
        for (int i = 0; i < _bounds.Length; i++)
        {
            // Not what the cut hides (whole above it).
            if (!_known[i] || !_shown[(int)_kinds[i]] || CutY is float cut && _bounds[i].Min.Y > cut)
            {
                continue;
            }
            if (Picking.HitBox(o, d, _bounds[i].Min, _bounds[i].Max) is float t && t < best)
            {
                best = t;
                hit = i;
            }
        }
        if (hit != null && Picking.HitGround(s, o, d, best + 1) is float g && g + 0.5f < best)
        {
            return null;
        }
        return hit;
    }

    /// <summary>
    /// A click on an object: it alone, or (add) added or taken out; range (Shift): every shown object in
    /// a row from the last one clicked to this one (w1, Shift + click w3: w1, w2 and w3).
    /// </summary>
    internal void Pick(Point at, Size size, bool add, bool range = false)
    {
        int? i = ObjectAt(at, size);
        var s = Scene;
        lock (_selection)
        {
            if (range && i is int k && _anchor is int a && a != k && s != null && a < s.Things.Count && !s.Things[a].Gone)
            {
                foreach (int j in Between(s, a, k))
                {
                    _selection.Add(j);
                }
            }
            else
            {
                if (!add)
                {
                    _selection.Clear();
                }
                if (i is int k2 && !_selection.Remove(k2))
                {
                    _selection.Add(k2);
                }
                _anchor = i;
            }
        }
        SelectionDone();
    }

    /// <summary>The object a Shift + click's row starts from: the last one clicked.</summary>
    private int? _anchor;

    /// <summary>
    /// The shown objects on the line from object a to object b (their middles within 0.75 m of it), both
    /// ends included.
    /// </summary>
    internal IEnumerable<int> Between(WorldScene s, int a, int b)
    {
        var pa = s.Things[a].Position;
        var pb = s.Things[b].Position;
        var ab = pb - pa;
        float len2 = MathF.Max(ab.LengthSquared(), 1e-6f);
        for (int i = 0; i < s.Things.Count && i < _known.Length; i++)
        {
            var t = s.Things[i];
            if (t.Gone || !_known[i] || !_shown[(int)_kinds[i]])
            {
                continue;
            }
            float u = Math.Clamp(Vector3.Dot(t.Position - pa, ab) / len2, 0, 1);
            if (Vector3.Distance(t.Position, pa + ab * u) <= 0.75f)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// Eyedropper: the next left click gives the object under it (null: none) instead of going to
    /// the tool; its release is swallowed too.
    /// </summary>
    internal Action<int?>? PickObjectOnce { get; set; }
    /// <summary>The eyedropper's click was taken: its release is swallowed.</summary>
    private bool _pickSwallow;

    /// <summary>Selects these things (add: to what is selected).</summary>
    internal void Select(IEnumerable<int> things, bool add = false)
    {
        var s = Scene;
        lock (_selection)
        {
            if (!add)
            {
                _selection.Clear();
            }
            foreach (int i in things)
            {
                if (s != null && i < s.Things.Count && !s.Things[i].Gone)
                {
                    _selection.Add(i);
                }
            }
        }
        SelectionDone();
    }

    /// <summary>The selection changed: its boxes made again, and <c>SelectionChanged</c> raised on the UI thread.</summary>
    private void SelectionDone()
    {
        _selectionDirty = true;
        var s = Scene;
        if (s != null)
        {
            var things = Selected.Select(n => s.Things[n]).ToList();
            Dispatcher.UIThread.Post(() => SelectionChanged?.Invoke(things));
        }
        Wake();
    }

    /// <summary>Tests (headless, so nothing is drawn): the boxes drawing gives things, in view space.</summary>
    internal void SetBoxes(IEnumerable<(int Index, Vector3 Min, Vector3 Max)> boxes)
    {
        var s = Scene!;
        EnsureSize(s.Things.Count);
        lock (_objLock)
        {
            foreach (var (i, lo, hi) in boxes)
            {
                var t = s.Things[i];
                _kinds[i] = ObjectKinds.Of(NameOf(t.Prefab), t.Piece, t.Tamed);
                _bounds[i] = (lo, hi);
                _boundsAt[i] = t.Position;
                _known[i] = !t.Gone;
            }
        }
    }

    /// <summary>The test driver's pictures: the next frame once the area and its models are in.</summary>
    private volatile GlPicture.Request? _picture;

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

    /// <summary>Whether a thing is drawn (not gone, its kind shown, its model known): only those can be picked.</summary>
    internal bool IsPickable(int i)
    {
        lock (_objLock)
        {
            return i < _known.Length && _known[i] && _shown[(int)_kinds[i]];
        }
    }

    /// <summary>A thing's box (view space) and the position it was made at (world coordinates).</summary>
    internal (Vector3 Min, Vector3 Max, Vector3 At) BoxOf(int i)
    {
        lock (_objLock)
        {
            return (_bounds[i].Min, _bounds[i].Max, _boundsAt[i]);
        }
    }

    /// <summary>A thing's box, in view space.</summary>
    internal (Vector3 Min, Vector3 Max) BoundsOf(int i)
    {
        lock (_objLock)
        {
            return _bounds[i];
        }
    }

    /// <summary>A thing's View kind.</summary>
    internal ObjectKind KindOf(int i)
    {
        lock (_objLock)
        {
            return _kinds[i];
        }
    }

    /// <summary>The overlays' lines to the graphics card, each layer's buffer filled again.</summary>
    private unsafe void UploadOverlays(Overlays.Built o)
    {
        foreach (var (layer, data) in o.Lines)
        {
            // Built again when objects change: the same buffers are filled again.
            if (!_overlayGl.TryGetValue(layer, out var old))
            {
                old = (_own.VertexArray(), 0, _own.Buffer());
                _gl.BindVertexArray(old.Vao);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, old.Vbo);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
            }
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, old.Vbo);
            fixed (float* p = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
            }
            _overlayGl[layer] = (old.Vao, data.Length / 3, old.Vbo);
        }
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// The overlays that are on; each layer's style says whether it is hidden behind what stands in front of it
    /// or shows through (the rings).
    /// </summary>
    private unsafe void DrawOverlays(Matrix4x4 vp)
    {
        if (_lineProg == 0)
        {
            _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        }
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&vp);
        int uColor = _gl.GetUniformLocation(_lineProg, "uColor");
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(false);
        foreach (var (layer, (vao, count, _)) in _overlayGl)
        {
            if (!_overlay[(int)layer] || count == 0)
            {
                continue;
            }
            var (c, hidden) = Overlays.Style(layer);
            if (hidden)
            {
                _gl.Enable(EnableCap.DepthTest);
            }
            else
            {
                // Rings show through what stands on them, like the web editor's.
                _gl.Disable(EnableCap.DepthTest);
            }
            _gl.Uniform4(uColor, c.X, c.Y, c.Z, c.W);
            _gl.BindVertexArray(vao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)count);
        }
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>The line shader (made at first use), and the selection's lines on the graphics card.</summary>
    private uint _lineProg, _lineVao, _lineVbo;
    /// <summary>The selected objects' boxes, orange, seen through what is in front of them.</summary>
    private unsafe void DrawSelection(Matrix4x4 vp)
    {
        if (_selectionDirty)
        {
            _selectionDirty = false;
            var v = new List<float>();
            foreach (int i in Selected)
            {
                var (lo, hi) = _bounds[i];
                Vector3 C(int k) => new((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z);
                foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
                {
                    var p = C(a); var q = C(b);
                    v.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
                }
            }
            _selectionSegs = [.. v];
        }
        // Seen through what is in front of it, like the web editor's selection.
        DrawLines(ref _lineVao, ref _lineVbo, _selectionSegs, vp, new Vector4(1, 0.66f, 0.2f, 1));
    }

    /// <summary>The selection's box lines, made again when it changes.</summary>
    private float[] _selectionSegs = [];

    /// <summary>The Select tool: the object under the pointer, outlined faintly before a click picks it.</summary>
    private int? _hoverObject;
    /// <summary>The hovered object's box lines on the graphics card.</summary>
    private uint _hoverBoxVao, _hoverBoxVbo;

    /// <summary>Sets the object under the pointer (null: none).</summary>
    private void SetHoverObject(int? i)
    {
        if (i != _hoverObject)
        {
            _hoverObject = i;
            Wake();
        }
    }

    /// <summary>The object under the pointer's box, faint, when a click can pick it.</summary>
    private void DrawHoverObject(Matrix4x4 vp)
    {
        // Only what a click can pick: drawn and not deleted (a deleted object stays under the pointer).
        if (!SelectMode || Tool != null || _hoverObject is not int i || i >= _bounds.Length || _selection.Contains(i)
            || !_known[i] || !_shown[(int)_kinds[i]] || Scene is not { } sc || i >= sc.Things.Count || sc.Things[i].Gone)
        {
            return;
        }
        var (lo, hi) = _bounds[i];
        Vector3 C(int k) => new((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z);
        var v = new List<float>();
        foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
        {
            var p = C(a); var q = C(b);
            v.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        }
        DrawLines(ref _hoverBoxVao, ref _hoverBoxVbo, [.. v], vp, new Vector4(1, 0.85f, 0.55f, 0.6f), blend: true, width: 0.8f);
    }

    /// <summary>Slides the view along the ground by a drag of (dx, dy) points (under the camera lock).</summary>
    private void Slide(float dx, float dy)
    {
        float k = _distance * 0.0015f, c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
        _target += new Vector3(-dx * c - dy * sn, 0, dx * sn - dy * c) * k;
        FollowGround();
    }

    /// <summary>
    /// A mouse drag: slides the view (Panning; in Orbit also the left or middle button) or turns it (right
    /// button).
    /// </summary>
    private void Drag(Point p)
    {
        if (_dragFrom is Point from)
        {
            float dx = (float)(p.X - from.X), dy = (float)(p.Y - from.Y);
            lock (_camLock)
            {
                if (Panning)
                {
                    Slide(dx, dy);
                }
                else if (_dragButton == PointerUpdateKind.RightButtonPressed)
                {
                    _yaw -= dx * 0.005f;
                    _pitch = _eye == EyeMode.Orbit ? Math.Clamp(_pitch + dy * 0.005f, 0.05f, 1.55f) : Math.Clamp(_pitch + dy * 0.005f, -1.45f, 1.45f);
                }
                else if (_eye == EyeMode.Orbit && _dragButton is PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.LeftButtonPressed)
                {
                    Slide(dx, dy);
                }
            }
            _dragFrom = p;
        }
        Wake();
    }
}
