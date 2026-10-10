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

// The 3D view: the ground, the water and every object and building piece of a WorldScene, drawn with
// OpenGL (OpenGL 3.3, or OpenGL ES 3.0 where Avalonia gives that, e.g. ANGLE on Windows). Objects are
// drawn with GPU instancing: one draw per model part for all its copies. Models load on worker threads
// and appear as they arrive. Drawn only while something happens (the camera moves, keys, the mouse),
// a few times a second otherwise.
public sealed class GlView : OpenGlControlBase
{
	private GL _gl = null!;
	// Every OpenGL object this view made, deleted with its context (see GlObjects).
	private GlObjects _own = null!;
	private bool _es;
	private WorldScene? _scene;
	private ModelStore? _models;

	internal ModelStore? Models => _models;

	// ---- Camera: orbit around a target point (view space), like the web editor's.
	private readonly object _camLock = new();
	private Vector3 _target;
	private float _distance = 140, _yaw = 0.8f, _pitch = 0.9f;
	// Walk and Fly (F): the camera itself at _eyePos, looking along yaw and pitch (positive: down).
	public enum EyeMode { Orbit, Walk, Fly }
	private EyeMode _eye = EyeMode.Orbit;
	private Vector3 _eyePos;
	public EyeMode Eye { get { lock (_camLock) { return _eye; } } }
	public event Action<EyeMode>? EyeChanged;
	private const float EyeHeight = 1.8f;
	private Vector3 Forward => new(-MathF.Sin(_yaw) * MathF.Cos(_pitch), -MathF.Sin(_pitch), -MathF.Cos(_yaw) * MathF.Cos(_pitch));

	// F: the usual view, then walking at a player's eye height, then flying, then back.
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

	// Above this the game takes a place for indoors (Character.InInterior): dungeons are built there,
	// 5000 m over their entrances. The camera does not keep to the ground up there.
	internal const float InteriorHeight = 3000f;

	// Walking: the eyes 1.8 m over the ground (or the water); flying: never below that. Not in a
	// dungeon (the ground is far below).
	private void KeepOverGround(bool walk)
	{
		var s = _scene;
		if (s == null || _eyePos.Y > InteriorHeight)
		{
			return;
		}
		float floor = Math.Max(Picking.HeightAt(s, _eyePos.X, _eyePos.Z), s.Water) + EyeHeight;
		_eyePos.Y = walk ? floor : Math.Max(_eyePos.Y, floor);
	}
	private readonly HashSet<Key> _keys = new();
	private Point? _dragFrom;
	private PointerUpdateKind _dragButton;
	private long _wokeAt, _lastFrame, _idleAt;
	private Matrix4x4 _lastView;

	// ---- Frame rate: shown by the window, and sampled every 0.2 s for the log when it is on.
	public sealed record Stats(int Fps, double WorkMs, int Objects, int Batches, int Instances, int PendingModels);
	public event Action<Stats>? StatsChanged;
	public event Action<string>? Status;
	private readonly Stopwatch _clock = Stopwatch.StartNew();
	private readonly List<(long At, double Work)> _frames = new();
	private long _statsAt;
	public PerfLog? Perf { get; set; }

	// ---- View switches: which kinds of objects are drawn, and the water.
	private readonly bool[] _shown = ObjectKinds.All.Select(ObjectKinds.ShownAtFirst).ToArray();
	public bool IsShown(ObjectKind k) => _shown[(int)k];
	public void SetShown(ObjectKind k, bool on)
	{
		_shown[(int)k] = on;
		Wake();
	}
	private volatile bool _showWater = true;
	public bool ShowWater { get => _showWater; set { _showWater = value; Wake(); } }

	// View, Look: the game's look (its terrain shader, sky and sea, the models) or plain colours and
	// boxes, as the web editor's switch; see-through buildings (players' pieces drawn faint, to see
	// what is inside or behind them); and how many pixels the 3D view draws (Sharp: the screen's;
	// Balanced: one per point; Fast: three quarters of that), fewer pixels for more frames.
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
	private volatile bool _gameLookOn = true;
	// Whether the game's look is there to switch on (copied from the game, set up for this area).
	public bool GameLookLoaded => _look != null;
	public bool SeeThroughBuildings { get => _seeThrough; set { _seeThrough = value; Wake(); } }
	private volatile bool _seeThrough;
	public enum Resolution { Sharp, Balanced, Fast }
	public Resolution Resolution3D { get => _resolution; set { _resolution = value; Wake(); } }
	private volatile Resolution _resolution = Resolution.Sharp;

	// Pixels drawn per screen point for a resolution, on a screen of this scaling.
	internal static double PixelScale(Resolution r, double screen) => r switch
	{
		Resolution.Balanced => Math.Min(screen, 1),
		Resolution.Fast => Math.Min(screen, 1) * 0.75,
		_ => screen,
	};

