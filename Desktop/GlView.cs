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

namespace TerrainEditor.Desktop;

// The 3D view: the ground, the water and every object and building piece of a WorldScene, drawn with
// OpenGL (OpenGL 3.3, or OpenGL ES 3.0 where Avalonia gives that, e.g. ANGLE on Windows). Objects are
// drawn with GPU instancing: one draw per model part for all its copies. Models load on worker threads
// and appear as they arrive. Drawn only while something happens (the camera moves, keys, the mouse),
// a few times a second otherwise.
public sealed class GlView : OpenGlControlBase
{
	private GL _gl = null!;
	private bool _es;
	private WorldScene? _scene;
	private ModelStore? _models;

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

	// Walking: the eyes 1.8 m over the ground (or the water); flying: never below that.
	private void KeepOverGround(bool walk)
	{
		var s = _scene;
		if (s == null)
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
		var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
		idle.Tick += (_, _) => { if (_look != null || _clock.ElapsedMilliseconds - _idleAt >= 250) { _idleAt = _clock.ElapsedMilliseconds; RequestNextFrameRendering(); } };
		idle.Start();
	}

	public void Show(WorldScene scene, ModelStore? models)
	{
		_scene = scene;
		_models = models;
		int g = (scene.H / 2) * scene.W + scene.W / 2;
		lock (_camLock)
		{
			_target = new Vector3(0, scene.Heights[g], 0);
		}
		_sceneDirty = true;
		_lookFiles = null;
		if (scene.Session != null)
		{
			scene.Session.Changed += Wake;
			scene.Session.ThingsChanged += OnThingsChanged;
			scene.Session.ThingsReset += () => { _thingsReset = true; Wake(); };
		}
		Task.Run(() =>
		{
			try
			{
				_lookFiles = GameLookGl.Read();
				Status?.Invoke(_lookFiles == null ? "Game look not copied yet: plain colours (open the web editor once to copy it)." : "Game look loaded.");
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
		public required (int Prefab, bool Piece) Key { get; init; }
		public required ObjectKind Kind { get; init; }
		public readonly List<int> Things = new();
		public Vector3 RootScale = Vector3.One;
		// The model's box in its own frame (for picking), known once read.
		public (Vector3 Min, Vector3 Max)? Box;
		public readonly List<(Batch Batch, Matrix4x4 Pre)> Batches = new();
		public bool Ready;
	}
	private readonly Dictionary<(int, bool), Group> _groups = new();
	private readonly HashSet<Group> _dirtyGroups = new();
	// Things shown somewhere else than where they are, while a move is being made (Select tool).
	private Dictionary<int, WorldScene.Thing> _previews = new();
	private volatile bool _thingsReset, _overlaysDirty;
	private readonly Dictionary<string, (uint Vbo, uint[] Ebos, int[] Counts)> _meshGl = new();
	private readonly Dictionary<string, uint> _textures = new();
	// Models read on worker threads, waiting to go to the graphics card (on the drawing thread).
	private sealed record ReadyModel(Group Group, ModelStore.Model? Model, Dictionary<string, ModelStore.MeshData> Meshes,
		Dictionary<string, ModelStore.MaterialData> Materials, Dictionary<string, ModelStore.ImageData?> Images);
	private readonly ConcurrentQueue<ReadyModel> _ready = new();
	private int _pending;
	private uint _boxVbo, _boxEbo;

	protected override void OnOpenGlInit(GlInterface gli)
	{
		_gl = GL.GetApi(name => gli.GetProcAddress(name));
		_es = GlVersion.Type == GlProfileType.OpenGLES;
		_terrainProg = Program(Shaders.TerrainVs, Shaders.TerrainFs);
		_objectProg = Program(Shaders.ObjectVs, Shaders.ObjectFs);
		_waterProg = Program(Shaders.WaterVs, Shaders.WaterFs);
		Status?.Invoke($"OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}");
	}

	protected override void OnOpenGlDeinit(GlInterface gli)
	{
	}

	private uint Program(string vs, string fs)
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

	private unsafe void BuildTerrain(WorldScene s)
	{
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
		_terrainVao = _gl.GenVertexArray();
		_gl.BindVertexArray(_terrainVao);
		uint vbo = _terrainVbo = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
		fixed (float* p = v)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * 4), p, BufferUsageARB.StaticDraw);
		}
		uint ebo = _gl.GenBuffer();
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
		uint bc = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, bc);
		fixed (byte* p = s.BiomeColor)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)s.BiomeColor.Length, p, BufferUsageARB.StaticDraw);
		}
		_gl.EnableVertexAttribArray(3);
		_gl.VertexAttribPointer(3, 4, VertexAttribPointerType.UnsignedByte, true, 4, (void*)0);
		float[] extra = ExtraRows(s, 0, h - 1);
		uint ex = _terrainExtraVbo = _gl.GenBuffer();
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
		_waterVao = _gl.GenVertexArray();
		_gl.BindVertexArray(_waterVao);
		uint wv = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, wv);
		fixed (float* p = q)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(q.Length * 4), p, BufferUsageARB.StaticDraw);
		}
		uint we = _gl.GenBuffer();
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
		var key = (t.Prefab, t.Piece);
		EnsureSize(i + 1);
		if (!_groups.TryGetValue(key, out var g))
		{
			g = _groups[key] = new Group { Key = key, Kind = ObjectKinds.Of(NameOf(t.Prefab), t.Piece) };
			if (start)
			{
				Interlocked.Increment(ref _pending);
				Load(g);
			}
		}
		if (!g.Things.Contains(i))
		{
			g.Things.Add(i);
		}
		lock (_objLock)
		{
			_kinds[i] = g.Kind;
		}
		return g;
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
			Array.Resize(ref _kinds, size);
		}
	}

	private void SendKindCounts(WorldScene s)
	{
		var counts = new Dictionary<ObjectKind, int>();
		lock (s.Things)
		{
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
				if (_groups.TryGetValue((t.Prefab, t.Piece), out var g))
				{
					MarkDirty(g);
				}
			}
		}
		_selectionDirty = true;
		Wake();
	}

	// Reads a group's model on a worker thread; the drawing thread sends it to the graphics card.
	private void Load(Group g)
	{
		Task.Run(() =>
		{
			_loadGate.Wait();
			try
			{
				string? name = NameOf(g.Key.Prefab);
				var model = name != null && _models != null ? _models.LoadModel(name) : null;
				var meshes = new Dictionary<string, ModelStore.MeshData>();
				var mats = new Dictionary<string, ModelStore.MaterialData>();
				var images = new Dictionary<string, ModelStore.ImageData?>();
				Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
				if (model != null)
				{
					foreach (var part in model.Parts)
					{
						if (_models!.LoadMesh(part.Mesh) is { } md)
						{
							meshes.TryAdd(part.Mesh, md);
							// The object's box, for picking: the parts' meshes.
							var (a, b) = Picking.Transform(md.Bounds.Min, md.Bounds.Max, part.Matrix);
							lo = Vector3.Min(lo, a);
							hi = Vector3.Max(hi, b);
						}
						if (!mats.ContainsKey(part.Material))
						{
							var mat = _models.Material(part.Material);
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
				g.Box = (lo, hi);
				_ready.Enqueue(new ReadyModel(g, model, meshes, mats, images));
			}
			catch (Exception ex)
			{
				Status?.Invoke($"Model of {g.Key.Prefab}: {ex.Message}");
				Interlocked.Decrement(ref _pending);
			}
			finally
			{
				_loadGate.Release();
			}
			Wake();
		});
	}
	private readonly SemaphoreSlim _loadGate = new(Math.Max(2, Environment.ProcessorCount / 2));
	private readonly object _objLock = new();

	// A group's instances (and its things' boxes) from where its things are now.
	private unsafe void Rebuild(WorldScene s, Group g)
	{
		var list = new List<Matrix4x4>();
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
					}
					_known[i] = !t.Gone && g.Box != null;
					if (!t.Gone)
					{
						list.Add(m);
					}
				}
			}
		}
		foreach (var (b, pre) in g.Batches)
		{
			var data = list.Select(m => pre * m).ToArray();
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
			fixed (Matrix4x4* ptr = data)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), ptr, BufferUsageARB.DynamicDraw);
			}
			b.Instances = data.Length;
		}
	}

	// Everything read again (after a save): the old batches go.
	private void DropObjects()
	{
		foreach (var b in _batches)
		{
			_gl.DeleteVertexArray(b.Vao);
			_gl.DeleteBuffer(b.InstanceVbo);
		}
		_batches.Clear();
		while (_ready.TryDequeue(out _))
		{
		}
		lock (_selection)
		{
			_selection.Clear();
		}
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

	private unsafe void Upload(ReadyModel r)
	{
		foreach (var (file, img) in r.Images)
		{
			if (img == null || _textures.ContainsKey(file))
			{
				continue;
			}
			uint tex = _gl.GenTexture();
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
			lock (_textures)
			{
				_textures[file] = tex;
			}
		}
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
		uint vbo = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
		fixed (float* p = md!.Vertices)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(md.Vertices.Length * 4), p, BufferUsageARB.StaticDraw);
		}
		var ebos = new uint[md.Submeshes.Length];
		var counts = new int[md.Submeshes.Length];
		for (int s = 0; s < ebos.Length; s++)
		{
			ebos[s] = _gl.GenBuffer();
			_gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebos[s]);
			fixed (uint* p = md.Submeshes[s])
			{
				_gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(md.Submeshes[s].Length * 4), p, BufferUsageARB.StaticDraw);
			}
			counts[s] = md.Submeshes[s].Length;
		}
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
		b.Vao = _gl.GenVertexArray();
		_gl.BindVertexArray(b.Vao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Vbo);
		_gl.EnableVertexAttribArray(0);
		_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
		_gl.EnableVertexAttribArray(1);
		_gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
		_gl.EnableVertexAttribArray(2);
		_gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
		_gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Ebos[sub]);
		b.InstanceVbo = _gl.GenBuffer();
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
	protected override unsafe void OnOpenGlRender(GlInterface gli, int fb)
	{
		long now = _clock.ElapsedMilliseconds;
		var start = Stopwatch.GetTimestamp();
		var s = _scene;
		if (s != null && _sceneDirty)
		{
			_sceneDirty = false;
			BuildTerrain(s);
			StartModels(s);
		}
		if (s != null && _look == null && _lookFiles is { } lf && _terrainVao != 0)
		{
			try
			{
				var look = new GameLookGl();
				look.Init(_gl, Program, lf, s);
				_look = look;
			}
			catch (Exception ex)
			{
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
			if (_overlaysDirty && _terrainVao != 0)
			{
				_overlaysDirty = false;
				Overlays.Built o;
				lock (s.Things)
				{
					o = _overlays = Overlays.Build(s, s.Modifiers, i => NameOf(s.Things[i].Prefab));
				}
				UploadOverlays(o);
				Dispatcher.UIThread.Post(() => OverlaysBuilt?.Invoke(o));
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
		_gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
		_gl.Viewport(0, 0, (uint)pw, (uint)ph);
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
			}
			else
			{
				eye = _eyePos;
				view = Matrix4x4.CreateLookAt(eye, eye + Forward, Vector3.UnitY);
			}
		}
		proj = Perspective(60 * MathF.PI / 180, pw / (float)ph, 0.5f, 6000);
		var vp = view * proj;
		_lastViewProj = vp;
		_lastEye = eye;
		bool camMoved = view != _lastView;
		_lastView = view;
		// The brush: where it is on the ground, a step of the stroke while the button is held, and the
		// changed ground to the graphics card.
		if (s != null)
		{
			_hover = (_tool != null || _mode == ToolMode.Shape) && _pointer is Point at ? GroundAt(s, vp, at, _surfaceSize) : null;
			if (_brushDown && _hover is { } hv)
			{
				s.Session?.StrokeStep(hv.X, hv.Z, dt);
			}
			if (s.Session?.TakeDirty() is { } d && _terrainVao != 0)
			{
				UpdateTerrain(s, d.Z0, d.Z1);
			}
		}
		var sun = GameLookGl.SunDirView;
		float time = _clock.ElapsedMilliseconds / 1000f;
		if (s != null && _terrainVao != 0)
		{
			if (_look != null)
			{
				_look.DrawSky(vp, eye, time);
				_look.UseTerrain(vp, eye, time, slope: _slope, contour: _contour);
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
			V3("uEye", eye);
			int uColor = _gl.GetUniformLocation(_objectProg, "uColor"), uCut = _gl.GetUniformLocation(_objectProg, "uCutoff"),
				uHasMap = _gl.GetUniformLocation(_objectProg, "uHasMap"), uUv = _gl.GetUniformLocation(_objectProg, "uUv");
			_gl.Uniform1(_gl.GetUniformLocation(_objectProg, "uMap"), 0);
			_gl.ActiveTexture(TextureUnit.Texture0);
			foreach (var b in _batches)
			{
				if (!_shown[(int)b.Kind])
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

			DrawOverlays(vp);
			DrawSelection(vp);
			DrawBrush(s, vp);
			DrawLasso(s, vp);
			DrawGizmo(s, vp);
			DrawMeasure(s, vp);
			if (ShowWater)
			{
				if (_look != null)
				{
					_look.DrawWater(vp, eye, time);
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
		}
		_gl.BindVertexArray(0);
		_gl.UseProgram(0);
		double work = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
		Measure(now, work, camMoved, pw, ph);
		Automate(now, pw, ph);
		// Full speed while something happens; the idle timer draws a few times a second otherwise.
		if (camMoved || moving || _brushDown || now - _wokeAt < 1000 || !_ready.IsEmpty || _pending > 0)
		{
			RequestNextFrameRendering();
		}
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
	private long _loadedAt = -1, _benchFrom = -1;
	private readonly List<(long At, double Work)> _bench = new();
	private unsafe void Automate(long now, int pw, int ph)
	{
		if (Options.QuitAfter > 0 && now > Options.QuitAfter * 1000)
		{
			lock (_camLock)
			{
				Options.Say($"camera: yaw {_yaw:0.000}, pitch {_pitch:0.000}, distance {_distance:0.0}, target {_target.X:0.0} {_target.Y:0.0} {_target.Z:0.0}");
			}
			Options.QuitAfterDone();
			Dispatcher.UIThread.Post(() => (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
			return;
		}
		if (Options.Shot == null && Options.Bench <= 0)
		{
			return;
		}
		if (_loadedAt < 0)
		{
			if (_scene != null && _terrainVao != 0 && _pending == 0 && _ready.IsEmpty)
			{
				_loadedAt = now;
				if (Options.PickAt is var (px, py))
				{
					var size = Bounds.Size;
					Pick(new Point(px * size.Width, py * size.Height), size, add: false);
					var sel = Selected;
					Options.Say(sel.Count == 0 ? "picked: nothing" : $"picked: {string.Join(", ", sel.Select(i => $"{_models?.NameOf(_scene!.Things[i].Prefab)} ({_kinds[i]})"))}");
					if (Options.MoveBy is var (mx, mz) && sel.Count > 0)
					{
						Dispatcher.UIThread.Post(() =>
						{
							var before = _scene!.Things[sel.First()].Position;
							SelectTool.PlaceAt(mx, 0, mz, 0, by: true);
							var now = Selected.Select(i => _scene.Things[i].Position).FirstOrDefault();
							Options.Say($"moved: {before.X:0.0} {before.Y:0.0} {before.Z:0.0} → {now.X:0.0} {now.Y:0.0} {now.Z:0.0}, {_scene.Session?.PendingText}");
						});
					}
				}
				if (Options.TapeAt is { Length: 4 } tp)
				{
					var size = Bounds.Size;
					var ta = WorldAt(new Point(tp[0] * size.Width, tp[1] * size.Height), size);
					var tb = WorldAt(new Point(tp[2] * size.Width, tp[3] * size.Height), size);
					if (ta is { } pa && tb is { } pb)
					{
						Tape.Down(pa);
						Tape.Down(pb);
						var r = Tape.Measure(GroundHeight, _scene.Water)!;
						Options.Say($"tape: {r.Distance:0.0} m, rise {r.Rise:0.0} m, slope {r.SlopeDegrees:0.0}°, lowest {r.Lowest:0.0}, highest {r.Highest:0.0}");
					}
				}
				if (Options.StrokeTool is BrushTool tool && _scene.Session is { } session)
				{
					var size = Bounds.Size;
					_tool = tool;
					_pointer = new Point(size.Width / 2, size.Height / 2);
					_surfaceSize = size;
					if (GroundAt(_scene, _lastViewProj, _pointer.Value, size) is { } at)
					{
						int g = (int)MathF.Round(at.Z) * _scene.W + (int)MathF.Round(at.X);
						float before = _scene.Heights[g];
						session.BeginStroke(tool, at.X, at.Z);
						for (int i = 0; i < 40; i++)
						{
							session.StrokeStep(at.X, at.Z, 1 / 30f);
						}
						string msg = session.EndStroke();
						Options.Say($"stroke {tool} at {at.X:0.0}, {at.Z:0.0}: ground {before:0.00} → {_scene.Heights[g]:0.00} m, {session.PendingText} {msg}");
					}
				}
				Options.Say($"loaded: {_scene.Things.Count} objects, {_batches.Sum(b => b.Instances)} model parts in {_batches.Count} draws, {_textures.Count} textures, view {pw}×{ph} px, {_gl.GetStringS(StringName.Renderer)}");
			}
			RequestNextFrameRendering();
			return;
		}
		if (Options.Shot != null && now - _loadedAt > 500)
		{
			byte[] px = new byte[pw * ph * 4];
			fixed (byte* p = px)
			{
				_gl.ReadPixels(0, 0, (uint)pw, (uint)ph, PixelFormat.Rgba, PixelType.UnsignedByte, p);
			}
			// OpenGL rows go bottom up.
			var info = new SkiaSharp.SKImageInfo(pw, ph, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
			using var bmp = new SkiaSharp.SKBitmap(info);
			for (int y = 0; y < ph; y++)
			{
				System.Runtime.InteropServices.Marshal.Copy(px, (ph - 1 - y) * pw * 4, bmp.GetPixels() + y * pw * 4, pw * 4);
			}
			using (var f = File.Create(Options.Shot))
			{
				bmp.Encode(f, SkiaSharp.SKEncodedImageFormat.Png, 90);
			}
			Options.Say($"picture: {Options.Shot}");
			if (Options.Bench <= 0)
			{
				Dispatcher.UIThread.Post(() => (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
				return;
			}
			Options.ClearShot();
		}
		if (Options.Bench > 0)
		{
			if (_benchFrom < 0)
			{
				_benchFrom = now;
			}
			_bench.Add((now, 0));
			lock (_camLock)
			{
				_yaw += 0.01f;
			}
			if (now - _benchFrom > Options.Bench * 1000)
			{
				var gaps = _bench.Zip(_bench.Skip(1), (a, b) => b.At - a.At).OrderBy(g => g).ToList();
				Options.Say($"bench: {_bench.Count / ((now - _benchFrom) / 1000.0):0} fps over {(now - _benchFrom) / 1000.0:0.0} s, work {_frames.Average(f => f.Work):0.0} ms, gaps median {gaps[gaps.Count / 2]} ms, 95% {gaps[gaps.Count * 95 / 100]} ms, max {gaps[^1]} ms, view {pw}×{ph} px");
				Dispatcher.UIThread.Post(() => (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
				Options.ClearBench();
			}
			RequestNextFrameRendering();
		}
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

	// The target point stays on the ground (or the water) under it.
	private void FollowGround()
	{
		var s = _scene;
		if (s == null)
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
			if (_dragButton == PointerUpdateKind.LeftButtonPressed && _mode == ToolMode.Shape)
			{
				_dragFrom = null;
				if (_scene is { } sc && GroundAt(sc, _lastViewProj, p.Position, _surfaceSize) is { } g)
				{
					ShapeClicked?.Invoke(g.X, g.Z);
				}
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
				SelectTool.Down(p.Position, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.ClickCount);
				Wake();
				return;
			}
			// With a brush, the left button paints (the middle one still slides the view).
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
			// A left click (not a drag) picks the object under the pointer.
			var at = e.GetPosition(surface);
			if (_selectDown)
			{
				_selectDown = false;
				SelectTool.Up(at, surface.Bounds.Size, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
			}
			else if (_brushDown)
			{
				_brushDown = false;
				string message = _scene?.Session?.EndStroke() ?? "";
				StrokeEnded?.Invoke(message);
			}
			else if (_mode == ToolMode.View && _dragButton == PointerUpdateKind.LeftButtonPressed && _pressAt is Point from && Point.Distance(from, at) < 5)
			{
				Pick(at, surface.Bounds.Size, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
			}
			_pressAt = null;
			_dragFrom = null;
			e.Pointer.Capture(null);
			Wake();
		};
		surface.PointerMoved += (_, e) =>
		{
			_pointer = e.GetPosition(surface);
			_surfaceSize = surface.Bounds.Size;
			if (_selectDown)
			{
				SelectTool.Moved(_pointer.Value, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Control));
			}
			else if (_selectMode && _tool == null)
			{
				SelectTool.Hover(_pointer.Value, _surfaceSize);
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
			Wake();
		};
		surface.PointerWheelChanged += (_, e) =>
		{
			lock (_camLock)
			{
				if (_eye == EyeMode.Orbit)
				{
					_distance = Math.Clamp(_distance * MathF.Pow(0.88f, (float)e.Delta.Y), 3, 3000);
				}
			}
			Wake();
		};
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

	internal WorldScene? Scene => _scene;
	// The Select tool takes the left button (see SelectTool); otherwise it slides the view and clicks pick.
	public SelectTool SelectTool { get; }
	private ToolMode _mode;
	public ToolMode Mode { get => _mode; set { if (value != ToolMode.Select) SelectTool.Commit(); _mode = value; Wake(); } }
	public bool SelectMode => _mode == ToolMode.Select;
	private bool _selectMode => _mode == ToolMode.Select;
	public MeasureTool Tape { get; } = new();
	// Shape tool: a click on the ground (grid point), and the radius its outline shows.
	public event Action<float, float>? ShapeClicked;
	public float ShapeRadius { get; set; } = 16;
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
				uint vao = _gl.GenVertexArray(), vbo = _gl.GenBuffer();
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
	private unsafe void DrawLines(ref uint vao, ref uint vbo, float[] data, Matrix4x4 vp, Vector4 color)
	{
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
			vao = _gl.GenVertexArray();
			vbo = _gl.GenBuffer();
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
		_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(data.Length / 3));
		_gl.Enable(EnableCap.DepthTest);
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
	private int _lassoCount;
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
			if (_lassoVao == 0)
			{
				_lassoVao = _gl.GenVertexArray();
				_lassoVbo = _gl.GenBuffer();
				_gl.BindVertexArray(_lassoVao);
				_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lassoVbo);
				_gl.EnableVertexAttribArray(0);
				_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
			}
			var arr = data.ToArray();
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lassoVbo);
			fixed (float* p = arr)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(arr.Length * 4), p, BufferUsageARB.DynamicDraw);
			}
			_lassoCount = arr.Length / 3;
		}
		if (_lassoCount == 0)
		{
			return;
		}
		if (_lineProg == 0)
		{
			_lineProg = Program(Shaders.LineVs, Shaders.LineFs);
		}
		_gl.Disable(EnableCap.DepthTest);
		_gl.UseProgram(_lineProg);
		_gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&vp);
		_gl.Uniform4(_gl.GetUniformLocation(_lineProg, "uColor"), 1f, 0.76f, 0.29f, 1f);
		_gl.BindVertexArray(_lassoVao);
		_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_lassoCount);
		_gl.Enable(EnableCap.DepthTest);
	}

	private uint _ringVao, _ringVbo;
	// The brush's outline on the ground (and the Ring shape's inner edge), seen through what stands on it.
	private unsafe void DrawBrush(WorldScene s, Matrix4x4 vp)
	{
		if (_hover is not { } h || s.Session is not { } session || _tool == null && _mode != ToolMode.Shape)
		{
			return;
		}
		const int n = 96;
		var data = new List<float>();
		void Loop(List<(float X, float Z)> pts)
		{
			for (int i = 0; i < pts.Count; i++)
			{
				foreach (var (ox, oz) in new[] { pts[i], pts[(i + 1) % pts.Count] })
				{
					float x = h.X + ox - (s.W - 1) / 2f, z = -(h.Z + oz - (s.H - 1) / 2f);
					data.AddRange(new[] { x, Picking.HeightAt(s, x, z) + 0.3f, z });
				}
			}
		}
		Loop(_tool == null ? Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * ShapeRadius, MathF.Sin(i * MathF.Tau / n) * ShapeRadius)).ToList() : session.Brush.Outline(n));
		if (_tool != null && session.Brush.InnerOutline(n) is { } inner)
		{
			Loop(inner);
		}
		if (_lineProg == 0)
		{
			_lineProg = Program(Shaders.LineVs, Shaders.LineFs);
		}
		if (_ringVao == 0)
		{
			_ringVao = _gl.GenVertexArray();
			_ringVbo = _gl.GenBuffer();
			_gl.BindVertexArray(_ringVao);
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ringVbo);
			_gl.EnableVertexAttribArray(0);
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
		}
		var arr = data.ToArray();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ringVbo);
		fixed (float* p = arr)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(arr.Length * 4), p, BufferUsageARB.DynamicDraw);
		}
		_gl.Disable(EnableCap.DepthTest);
		_gl.UseProgram(_lineProg);
		_gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&vp);
		_gl.Uniform4(_gl.GetUniformLocation(_lineProg, "uColor"), 1f, 1f, 1f, 1f);
		_gl.BindVertexArray(_ringVao);
		_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(arr.Length / 3));
		_gl.Enable(EnableCap.DepthTest);
	}

	// ---- Selection: the objects picked (indices into the scene's things), drawn as orange boxes.
	private (Vector3 Min, Vector3 Max)[] _bounds = Array.Empty<(Vector3, Vector3)>();
	private bool[] _known = Array.Empty<bool>();
	private ObjectKind[] _kinds = Array.Empty<ObjectKind>();
	private readonly HashSet<int> _selection = new();
	private Matrix4x4 _lastViewProj;
	private bool _selectionDirty;
	public event Action<IReadOnlyList<WorldScene.Thing>>? SelectionChanged;
	internal IReadOnlyCollection<int> Selected { get { lock (_selection) { return _selection.ToArray(); } } }

	// The shown object under a point of the view (null: the ground or nothing is nearer), like the web
	// editor: the nearest box the ray enters, unless the ground is hit first (half a metre of slack).
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
			if (!_known[i] || !_shown[(int)_kinds[i]])
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

	internal void Pick(Point at, Size size, bool add)
	{
		int? i = ObjectAt(at, size);
		lock (_selection)
		{
			if (!add)
			{
				_selection.Clear();
			}
			if (i is int k && !_selection.Remove(k))
			{
				_selection.Add(k);
			}
		}
		SelectionDone();
	}

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
	internal bool IsPickable(int i)
	{
		lock (_objLock)
		{
			return i < _known.Length && _known[i] && _shown[(int)_kinds[i]];
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
				old = (_gl.GenVertexArray(), 0, _gl.GenBuffer());
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
	private int _lineCount;
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
			if (_lineProg == 0)
			{
				_lineProg = Program(Shaders.LineVs, Shaders.LineFs);
			}
			if (_lineVao == 0)
			{
				_lineVao = _gl.GenVertexArray();
				_lineVbo = _gl.GenBuffer();
				_gl.BindVertexArray(_lineVao);
				_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
				_gl.EnableVertexAttribArray(0);
				_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
			}
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
			var data = v.ToArray();
			fixed (float* p = data)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
			}
			_lineCount = data.Length / 3;
		}
		if (_lineCount == 0)
		{
			return;
		}
		// Seen through what is in front of it, like the web editor's selection.
		_gl.Disable(EnableCap.DepthTest);
		_gl.UseProgram(_lineProg);
		_gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&vp);
		_gl.Uniform4(_gl.GetUniformLocation(_lineProg, "uColor"), 1f, 0.66f, 0.2f, 1f);
		_gl.BindVertexArray(_lineVao);
		_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_lineCount);
		_gl.Enable(EnableCap.DepthTest);
	}

	private void Drag(Point p)
	{
		if (_dragFrom is Point from)
		{
			float dx = (float)(p.X - from.X), dy = (float)(p.Y - from.Y);
			lock (_camLock)
			{
				if (_dragButton == PointerUpdateKind.RightButtonPressed)
				{
					_yaw -= dx * 0.005f;
					_pitch = _eye == EyeMode.Orbit ? Math.Clamp(_pitch + dy * 0.005f, 0.05f, 1.55f) : Math.Clamp(_pitch + dy * 0.005f, -1.45f, 1.45f);
				}
				else if (_eye == EyeMode.Orbit && _dragButton is PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.LeftButtonPressed)
				{
					float k = _distance * 0.0015f, c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
					_target += new Vector3(-dx * c - dy * sn, 0, dx * sn - dy * c) * k;
					FollowGround();
				}
			}
			_dragFrom = p;
		}
		Wake();
	}
}