	// The size in pixels the 3D view draws at now.
	internal (int W, int H) RenderSize()
	{
		double scale = PixelScale(_resolution, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
		return (Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)));
	}

	// Alt + wheel: the tools that turn take it (the window decides: true when taken); otherwise it zooms.
	// The direction is that of the , and . keys: towards you (down) as . and away (up) as ,.
	internal Func<Key, bool, bool>? AltWheel { get; set; }
	// Ctrl + wheel (up: 1, down: -1; with Shift): what the tool does with it (true), else the view zooms.
	internal Func<int, bool, bool>? CtrlWheel { get; set; }
	private double _wheelSum;
	// How many objects of each kind the area has (once the models' names are known).
	public event Action<Dictionary<ObjectKind, int>>? KindCounts;

	// ---- Overlays (zone borders at first) and the ground's measuring colours (game look only).
	private readonly bool[] _overlay = Overlays.All.Select(l => l == Overlays.Layer.Borders).ToArray();
	public bool IsOverlayShown(Overlays.Layer l) => _overlay[(int)l];
	public void SetOverlay(Overlays.Layer l, bool on)
	{
		_overlay[(int)l] = on;
		if (l == Overlays.Layer.Flatten)
		{
			_overlay[(int)Overlays.Layer.FlattenEdge] = on;
		}
		Wake();
	}
	private volatile bool _slope;
	private float _contour;
	public bool SlopeColours { get => _slope; set { _slope = value; Wake(); } }
	public float ContourStep { get => _contour; set { _contour = value; Wake(); } }
	// The red on ground at the game's ±8 m limit can be hidden (View: Limit marks).
	private volatile bool _limit = true;
	public bool LimitMarks { get => _limit; set { _limit = value; Wake(); } }
	// How many wards, crafting stations, flattened places and locations the overlays show.
	public event Action<Overlays.Built>? OverlaysBuilt;
	private Overlays.Built? _overlays;
	private readonly Dictionary<Overlays.Layer, (uint Vao, int Count, uint Vbo)> _overlayGl = new();

	// For tests: the camera, and the keys held.
	internal (float Yaw, float Pitch, float Distance, Vector3 Target) Camera { get { lock (_camLock) { return (_yaw, _pitch, _distance, _target); } } }
	internal Vector3 EyePosition { get { lock (_camLock) { return _eyePos; } } }
	// For tests: move as if the keys were held for dt seconds.
	internal bool Step(float dt) => MoveWithKeys(dt);
	internal Key[] KeysHeld { get { lock (_keys) { return _keys.ToArray(); } } }

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

	public void Show(WorldScene scene, ModelStore? models)
	{
		_scene = scene;
		Dungeon.Scene = scene;
		Dungeon.Changed -= Wake;
		Dungeon.Changed += Wake;
		_models = models;
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
				Status?.Invoke(_lookFiles == null ? "Game look not copied yet: plain colours (see the start page)." : "Game look loaded.");
			}
			catch (Exception ex)
			{
				Status?.Invoke("Game look could not be read: " + ex.Message);
			}
			Wake();
		});
		Wake();
	}
	private volatile GameLookGl.Files? _lookFiles;
	private GameLookGl? _look;
	private readonly List<GameLookGl> _looksDropped = new();

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

	// Asks for frames at full speed for a second. Avalonia takes frame requests on its own (UI) thread
	// only: from a worker thread (a model or the game look finished loading) the request goes there.
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
	private uint _terrainProg, _objectProg, _waterProg;
	private uint _terrainVao, _terrainIndexCount, _waterVao, _terrainVbo, _terrainExtraVbo;
	private uint _terrainEbo, _terrainBiomeVbo, _waterVbo, _waterEbo;
	private bool _sceneDirty;

	private sealed class Batch
	{
		public ObjectKind Kind;
		public uint Vao, InstanceVbo;
		public int IndexCount, Instances;
		public uint Texture;
		public ModelStore.MaterialData Material = null!;
	}
	private readonly List<Batch> _batches = new();

	// The things of one kind of object (a building piece a player placed apart from the same piece in a
	// ruin): one model, drawn with one batch per model part. Its batches' instances are rebuilt when one
	// of its things changes (deleted, moved, added, or shown elsewhere while being moved).
	private sealed class Group
	{
		public required (int Prefab, bool Piece, bool Tamed) Key { get; init; }
		public required ObjectKind Kind { get; init; }
		public readonly List<int> Things = new();
		// The same, to ask whether one is in (a list's Contains made opening an area with tens of
		// thousands of one piece take seconds: every object asked it).
		public readonly HashSet<int> Members = new();
		public Vector3 RootScale = Vector3.One;
		// The model's box in its own frame (for picking), known once read.
		public (Vector3 Min, Vector3 Max)? Box;
		public readonly List<(Batch Batch, Matrix4x4 Pre)> Batches = new();
		public bool Ready;
		// A model was read (else the batches are the stand-in box).
		public bool HasModel;
	}
	// By prefab, piece or not and tamed or not: tamed creatures are their own View kind.
	private readonly Dictionary<(int, bool, bool), Group> _groups = new();
	private readonly HashSet<Group> _dirtyGroups = new();
	// Things shown somewhere else than where they are, while a move is being made (Select tool).
	private Dictionary<int, WorldScene.Thing> _previews = new();
	private volatile bool _thingsReset, _overlaysDirty;
	// The overlays wait for the ground only (not for objects, which show at once): built again 200 ms
	// after the last time at the most.
	private bool _overlaysGroundOnly;
	private long _overlaysBuiltAt;
	private readonly Dictionary<string, (uint Vbo, uint[] Ebos, int[] Counts)> _meshGl = new();
	// Meshes on the graphics card (read by the loaders on worker threads), and about how much the
	// models and their textures take there. Past the budget, opening another area lets them all go
	// (that area's are read again): they piled up over a long session, gigabytes with Valheim open.
	private readonly ConcurrentDictionary<string, bool> _onGpu = new();
	private long _gpuBytes;
	// VWE_GPU_BUDGET (bytes): another budget, for the tests.
	private static readonly long GpuBudget = long.TryParse(Environment.GetEnvironmentVariable("VWE_GPU_BUDGET"), out long b) ? b : 1L << 30;
	private readonly Dictionary<string, uint> _textures = new();
	// Models read on worker threads, waiting to go to the graphics card (on the drawing thread).
	private sealed record ReadyModel(Group Group, ModelStore.Model? Model, Dictionary<string, ModelStore.MeshData> Meshes,
		Dictionary<string, ModelStore.MaterialData> Materials, Dictionary<string, ModelStore.ImageData?> Images);
	private readonly ConcurrentQueue<ReadyModel> _ready = new();
	private int _pending;
	private uint _boxVbo, _boxEbo;

	protected override void OnOpenGlInit(GlInterface gl)
	{
		_gl = GL.GetApi(name => gl.GetProcAddress(name));
		_own = new GlObjects(_gl);
		_es = GlVersion.Type == GlProfileType.OpenGLES;
		_terrainProg = Program(Shaders.TerrainVs, Shaders.TerrainFs);
		_objectProg = Program(Shaders.ObjectVs, Shaders.ObjectFs);
		_waterProg = Program(Shaders.WaterVs, Shaders.WaterFs);
		Status?.Invoke($"OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}");
	}

	// The view left the window (the map page is shown): Avalonia drops its OpenGL context and makes a
	// new one when it comes back. Its buffers, textures and programs are shared with Avalonia's own
	// context, so they outlive it: all are deleted here (the context is still current), then every name
	// is forgotten and the next frame builds them again in the new one. Keeping the names drew with
	// names that meant nothing, or something else, in the new context.
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
		_markVao = _markVbo = 0;
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

	private uint Program(string vs, string fs) => _own.Program(Link(vs, fs));

	// A program not yet anyone's (the game look keeps its own).
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
	// Position, normal and colour of rows z0..z1 (9 floats a point).
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

	// Mask uv, ocean depth and limit tint of rows z0..z1 (4 floats a point).
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

	// After an edit: the changed rows (and their neighbours, whose normals move) to the graphics card.
	private unsafe void UpdateTerrain(WorldScene s, int z0, int z1)
	{
		z0 = Math.Max(0, z0 - 1);
		z1 = Math.Min(s.H - 1, z1 + 1);
		float[] v = TerrainRows(s, z0, z1), extra = ExtraRows(s, z0, z1);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainVbo);
		fixed (float* p = v)
		{
			_gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(z0 * s.W * 36), (nuint)(v.Length * 4), p);
		}
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainExtraVbo);
		fixed (float* p = extra)
		{
			_gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(z0 * s.W * 16), (nuint)(extra.Length * 4), p);
		}
		_look?.UpdateRows(s, z0, z1);
	}

	// The last area's ground and sea.
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
		float[] q = { -hw, y, -hh, hw, y, -hh, hw, y, hh, -hw, y, hh };
		uint[] qi = { 0, 2, 1, 0, 3, 2 };
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
	private string? NameOf(int prefab) => _models?.NameOf(prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(prefab);

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

	// The group a thing belongs to (made, and its model read, if it is the first of its kind).
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

	// A kind's group without things yet (the Place tool's ghosts), its model read if new.
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

	private uint _ghostVbo;
	// The kinds the paste's ghost drew this frame (their posts are left out).
	private HashSet<int>? _pasteGhosts;
	// The Place tool's preview, or the Paste tool's (the building where it would land), drawn with the
	// models, see-through. Kinds whose model is not there yet keep their posts (DrawPlace, the paste's
	// outline). Returns the prefabs drawn.
	private unsafe HashSet<int> DrawGhosts(WorldScene s, Matrix4x4 vp)
	{
		var drawn = new HashSet<int>();
		List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown;
		if (_mode == ToolMode.Place && Place is { } pl && pl.Shown.Length > 0)
		{
			shown = pl.Shown.Select(o => (TerrainEditor.Save.StableHash.Of(o.Name), o.Position, o.Rotation, o.Scale)).ToList();
		}
		else if (_mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
		{
			shown = Paste.Ghosts(pat, GridHeight, s.X0 * 64f - 32f, s.Z0 * 64f - 32f);
		}
		else
		{
			return drawn;
		}
		_pasteGhosts = _mode == ToolMode.Paste ? drawn : null;
		DrawInstances(s, shown, ghost: true, drawn);
		return drawn;
	}

	// Models at places that are not things of the scene, one draw per model part: see-through (ghost:
	// the Place and Paste previews) or solid (dungeon rooms). Kinds whose model is not read yet, or that
	// have none, are skipped (and left out of `drawn`).
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
			bool piece = TerrainEditor.Terrain.PieceCatalog.Get(prefab)?.Tool != null;
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

	// The rooms of the area's dungeons (not objects of the save: entries in their dungeon's data, see
	// DungeonRooms), drawn like objects; a room shown elsewhere by the Dungeon tool (moving, adding) too.
	private void DrawRooms(WorldScene s)
	{
		var shown = new List<(int, Vector3, Vector3, float)>();
		foreach (var d in DungeonRooms.Of(s))
		{
			foreach (var r in d.Rooms)
			{
				shown.Add((r.Hash, r.Position, TerrainEditor.Editing.Dungeons.ToEuler(r.Rotation), 0f));
			}
		}
		if (shown.Count > 0)
		{
			DrawInstances(s, shown, ghost: false);
		}
	}

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

	// Things appeared, went or came back (EditSession): their groups are drawn again.
	private void OnThingsChanged(IReadOnlyList<int> indices)
	{
		var s = _scene;
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
		SendKindCounts(s);
		Wake();
	}

	private void MarkDirty(Group g)
	{
		lock (_dirtyGroups)
		{
			_dirtyGroups.Add(g);
		}
	}

	// Shows things somewhere else than where they are (a move in progress), or (null) where they are.
	internal void SetPreviews(Dictionary<int, WorldScene.Thing>? previews)
	{
		var s = _scene;
		if (s == null)
		{
			return;
		}
		var old = _previews;
		_previews = previews ?? new();
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

	// Reads a group's model on a worker thread; the drawing thread sends it to the graphics card. Waiting
	// for its turn holds no thread (hundreds of kinds each blocking one starved the thread pool: the
	// next area's loading and the map's search waited seconds behind them).
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
				var model = name != null && _models != null && _gameLookOn ? _models.LoadModel(name) : null;
				var meshes = new Dictionary<string, ModelStore.MeshData>();
				var mats = new Dictionary<string, ModelStore.MaterialData>();
				var images = new Dictionary<string, ModelStore.ImageData?>();
				Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
				if (model != null)
				{
					foreach (var part in model.Parts)
					{
						// A mesh already on the graphics card is not read again: only its box is needed.
						var bounds = _onGpu.ContainsKey(part.Mesh) ? _models!.BoundsOf(part.Mesh) : null;
						if (bounds == null && _models!.LoadMesh(part.Mesh) is { } md)
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
							var mat = _models!.Material(part.Material);
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
								images[mat.Map] = _models.LoadTexture(mat.Map);
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
	// Static: one 3D view lives as long as the app (and an instance field would make the view disposable).
	private static readonly SemaphoreSlim LoadGate = new(Math.Max(2, Environment.ProcessorCount / 2));
	private readonly object _objLock = new();

	// A group's instances (and its things' boxes) from where its things are now.
	private unsafe void Rebuild(WorldScene s, Group g)
	{
		var list = new List<Matrix4x4>();
		var tints = new List<Vector3>();
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
					if (g.Box is { } box)
					{
						_bounds[i] = Picking.Transform(box.Min, box.Max, m);
						_boundsAt[i] = t.Position;
					}
					_known[i] = !t.Gone && g.Box != null;
					if (!t.Gone)
					{
						list.Add(m);
						tints.Add(support != null && support.TryGetValue(i, out float sv) ? SupportTint(sv) : Vector3.Zero);
					}
				}
			}
		}
		foreach (var (b, pre) in g.Batches)
		{
			var data = list.Select((m, k) =>
			{
				var w = pre * m;
				(w.M14, w.M24, w.M34) = (tints[k].X, tints[k].Y, tints[k].Z);
				return w;
			}).ToArray();
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
			fixed (Matrix4x4* ptr = data)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), ptr, BufferUsageARB.DynamicDraw);
			}
			b.Instances = data.Length;
		}
	}

	// Every model and texture off the graphics card (after DropObjects: no batch uses them).
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

	// Everything read again (after a save): the old batches go.
	private void DropObjects()
	{
		foreach (var b in _batches)
		{
			_own.DeleteVertexArray(b.Vao);
			_own.DeleteBuffer(b.InstanceVbo);
		}
		_batches.Clear();
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
	private readonly HashSet<string> _claimed = new();

	// Where an object goes, in view space: Unity's rotation (Euler degrees, applied z, x, then y)
	// mirrored in z, like the web editor's objects.js.
	private static Matrix4x4 Placement(WorldScene s, WorldScene.Thing t, Vector3 rootScale)
	{
		const float D = MathF.PI / 180f;
		var q = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y * D, t.Rotation.X * D, t.Rotation.Z * D);
		q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
		var scale = t.Scale > 0 ? new Vector3(t.Scale) : rootScale;
		var pos = new Vector3(t.Position.X - s.Cx, t.Position.Y, -(t.Position.Z - s.Cz));
		return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(pos);
	}

	// A loaded model's textures, then its batches. A model read for an area (or a look) left since
	// keeps only its textures: its group is gone, and its things' indices are another scene's.
	private unsafe void Upload(ReadyModel r)
	{
		UploadTextures(r);
		if (Current(r.Group))
		{
			UploadModel(r);
		}
	}

	// The group is still the view's (not one of an area left since).
	private bool Current(Group g)
	{
		lock (_groups)
		{
			return _groups.TryGetValue(g.Key, out var now) && now == g;
		}
	}

	// A loaded model's textures. Also for a model dropped before it was drawn (a new area): its loader
	// claimed them, so no other loader reads them.
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

	private unsafe void UploadModel(ReadyModel r)
	{
		var g = r.Group;
		if (r.Model == null)
		{
			// No model copied for this kind: a box stands in, as in the web editor.
			AddBatch(g, BoxMesh(), 0, new ModelStore.MaterialData(new Vector4(0.35f, 0.3f, 0.25f, 1), null, 0, false, new Vector4(1, 1, 0, 0)),
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
		if (_scene is { } s)
		{
			Rebuild(s, g);
		}
		Interlocked.Decrement(ref _pending);
	}

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
		var ebos = new uint[md.Submeshes.Length];
		var counts = new int[md.Submeshes.Length];
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
		_models?.Forget(id);
		return _meshGl[id] = (vbo, ebos, counts);
	}

	private (uint, uint[], int[]) BoxMesh()
	{
		const string id = "\0box";
		if (_meshGl.TryGetValue(id, out var m))
		{
			return m;
		}
		var verts = new List<float>();
		var idx = new List<uint>();
		// A unit cube centred on 0, flat normals per face.
		Vector3[] n = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
		foreach (var f in n)
		{
			var u = MathF.Abs(f.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
			var v = Vector3.Cross(f, u);
			uint b = (uint)(verts.Count / 8);
			foreach (var (a, c) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
			{
				var p = (f + u * a + v * c) * 0.5f;
				verts.AddRange(new[] { p.X, p.Y, p.Z, f.X, f.Y, f.Z, 0f, 0f });
			}
			idx.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
		}
		return MeshGl(id, new ModelStore.MeshData(verts.ToArray(), new[] { idx.ToArray() }));
	}

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
	protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
	{
		long now = _clock.ElapsedMilliseconds;
		var start = Stopwatch.GetTimestamp();
		var s = _scene;
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
				dirty = _dirtyGroups.Where(g => g.Ready).ToArray();
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
			_hover = (_tool != null || _mode is ToolMode.Shape or ToolMode.Mountain or ToolMode.Path || _mode == ToolMode.Place && Place?.Tool.Mode == PlaceTool.Modes.Brush) && _pointer is Point at ? GroundAt(s, vp, at, _surfaceSize) : null;
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
				look.UseTerrain(vp, eye, time, slope: _slope, contour: _contour, limit: _limit);
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
			// Opaque first; see-through buildings after, blended over everything and not hiding
			// what is behind them.
			foreach (var pass in seeThrough ? new[] { false, true } : new[] { false })
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
					_gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)b.Instances);
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
			_ghostsDrawn = DrawGhosts(s, vp);
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
			// Over everything, the water too.
			if (ShowNewMarkers)
			{
				DrawNewMarkers(s, vp);
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

	// The smaller picture for a lower 3D resolution (made again when the size changes).
	private uint _lowFbo, _lowColor, _lowDepth;
	private int _lowW, _lowH;

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

	private void Measure(long now, double work, bool camMoved, int pw, int ph)
	{
		_frames.Add((now, work));
		while (_frames.Count > 0 && now - _frames[0].At > 1000)
		{
			_frames.RemoveAt(0);
		}
		Perf?.Frame(now, work, camMoved, busy: now - _wokeAt < 1000, $"view {pw}×{ph} px · {_gl.GetStringS(StringName.Renderer)} · {_scene?.Name} · {_scene?.Things.Count} objects · area {_scene?.Size}×{_scene?.Size} zones");
		if (now - _statsAt >= 500)
		{
			_statsAt = now;
			var st = new Stats(_frames.Count, _frames.Average(f => f.Work), _scene?.Things.Count ?? 0, _batches.Count, _batches.Sum(b => b.Instances), _pending);
			Dispatcher.UIThread.Post(() => StatsChanged?.Invoke(st));
		}
	}

	// --shot and --bench (Options): once every model is loaded, save a picture, then turn the camera
	// for the given time and print the frame rates, then close the app.
	private long _loadedAt = -1;
	private WorldScene? _loadedScene;
	// The test driver: its pictures (once the area and its models are in) and its frame-rate measure
	// (the camera turns on its own for a while, then the rates are given back).
	private unsafe void Automate(long now, int pw, int ph)
	{
		if (_picture == null && _bench == null)
		{
			return;
		}
		if (_loadedAt < 0)
		{
			if (_scene != null && _terrainVao != 0 && _pending == 0 && _ready.IsEmpty)
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
				bench.Done.SetResult($"{bench.Frames.Count / ((now - bench.From) / 1000.0):0} fps over {(now - bench.From) / 1000.0:0.0} s, work {_frames.Average(f => f.Work):0.0} ms, gaps median {gaps[gaps.Count / 2]} ms, 95% {gaps[gaps.Count * 95 / 100]} ms, max {gaps[^1]} ms, view {pw}×{ph} px");
			}
			RequestNextFrameRendering();
		}
	}

	private sealed class Bench
	{
		public required double Seconds { get; init; }
		public long From { get; set; } = -1;
		public List<long> Frames { get; } = new();
		public TaskCompletionSource<string> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private volatile Bench? _bench;

	// The camera turns on its own for this many seconds; the frame rates come back.
	internal Task<string> Benchmark(double seconds)
	{
		var b = new Bench { Seconds = seconds };
		_bench = b;
		Wake();
		return b.Done.Task;
	}

	// OpenGL's perspective (depth from -1 to 1), as a System.Numerics (row vector) matrix.
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
	private bool MoveWithKeys(float dt)
	{
		Vector3 d = Vector3.Zero;
		bool fast;
		float up = 0;
		lock (_keys)
		{
			if (_keys.Contains(Key.W) || _keys.Contains(Key.Up)) d.Z -= 1;
			if (_keys.Contains(Key.S) || _keys.Contains(Key.Down)) d.Z += 1;
			if (_keys.Contains(Key.A) || _keys.Contains(Key.Left)) d.X -= 1;
			if (_keys.Contains(Key.D) || _keys.Contains(Key.Right)) d.X += 1;
			if (_keys.Contains(Key.Space)) up += 1;
			if (_keys.Contains(Key.C)) up -= 1;
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

	// The target point stays on the ground (or the water) under it; in a dungeon, at its height.
	private void FollowGround()
	{
		var s = _scene;
		if (s == null || _target.Y > InteriorHeight)
		{
			return;
		}
		int gx = Math.Clamp((int)MathF.Round(_target.X + (s.W - 1) / 2f), 0, s.W - 1), gz = Math.Clamp((int)MathF.Round(-_target.Z + (s.H - 1) / 2f), 0, s.H - 1);
		_target.Y = Math.Max(s.Heights[gz * s.W + gx], s.Water);
	}

	// Mouse and keys. The OpenGL picture is not something Avalonia can hit-test (pointer events go
	// through it), so a transparent surface laid over the view takes them and hands them here; keys
	// come from the window, whatever has the focus.
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
				_panDrag = true;
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Place && Place != null)
			{
				_dragFrom = null;
				_placeDown = true;
				var m = e.KeyModifiers;
				Place.Down(p.Position, _surfaceSize, m.HasFlag(KeyModifiers.Shift), m.HasFlag(KeyModifiers.Control), m.HasFlag(KeyModifiers.Alt), e.ClickCount);
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Paste)
			{
				_dragFrom = null;
				if (GridAt(p.Position, _surfaceSize) is { } at)
				{
					PasteClicked?.Invoke(at);
				}
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Area)
			{
				_dragFrom = null;
				_areaDown = true;
				Area.Down(GridAt(p.Position, _surfaceSize), e.ClickCount);
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Path)
			{
				_dragFrom = null;
				_pathDown = true;
				var mods = e.KeyModifiers;
				Path.Down(GridAt(p.Position, _surfaceSize), p.Position, g => ScreenOfGrid(g), mods.HasFlag(KeyModifiers.Control), mods.HasFlag(KeyModifiers.Alt),
					mods.HasFlag(KeyModifiers.Shift), GridHeight);
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode is ToolMode.Shape or ToolMode.Mountain)
			{
				_dragFrom = null;
				if (_scene is { } sc && GroundAt(sc, _lastViewProj, p.Position, _surfaceSize) is { } g)
				{
					ShapeClicked?.Invoke(g.X, g.Z);
				}
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Dungeon)
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
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Measure)
			{
				_dragFrom = null;
				if (WorldAt(p.Position, _surfaceSize) is { } w)
				{
					Tape.Down(w);
				}
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _tool == null && _selectMode)
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
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _tool is BrushTool && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
			{
				_dragFrom = null;
				if (WorldAt(p.Position, _surfaceSize) is { } w)
				{
					BrushAltClick?.Invoke(w.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
				}
				return;
			}
			// Stamp once: a click of Raise or Lower puts the whole stamp in.
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _tool is BrushTool.Raise or BrushTool.Lower && _scene is { Session: { Brush: { StampOnce: true, Shape: BrushShape.Stamp } } } ss)
			{
				_dragFrom = null;
				if (GroundAt(ss, _lastViewProj, p.Position, _surfaceSize) is { } at)
				{
					StampClicked?.Invoke(at.X, at.Z);
				}
				Wake();
				return;
			}
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _tool is BrushTool tool && _scene is { Session: { } session } s)
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
				string message = _scene?.Session?.EndStroke() ?? "";
				StrokeEnded?.Invoke(message);
			}
			else if (_mode == ToolMode.View && !_panDrag && _dragButton == PointerUpdateKind.LeftButtonPressed && _pressAt is Point from && Point.Distance(from, at) < 5)
			{
				Pick(at, surface.Bounds.Size, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control),
					e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Control));
			}
			_pressAt = null;
			_dragFrom = null;
			_panDrag = false;
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
			if (_mode == ToolMode.Paste)
			{
				Paste.At = GridAt(_pointer.Value, _surfaceSize);
			}
			if (_mode == ToolMode.Place)
			{
				Place?.Moved(_pointer.Value, _surfaceSize);
			}
			if (_selectDown)
			{
				SelectTool.Moved(_pointer.Value, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Control));
			}
			else if (_selectMode && _tool == null)
			{
				SelectTool.Hover(_pointer.Value, _surfaceSize);
				// The object a click would pick (none over a move handle).
				SetHoverObject(SelectTool.OverHandle ? null : ObjectAt(_pointer.Value, _surfaceSize));
			}
			else if (_mode == ToolMode.Dungeon && WorldRay(_pointer.Value, _surfaceSize) is { } ray)
			{
				Dungeon.Hover(ray.O, ray.D);
			}
			else if (_mode == ToolMode.Measure && WorldAt(_pointer.Value, _surfaceSize) is { } w)
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
		window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
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
		window.AddHandler(InputElement.KeyUpEvent, (_, e) =>
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

	private Point? _pressAt;
	// The drag slides the view (Space + left, Shift + right).
	private bool _panDrag;
	internal bool Panning => _panDrag;

	internal WorldScene? Scene => _scene;
	// The Select tool takes the left button (see SelectTool); otherwise it slides the view and clicks pick.
	public SelectTool SelectTool { get; }
	private ToolMode _mode;
	public ToolMode Mode { get => _mode; set { if (value != ToolMode.Select) SelectTool.Commit(); _mode = value; Wake(); } }
	public bool SelectMode => _mode == ToolMode.Select;
	private bool _selectMode => _mode == ToolMode.Select;
	public MeasureTool Tape { get; } = new();

	// ---- The Dungeon tool: its state, and what it shows (the free openings, the room selected or
	// pointed at, and the room a click would add, see-through).
	public DungeonTool Dungeon { get; } = new();

	private uint _dungeonVao, _dungeonVbo, _dungeonPickVao, _dungeonPickVbo, _dungeonAddVao, _dungeonAddVbo, _dungeonDoorVao, _dungeonDoorVbo, _dungeonJointVao, _dungeonJointVbo;

	private void DrawDungeon(WorldScene s, Matrix4x4 vp)
	{
		if (_mode != ToolMode.Dungeon || Dungeon.Dungeon is not { } d)
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
			Vector3[] c = { e.Position - side, e.Position + side, e.Position + side + up * 3, e.Position - side + up * 3 };
			var list = Dungeon.HoverEnd == i ? hovered : ends;
			for (int k = 0; k < 4; k++)
			{
				var a = V(c[k]);
				var b = V(c[(k + 1) % 4]);
				list.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z });
			}
			var m = V(e.Position + up * 1.5f);
			var n = V(e.Position + up * 1.5f + Vector3.Transform(Vector3.UnitZ, e.Rotation) * -1.5f);
			list.AddRange(new[] { m.X, m.Y, m.Z, n.X, n.Y, n.Z });
		}
		if (ends.Count > 0)
		{
			DrawLines(ref _dungeonVao, ref _dungeonVbo, ends.ToArray(), vp, new Vector4(0.35f, 0.95f, 0.45f, 0.95f), blend: true, width: 2f);
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
			Vector3[] c = { j.Position + up * 0.9f - side, j.Position + up * 0.9f + side, j.Position + up * 2.1f + side, j.Position + up * 2.1f - side };
			var list = Dungeon.HoverJoint == i ? hovered : Dungeon.DoorsAt(j).Count > 0 ? withDoor : noDoor;
			for (int k = 0; k < 4; k++)
			{
				var a = V(c[k]);
				var b = V(c[(k + 1) % 4]);
				list.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z });
			}
		}
		if (withDoor.Count > 0)
		{
			DrawLines(ref _dungeonDoorVao, ref _dungeonDoorVbo, withDoor.ToArray(), vp, new Vector4(0.4f, 0.7f, 1f, 0.95f), blend: true, width: 2f);
		}
		if (noDoor.Count > 0)
		{
			DrawLines(ref _dungeonJointVao, ref _dungeonJointVbo, noDoor.ToArray(), vp, new Vector4(1f, 0.6f, 0.2f, 0.9f), blend: true, width: 1.5f);
		}
		// The room selected (white) or pointed at (faint), as its box.
		var boxes = new List<float>();
		foreach (int i in new[] { Dungeon.Selected, Dungeon.HoverRoom }.OfType<int>().Distinct().Where(i => i < d.Rooms.Count))
		{
			BoxLines(boxes, Dungeons.Box(d.Rooms[i]), V);
		}
		if (boxes.Count > 0)
		{
			DrawLines(ref _dungeonPickVao, ref _dungeonPickVbo, boxes.ToArray(), vp, new Vector4(1, 0.9f, 0.6f, 0.9f), blend: true, width: 1.5f);
		}
		// The room a click adds: its model see-through, its box green, or red when it would not fit.
		if (Dungeon.Preview is { } p)
		{
			DrawInstances(s, new[] { (p.Hash, p.Position, Dungeons.ToEuler(p.Rotation), 0f) }, ghost: true);
			BoxLines(hovered, Dungeons.Box(p), V);
		}
		if (hovered.Count > 0)
		{
			bool ok = Dungeon.Preview is not { } pp || Dungeon.Problem(pp) == null;
			DrawLines(ref _dungeonAddVao, ref _dungeonAddVbo, hovered.ToArray(), vp, ok ? new Vector4(1, 0.95f, 0.3f, 1) : new Vector4(1, 0.3f, 0.25f, 1), blend: true, width: 2.5f);
		}
	}

	private static void BoxLines(List<float> into, (Vector3 C, Vector3 H, Quaternion R) box, Func<Vector3, Vector3> view)
	{
		Vector3 h = new(Math.Max(box.H.X, 0.25f), Math.Max(box.H.Y, 0.25f), Math.Max(box.H.Z, 0.25f));
		Vector3 Corner(int k) => view(box.C + Vector3.Transform(new Vector3((k & 1) == 0 ? -h.X : h.X, (k & 2) == 0 ? -h.Y : h.Y, (k & 4) == 0 ? -h.Z : h.Z), box.R));
		foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
		{
			var p = Corner(a);
			var q = Corner(b);
			into.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
		}
	}
	// Shape tool: a click on the ground (grid point), and the radius its outline shows.
	public event Action<float, float>? ShapeClicked;
	public float ShapeRadius { get; set; } = 16;

	// The Mountain tool's shape (metres added east and north of the middle), drawn under the pointer as
	// a mesh on the ground before clicking; null: none.
	private Func<float, float, float>? _mountainPreview;
	internal Func<float, float, float>? MountainPreview
	{
		get => _mountainPreview;
		set
		{
			_mountainPreview = value;
			_previewKey = null;
			RequestNextFrameRendering();
		}
	}
	public PathTool Path { get; } = new();
	// Stamp once: a click on the ground (grid point).
	public event Action<float, float>? StampClicked;
	// Alt + click with a brush: the ground's height there (shift: Alt + Shift).
	public event Action<float, bool>? BrushAltClick;
	public AreaTool Area { get; } = new();
	// The Place tool's mouse (set by the window).
	public PlaceInput? Place { get; set; }
	private bool _placeDown;
	public PasteTool Paste { get; } = new();
	// Paste tool: a click on the ground (grid point).
	public event Action<Vector2>? PasteClicked;
	private bool _areaDown;
	private bool _pathDown;

	// Where a grid point (on the ground, lifted a little) is on the view, or null behind the camera.
	internal Point? ScreenOfGrid(Vector2 g, float lift = 0.6f)
	{
		var s = _scene;
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

	// The grid point of the ground under a point of the view (null: none).
	internal Vector2? GridAt(Point at, Size size) => _scene is { } s && GroundAt(s, _lastViewProj, at, size) is { } g ? new Vector2(g.X, g.Z) : null;

	internal float GridHeight(Vector2 g)
	{
		var s = _scene;
		return s == null ? 0 : Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Y - (s.H - 1) / 2f));
	}
	private bool _selectDown;

	// The world point (x east, height, z north) of the ground under a point of the view, or null.
	internal Vector3? WorldAt(Point at, Size size)
	{
		var s = _scene;
		if (s == null || GroundAt(s, _lastViewProj, at, size) is not { } g)
		{
			return null;
		}
		float wx = s.X0 * 64f - 32f + g.X, wz = s.Z0 * 64f - 32f + g.Z;
		return new Vector3(wx, Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Z - (s.H - 1) / 2f)), wz);
	}

	private Vector3 _lastEye;
	// For tests (nothing is drawn headless): the camera the mouse is read with.
	// The camera turned onto a world point, close (the map's search: the object found).
	// The orbit camera on world point (x, z) at the ground, turned yaw° around it, looking down pitch°
	// (the documentation's pictures, through the driver).
	// height: around that height instead (inside a dungeon).
	internal void Orbit(float x, float z, float yawDegrees, float pitchDegrees, float distance, float? height = null)
	{
		if (_scene is not { } s)
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

	internal void Focus(Vector3 world, float distance = 35)
	{
		if (_scene is not { } s)
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

	// The camera with its target in world metres: kept when another area opens (null: no area).
	internal (float Yaw, float Pitch, float Distance, Vector3 World)? WorldCamera
	{
		get
		{
			if (_scene is not { } s)
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
			if (_scene is not { } s || value is not var (yaw, pitch, distance, world))
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

	// Live: the players online, in world metres (blue posts with a beam up; their names are labels
	// the window places over the view).
	internal IReadOnlyList<(string Name, Vector3 World)> Players
	{
		get => _players;
		set
		{
			_players = value;
			Wake();
		}
	}
	private IReadOnlyList<(string Name, Vector3 World)> _players = Array.Empty<(string, Vector3)>();
	private uint _playersVao, _playersVbo;

	// A player's post: a body 1.9 m tall (rings and sides) and a 40 m beam, in view space.
	internal static float[] PlayerPosts(WorldScene s, IEnumerable<(string Name, Vector3 World)> players)
	{
		var data = new List<float>();
		void Seg(Vector3 p, Vector3 q) => data.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
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
		return data.ToArray();
	}

	private void DrawPlayers(WorldScene s, Matrix4x4 vp)
	{
		if (_players.Count == 0)
		{
			return;
		}
		DrawLines(ref _playersVao, ref _playersVbo, PlayerPosts(s, _players), vp, new Vector4(0.31f, 0.76f, 1, 1));
	}

	internal void SetCamera(Vector3 eye, Vector3 target, float aspect)
	{
		_lastEye = eye;
		_lastViewProj = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * Perspective(60 * MathF.PI / 180, aspect, 0.5f, 6000);
	}
	internal Matrix4x4 ViewProj => _lastViewProj;

	// The ray from the camera through a point of the view (view space).
	internal (Vector3 O, Vector3 D) RayAt(Point at, Size size) =>
		Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));

	// Where the Select tool's handles go: the middle of the selection (following a move in progress), in
	// view space, and their scale (they keep their size on screen). Null when nothing is selected.
	internal (Vector3 Centre, float Scale)? GizmoAt()
	{
		var s = _scene;
		if (s == null || !_selectMode || _tool != null)
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

	private readonly Dictionary<Gizmo.Handle, (uint Vao, int Count)> _gizmoGl = new();
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
				var mesh = Gizmo.Mesh(h);
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

	private volatile bool _lassoDirty;
	internal void LassoChanged()
	{
		_lassoDirty = true;
		Wake();
	}

	// ---- Tools: a sculpt or paint brush (null: looking around and picking objects).
	private BrushTool? _tool;
	public BrushTool? Tool { get => _tool; set { _tool = value; Wake(); } }
	// A stroke finished, with what to say about it ("" when nothing).
	public event Action<string>? StrokeEnded;
	private Point? _pointer;
	private Size _surfaceSize;
	private volatile bool _brushDown;
	private (float X, float Z)? _hover;
	// For tests: the brush's grid point under the mouse, and whether it is painting.
	internal (float X, float Z)? Hover => _hover;
	internal bool BrushDown => _brushDown;

	// The grid point (fractional) of the ground under a point of the view, or null.
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

	// The ground's height at a world point.
	internal float GroundHeight(float wx, float wz)
	{
		var s = _scene;
		return s == null ? 0 : Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
	}

	// Lines (pairs of view-space points) drawn over everything in one colour, refilled every call.
	// Half a tool line's width per metre from the eye (set each frame from the view's size).
	private float _linePx = 0.0015f;

	// Line segments (pairs of points) as thin strips facing the eye, `width` times a tool line's
	// width: they stay whole at any resolution and angle, where 1-pixel lines broke into dashes.
	private float[] Thicken(float[] seg, float width)
	{
		var o = new float[seg.Length / 6 * 18];
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
			Vector3 side = Vector3.Cross(d, (a + b) / 2 - _lastEye);
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

	// width: for line segments, times a tool line's width (0: 1-pixel lines).
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

	// Whether a new object (id < 0) is in the game already (live: applied); such objects lose their marker.
	public Func<int, bool> InGame { get; set; } = _ => false;

	// The green marks on new objects can be hidden (View: Unsaved marks).
	public bool ShowNewMarkers { get; set; } = true;

	// The new objects not saved (or applied) yet, as the web editor marked them: a green dot of the same
	// size on screen just above each, drawn over everything, for the kinds shown.
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

	// The support check (the Workshop): each building piece tinted as the game's build mode shows its
	// support (SupportTint). Per thing index: -1 full, 0..1, or -2 breaks. Null: off.
	private volatile Dictionary<int, float>? _support;

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

	internal Dictionary<int, float>? Support => _support;

	// The game's colour for a support value (WearNTear.Highlight): light blue at full support (on the
	// ground), else red to green as it gets stronger, more saturated and brighter the weaker; one that
	// breaks as the weakest (red).
	internal static Vector3 SupportTint(float v)
	{
		if (v > -1.5f && v < 0)
		{
			return new Vector3(0.6f, 0.8f, 1f);
		}
		float t = Math.Clamp(v < 0 ? 0 : v, 0, 1);
		// Lerp(red, green, t) has hue 0..1/3 with full saturation and value: its HSV, then the game's S and V.
		float h = new Vector3(1 - t, t, 0) is var c && c.X >= c.Y ? c.Y / c.X / 6f : (2 - c.X / c.Y) / 6f;
		return HsvToRgb(h, 1 - 0.5f * t, 1.2f - 0.3f * t);
	}

	private static Vector3 HsvToRgb(float h, float s, float v)
	{
		float r = Math.Clamp(MathF.Abs(h * 6 - 3) - 1, 0, 1), g = Math.Clamp(2 - MathF.Abs(h * 6 - 2), 0, 1), b = Math.Clamp(2 - MathF.Abs(h * 6 - 4), 0, 1);
		return new Vector3((1 - s + s * r) * v, (1 - s + s * g) * v, (1 - s + s * b) * v);
	}

	private uint _markVao, _markVbo;
	private void DrawNewMarkers(WorldScene s, Matrix4x4 vp)
	{
		var marks = NewMarkers(s);
		if (marks.Count == 0)
		{
			return;
		}
		var tri = new List<float>(marks.Count * 8 * 9);
		foreach (var c in marks)
		{
			// About 9 pixels across at any distance: a little disc facing the camera.
			var f = Vector3.Normalize(c - _lastEye);
			var right = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY) is var x && x.LengthSquared() > 1e-6f ? x : Vector3.UnitX);
			var up = Vector3.Cross(right, f);
			float r = Vector3.Distance(_lastEye, c) * 0.0058f;
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
		DrawLines(ref _markVao, ref _markVbo, tri.ToArray(), vp, new Vector4(0x5d / 255f, 1, 0x8a / 255f, 1), PrimitiveType.Triangles);
	}

	private uint _pathVao, _pathVbo, _pathEdgeVao, _pathEdgeVbo, _pathDotVao, _pathDotVbo;
	private uint _placeRingVao, _placeRingVbo;
	private uint _pathBandVao, _pathBandVbo, _pathSoftVao, _pathSoftVbo, _pathPreviewVao, _pathPreviewVbo, _pathPreviewSoftVao, _pathPreviewSoftVbo;
	// The Path tool: the ground it covers (a light band), its width's edges and the soft edge's outer
	// line, the centre line (red) and its points; under the cursor, the width (and the soft edge) the
	// next point brings, with the stretch from the last point to it. Lines are flat ribbons on the
	// ground a few pixels wide, so they stay whole at any resolution and angle (1-pixel lines broke
	// into dashes once the view was scaled up).
	private void DrawPath(WorldScene s, Matrix4x4 vp)
	{
		if (_mode != ToolMode.Path)
		{
			return;
		}
		List<(Vector2 P, int Seg)> curve;
		List<Vector2> pts;
		lock (Path.Points)
		{
			curve = Path.Curve();
			pts = Path.Points.ToList();
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
					to.AddRange(new[] { v.X, v.Y, v.Z });
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
			return Enumerable.Range(0, n + 1).Select(i => c + new Vector2(MathF.Cos(i * MathF.Tau / n), MathF.Sin(i * MathF.Tau / n)) * r).ToList();
		}
		float half = Path.Width / 2, outer = half + MathF.Max(0, Path.Soft);
		float Thick = _linePx * 1.6f, Thin = _linePx * 0.9f;
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
						band.AddRange(new[] { v.X, v.Y, v.Z });
					}
				}
			}
			Ribbon(edges, Offset(d, half), Thick, 0.35f);
			Ribbon(edges, Offset(d, -half), Thick, 0.35f);
			if (Path.Soft > 0.01f)
			{
				Ribbon(soft, Offset(d, outer), Thin, 0.3f);
				Ribbon(soft, Offset(d, -outer), Thin, 0.3f);
			}
			Ribbon(line, c, Thick, 0.45f);
		}
		Stretch(curve.Select(c => c.P).ToList());
		// The cursor: the width and soft edge a point there brings, and the stretch from the last point.
		var preview = new List<float>();
		var previewSoft = new List<float>();
		if (_hover is { } h && !Path.Drawing)
		{
			var at = new Vector2(h.X, h.Z);
			Ribbon(preview, Circle(at, half), Thick, 0.35f);
			if (Path.Soft > 0.01f)
			{
				Ribbon(previewSoft, Circle(at, outer), Thin, 0.3f);
			}
			if (pts.Count > 0 && Vector2.Distance(pts[^1], at) > 0.5f)
			{
				var next = new List<Vector2> { pts[^1], at };
				Ribbon(preview, Offset(Dense(next), half), Thin, 0.35f);
				Ribbon(preview, Offset(Dense(next), -half), Thin, 0.35f);
				Ribbon(preview, next, Thin, 0.45f);
			}
		}
		// Each point: a small square that keeps its size on screen.
		var dots = new List<float>();
		void Seg(List<float> to, Vector3 a, Vector3 b) => to.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z });
		foreach (var p in pts)
		{
			var c = V(p, 0.6f);
			float r = Vector3.Distance(_lastEye, c) * 0.008f;
			Vector3[] k = { c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r) };
			for (int j = 0; j < 4; j++)
			{
				Seg(dots, k[j], k[(j + 1) % 4]);
			}
			Seg(dots, c - new Vector3(0, r, 0), c + new Vector3(0, r, 0));
		}
		DrawLines(ref _pathBandVao, ref _pathBandVbo, band.ToArray(), vp, new Vector4(1, 0.45f, 0.35f, 0.16f), PrimitiveType.Triangles, blend: true);
		DrawLines(ref _pathSoftVao, ref _pathSoftVbo, soft.ToArray(), vp, new Vector4(1, 0.8f, 0.7f, 0.55f), PrimitiveType.Triangles, blend: true);
		DrawLines(ref _pathEdgeVao, ref _pathEdgeVbo, edges.ToArray(), vp, new Vector4(1, 0.75f, 0.65f, 0.95f), PrimitiveType.Triangles, blend: true);
		DrawLines(ref _pathVao, ref _pathVbo, line.ToArray(), vp, new Vector4(1, 0.23f, 0.23f, 1), PrimitiveType.Triangles);
		DrawLines(ref _pathPreviewSoftVao, ref _pathPreviewSoftVbo, previewSoft.ToArray(), vp, new Vector4(1, 1, 1, 0.5f), PrimitiveType.Triangles, blend: true);
		DrawLines(ref _pathPreviewVao, ref _pathPreviewVbo, preview.ToArray(), vp, new Vector4(1, 1, 1, 0.9f), PrimitiveType.Triangles, blend: true);
		DrawLines(ref _pathDotVao, ref _pathDotVbo, dots.ToArray(), vp, new Vector4(1, 0.69f, 0.63f, 1));
	}

	private uint _placeVao, _placeVbo, _placeShapeVao, _placeShapeVbo;
	private HashSet<int> _ghostsDrawn = new();
	// The Place tool: a post at each placement the preview (or a stroke) shows, and the line, zone or
	// grid being drawn with its points.
	private void DrawPlace(WorldScene s, Matrix4x4 vp)
	{
		if (_mode != ToolMode.Place || Place is not { } pl)
		{
			return;
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		var posts = new List<float>();
		foreach (var o in pl.Shown)
		{
			if (_ghostsDrawn.Contains(TerrainEditor.Save.StableHash.Of(o.Name)))
			{
				continue;
			}
			float x = o.Position.X - ox - (s.W - 1) / 2f, z = -(o.Position.Z - oz - (s.H - 1) / 2f), y = o.Position.Y;
			posts.AddRange(new[] { x, y, z, x, y + 1.5f, z, x - 0.35f, y + 0.6f, z, x + 0.35f, y + 0.6f, z, x, y + 0.6f, z - 0.35f, x, y + 0.6f, z + 0.35f });
		}
		DrawLines(ref _placeVao, ref _placeVbo, posts.ToArray(), vp, new Vector4(0.62f, 0.88f, 1, 1));
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
					ring.AddRange(new[] { x, Picking.HeightAt(s, x, z) + 0.3f, z });
				}
			}
			DrawLines(ref _placeRingVao, ref _placeRingVbo, ring.ToArray(), vp, new Vector4(0.62f, 0.88f, 1, 1), width: 1.2f);
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
					data.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z });
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
				Strip(z.Append(z[0]).ToList());
			}
		}
		else if (t.Mode == PlaceTool.Modes.Grid && t.GridA is { } a && t.GridB is { } b)
		{
			Strip(new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y), a }.Select(t.Xf).ToList());
		}
		if (t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone)
		{
			foreach (var p in pl.ShownPoints())
			{
				var c = V(p);
				float r = Vector3.Distance(_lastEye, c) * 0.008f;
				Vector3[] k = { c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r) };
				for (int j = 0; j < 4; j++)
				{
					data.AddRange(new[] { k[j].X, k[j].Y, k[j].Z, k[(j + 1) % 4].X, k[(j + 1) % 4].Y, k[(j + 1) % 4].Z });
				}
			}
		}
		DrawLines(ref _placeShapeVao, ref _placeShapeVbo, data.ToArray(), vp, new Vector4(0.62f, 0.88f, 1, 1));
	}

	private uint _areaVao, _areaVbo, _resetVao, _resetVbo;
	// The Area selection (in the Area tool), following the ground, and the zones marked for reset (red, always).
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
					to.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
				}
			}
		}
		if (_mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
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
				data.AddRange(new[] { x, o.Y, z, x, o.Y + 1.2f, z, x - 0.3f, o.Y + 0.5f, z, x + 0.3f, o.Y + 0.5f, z });
			}
			DrawLines(ref _areaVao, ref _areaVbo, data.ToArray(), vp, new Vector4(1, 0.76f, 0.29f, 1));
		}
		if (_mode == ToolMode.Area)
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
					pts = Area.Points.ToList();
				}
				if (pts.Count > 1)
				{
					Ring(data, pts, false, 0.3f);
				}
			}
			DrawLines(ref _areaVao, ref _areaVbo, data.ToArray(), vp, new Vector4(0.37f, 0.83f, 1, 1));
		}
		if (s.Session is { } session)
		{
			var data = new List<float>();
			foreach (var r in session.Resets)
			{
				float gx0 = (r.X - s.X0) * 64, gz0 = (r.Z - s.Z0) * 64;
				Ring(data, new() { new(gx0, gz0), new(gx0 + 64, gz0), new(gx0 + 64, gz0 + 64), new(gx0, gz0 + 64) }, true, 0.6f);
			}
			DrawLines(ref _resetVao, ref _resetVbo, data.ToArray(), vp, new Vector4(1, 0.31f, 0.25f, 1));
		}
	}

	private uint _measureVao, _measureVbo;
	// The tape: following the ground from A to B (its profile), and a cross at each end. Shown in the
	// Measure tool, and kept once fixed.
	private void DrawMeasure(WorldScene s, Matrix4x4 vp)
	{
		var (a, b) = (Tape.A, Tape.B);
		if (a == null || _mode != ToolMode.Measure && !Tape.Fixed)
		{
			return;
		}
		var data = new List<float>();
		Vector3 V(float wx, float wz) => new(wx - s.Cx, GroundHeight(wx, wz) + 0.2f, -(wz - s.Cz));
		void Seg(Vector3 p, Vector3 q) => data.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
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
		DrawLines(ref _measureVao, ref _measureVbo, data.ToArray(), vp, new Vector4(1, 0.88f, 0.54f, 1));
	}

	private uint _lassoVao, _lassoVbo;
	// The zone being drawn with the Select tool, on the ground.
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
						foreach (var t in new[] { k / (float)n, (k + 1) / (float)n })
						{
							var p = Vector2.Lerp(a, b, t);
							float x = p.X - ox - (s.W - 1) / 2f, z = -(p.Y - oz - (s.H - 1) / 2f);
							data.AddRange(new[] { x, Picking.HeightAt(s, x, z) + 0.3f, z });
						}
					}
				}
			}
			_lassoSegs = data.ToArray();
		}
		DrawLines(ref _lassoVao, ref _lassoVbo, _lassoSegs, vp, new Vector4(1, 0.76f, 0.29f, 1));
	}

	private float[] _lassoSegs = Array.Empty<float>();

	private uint _ringVao, _ringVbo, _previewVao, _previewVbo;
	// The mountain preview's lines, kept while the pointer stays on the same grid point.
	private float[] _previewSegs = Array.Empty<float>();
	private (int X, int Z, int W)? _previewKey;

	// Whether a Mountain click at a grid point fits in the open area (what EditSession.Mountain needs).
	internal static bool MountainFits(WorldScene s, float gx, float gz, float reach) => gx - reach >= 1 && gz - reach >= 1 && gx + reach <= s.W - 2 && gz + reach <= s.H - 2;

	// The brush's outline on the ground (and the Ring shape's inner edge), seen through what stands on it.
	// Short segments, so a wide ring follows hills instead of cutting through them; none outside the open
	// area (no ground there). For the Mountain tool: the mountain itself as a mesh, and the ring in red
	// where it does not fit.
	private unsafe void DrawBrush(WorldScene s, Matrix4x4 vp)
	{
		if (_hover is not { } h || s.Session is not { } session || _tool == null && _mode is not (ToolMode.Shape or ToolMode.Mountain))
		{
			return;
		}
		float r = _tool == null ? ShapeRadius : session.Brush.Radius;
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
				data.AddRange(new[] { ax, ya + 0.3f, az, bx, yb + 0.3f, bz });
			}
		}
		(float, float) At((float X, float Z) o) => (h.X + o.X - (s.W - 1) / 2f, -(h.Z + o.Z - (s.H - 1) / 2f));
		Loop(_tool == null ? Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * ShapeRadius, MathF.Sin(i * MathF.Tau / n) * ShapeRadius)).ToList() : session.Brush.Outline(n));
		if (_tool != null && session.Brush.InnerOutline(n) is { } inner)
		{
			Loop(inner);
		}
		bool fits = _mode != ToolMode.Mountain || MountainFits(s, h.X, h.Z, ShapeRadius);
		DrawLines(ref _ringVao, ref _ringVbo, data.ToArray(), vp, fits ? new Vector4(1, 1, 1, 1) : new Vector4(1, 0.35f, 0.3f, 1), width: 1.2f);
		if (_tool == null && _mode == ToolMode.Mountain && fits && _mountainPreview is { } shape)
		{
			var key = ((int)MathF.Round(h.X), (int)MathF.Round(h.Z), s.W);
			if (_previewKey != key)
			{
				_previewKey = key;
				_previewSegs = MountainMesh(s, key.Item1, key.Item2, ShapeRadius, shape);
			}
			DrawLines(ref _previewVao, ref _previewVbo, _previewSegs, vp, new Vector4(1, 0.78f, 0.35f, 0.9f), width: 1f);
		}
	}

	// The mountain as it would rise at grid point (cx, cz): lines along x and z every few metres, each
	// point at the ground plus the shape's height; flat ground (under half a metre added) left out.
	internal static float[] MountainMesh(WorldScene s, int cx, int cz, float reach, Func<float, float, float> shape)
	{
		const int Cells = 40;
		float step = 2 * reach / Cells;
		var y = new float[(Cells + 1) * (Cells + 1)];
		var up = new bool[y.Length];
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
			data.AddRange(new[] { cx - reach + i0 * step - (s.W - 1) / 2f, y[a], -(cz - reach + j0 * step - (s.H - 1) / 2f) });
			data.AddRange(new[] { cx - reach + i1 * step - (s.W - 1) / 2f, y[b], -(cz - reach + j1 * step - (s.H - 1) / 2f) });
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
		return data.ToArray();
	}

	// ---- Selection: the objects picked (indices into the scene's things), drawn as orange boxes.
	private (Vector3 Min, Vector3 Max)[] _bounds = Array.Empty<(Vector3, Vector3)>();
	private bool[] _known = Array.Empty<bool>();
	// Where each thing was when its box was made (a move's preview may be ahead of the boxes).
	private Vector3[] _boundsAt = Array.Empty<Vector3>();
	private ObjectKind[] _kinds = Array.Empty<ObjectKind>();
	private readonly HashSet<int> _selection = new();
	private Matrix4x4 _lastViewProj;
	private bool _selectionDirty;
	public event Action<IReadOnlyList<WorldScene.Thing>>? SelectionChanged;

	// A double click in Select mode.
	public event Action? InspectAsked;
	internal IReadOnlyCollection<int> Selected { get { lock (_selection) { return _selection.ToArray(); } } }

	// The shown object under a point of the view (null: the ground or nothing is nearer), like the web
	// editor: the nearest box the ray enters, unless the ground is hit first (half a metre of slack).
	// The Workshop's cut: nothing drawn above this height (world y), and the cursor goes through what is
	// hidden; null: none.
	private float? _cutY;
	internal float? CutY
	{
		get => _cutY;
		set
		{
			_cutY = value;
			Wake();
		}
	}

	// The view's ray through a point, in world space (x east, up, z north), and how far along it the
	// ground is (null: it does not meet it).
	internal (Vector3 O, Vector3 D, float? GroundT)? WorldRay(Point at, Size size)
	{
		var s = _scene;
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
		if (_shown[(int)ObjectKind.Dungeons] && DungeonRooms.Of(s) is { Count: > 0 } dungeons && RoomSurfaces.Hit(dungeons, _models, wo, wd, CutY ?? float.MaxValue) is float rt && (g == null || rt < g))
		{
			g = rt;
		}
		return (wo, wd, g);
	}

	internal int? ObjectAt(Point at, Size size)
	{
		var s = _scene;
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
			if (!_known[i] || !_shown[(int)_kinds[i]] || _cutY is float cut && _bounds[i].Min.Y > cut)
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

	// A click on an object: it alone, or (add) added or taken out; range (Shift): every shown object in
	// a row from the last one clicked to this one (w1, Shift + click w3: w1, w2 and w3).
	internal void Pick(Point at, Size size, bool add, bool range = false)
	{
		int? i = ObjectAt(at, size);
		var s = _scene;
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

	// The object a Shift + click's row starts from: the last one clicked.
	private int? _anchor;

	// The shown objects on the line from object a to object b (their middles within 0.75 m of it), both
	// ends included.
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

	// Eyedropper: the next left click gives the object under it (null: none) instead of going to
	// the tool; its release is swallowed too.
	internal Action<int?>? PickObjectOnce { get; set; }
	private bool _pickSwallow;

	// Selects these things (add: to what is selected).
	internal void Select(IEnumerable<int> things, bool add = false)
	{
		var s = _scene;
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

	private void SelectionDone()
	{
		_selectionDirty = true;
		var s = _scene;
		if (s != null)
		{
			var things = Selected.Select(n => s.Things[n]).ToList();
			Dispatcher.UIThread.Post(() => SelectionChanged?.Invoke(things));
		}
		Wake();
	}

	// Whether a thing is drawn (not gone, its kind shown, its model known): only those can be picked.
	// Tests (headless, so nothing is drawn): the boxes drawing gives things, in view space.
	internal void SetBoxes(IEnumerable<(int Index, Vector3 Min, Vector3 Max)> boxes)
	{
		var s = _scene!;
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

	// The test driver's pictures: the next frame once the area and its models are in.
	private volatile GlPicture.Request? _picture;

	internal Task Picture(string path)
	{
		var r = new GlPicture.Request { Path = path };
		// Measured again for this area: the picture waits until it and its models are in.
		if (_scene != _loadedScene)
		{
			_loadedAt = -1;
			_loadedScene = _scene;
		}
		_picture = r;
		Wake();
		return r.Done.Task;
	}

	internal int FramesDrawn => _frames.Count;

	// The test driver checks drawing raised no OpenGL error (counted only when driven).
	internal int GlErrors { get; private set; }

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

	internal bool IsPickable(int i)
	{
		lock (_objLock)
		{
			return i < _known.Length && _known[i] && _shown[(int)_kinds[i]];
		}
	}

	// A thing's box and the position it was made at (world coordinates).
	internal (Vector3 Min, Vector3 Max, Vector3 At) BoxOf(int i)
	{
		lock (_objLock)
		{
			return (_bounds[i].Min, _bounds[i].Max, _boundsAt[i]);
		}
	}

	internal (Vector3 Min, Vector3 Max) BoundsOf(int i)
	{
		lock (_objLock)
		{
			return _bounds[i];
		}
	}

	internal ObjectKind KindOf(int i)
	{
		lock (_objLock)
		{
			return _kinds[i];
		}
	}

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

	private uint _lineProg, _lineVao, _lineVbo;
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
					v.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
				}
			}
			_selectionSegs = v.ToArray();
		}
		// Seen through what is in front of it, like the web editor's selection.
		DrawLines(ref _lineVao, ref _lineVbo, _selectionSegs, vp, new Vector4(1, 0.66f, 0.2f, 1));
	}

	private float[] _selectionSegs = Array.Empty<float>();

	// The Select tool: the object under the pointer, outlined faintly before a click picks it.
	private int? _hoverObject;
	private uint _hoverBoxVao, _hoverBoxVbo;

	private void SetHoverObject(int? i)
	{
		if (i != _hoverObject)
		{
			_hoverObject = i;
			Wake();
		}
	}

	private void DrawHoverObject(Matrix4x4 vp)
	{
		// Only what a click can pick: drawn and not deleted (a deleted object stays under the pointer).
		if (!_selectMode || _tool != null || _hoverObject is not int i || i >= _bounds.Length || _selection.Contains(i)
			|| !_known[i] || !_shown[(int)_kinds[i]] || _scene is not { } sc || i >= sc.Things.Count || sc.Things[i].Gone)
		{
			return;
		}
		var (lo, hi) = _bounds[i];
		Vector3 C(int k) => new((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z);
		var v = new List<float>();
		foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
		{
			var p = C(a); var q = C(b);
			v.AddRange(new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
		}
		DrawLines(ref _hoverBoxVao, ref _hoverBoxVbo, v.ToArray(), vp, new Vector4(1, 0.85f, 0.55f, 0.6f), blend: true, width: 0.8f);
	}

	// Slides the view along the ground by a drag of (dx, dy) points (under the camera lock).
	private void Slide(float dx, float dy)
	{
		float k = _distance * 0.0015f, c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
		_target += new Vector3(-dx * c - dy * sn, 0, dx * sn - dy * c) * k;
		FollowGround();
	}

	private void Drag(Point p)
	{
		if (_dragFrom is Point from)
		{
			float dx = (float)(p.X - from.X), dy = (float)(p.Y - from.Y);
			lock (_camLock)
			{
				if (_panDrag)
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
