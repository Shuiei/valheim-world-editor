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

	// For tests: the camera, and the keys held.
	internal (float Yaw, float Pitch, float Distance, Vector3 Target) Camera { get { lock (_camLock) { return (_yaw, _pitch, _distance, _target); } } }
	internal Key[] KeysHeld { get { lock (_keys) { return _keys.ToArray(); } } }

	public GlView()
	{
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

	private void Wake()
	{
		_wokeAt = _clock.ElapsedMilliseconds;
		RequestNextFrameRendering();
	}

	// ---------------------------------------------------------------- GL objects
	private uint _terrainProg, _objectProg, _waterProg;
	private uint _terrainVao, _terrainIndexCount, _waterVao;
	private bool _sceneDirty;

	private sealed class Batch
	{
		public uint Vao, InstanceVbo;
		public int IndexCount, Instances;
		public uint Texture;
		public ModelStore.MaterialData Material = null!;
	}
	private readonly List<Batch> _batches = new();
	private readonly Dictionary<string, (uint Vbo, uint[] Ebos, int[] Counts)> _meshGl = new();
	private readonly Dictionary<string, uint> _textures = new();
	// Models read on worker threads, waiting to go to the graphics card (on the drawing thread).
	private sealed record ReadyModel(ModelStore.Model? Model, List<Matrix4x4> Placements, Dictionary<string, ModelStore.MeshData> Meshes,
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
	private unsafe void BuildTerrain(WorldScene s)
	{
		int w = s.W, h = s.H;
		float[] v = new float[w * h * 9];
		for (int gz = 0; gz < h; gz++)
		{
			for (int gx = 0; gx < w; gx++)
			{
				int g = gz * w + gx, o = g * 9;
				float H(int x, int z) => s.Heights[Math.Clamp(z, 0, h - 1) * w + Math.Clamp(x, 0, w - 1)];
				var n = Vector3.Normalize(new Vector3(H(gx - 1, gz) - H(gx + 1, gz), 2f, -(H(gx, gz - 1) - H(gx, gz + 1))));
				var c = Shaders.BiomeColor(s.Biomes[g]);
				v[o] = gx - (w - 1) / 2f; v[o + 1] = s.Heights[g]; v[o + 2] = -(gz - (h - 1) / 2f);
				v[o + 3] = n.X; v[o + 4] = n.Y; v[o + 5] = n.Z;
				v[o + 6] = c.X; v[o + 7] = c.Y; v[o + 8] = c.Z;
			}
		}
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
		uint vbo = _gl.GenBuffer();
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
		float[] extra = new float[w * h * 4];
		for (int g = 0; g < w * h; g++)
		{
			extra[g * 4] = (g % w + 0.5f) / w;
			extra[g * 4 + 1] = (g / w + 0.5f) / h;
			extra[g * 4 + 2] = s.OceanDepth[g];
			extra[g * 4 + 3] = s.Limit[g];
		}
		uint ex = _gl.GenBuffer();
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
	private void StartModels(WorldScene s)
	{
		var byPrefab = s.Things.GroupBy(t => t.Prefab).ToList();
		_pending = byPrefab.Count;
		Task.Run(() => Parallel.ForEach(byPrefab, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, group =>
		{
			try
			{
				string? name = _models?.NameOf(group.Key);
				var model = name != null ? _models!.LoadModel(name) : null;
				var meshes = new Dictionary<string, ModelStore.MeshData>();
				var mats = new Dictionary<string, ModelStore.MaterialData>();
				var images = new Dictionary<string, ModelStore.ImageData?>();
				if (model != null)
				{
					foreach (var part in model.Parts)
					{
						if (!meshes.ContainsKey(part.Mesh) && _models!.LoadMesh(part.Mesh) is { } md)
						{
							meshes[part.Mesh] = md;
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
				var root = model?.RootScale ?? Vector3.One;
				var placements = group.Select(t => Placement(s, t, root)).ToList();
				_ready.Enqueue(new ReadyModel(model, placements, meshes, mats, images));
			}
			catch (Exception ex)
			{
				Status?.Invoke($"Model of {group.Key}: {ex.Message}");
				Interlocked.Decrement(ref _pending);
			}
		}));
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
		if (r.Model == null)
		{
			// No model copied for this kind: a box stands in, as in the web editor.
			AddBatch(BoxMesh(), 0, new ModelStore.MaterialData(new Vector4(0.35f, 0.3f, 0.25f, 1), null, 0, false, new Vector4(1, 1, 0, 0)),
				r.Placements.Select(p => Matrix4x4.CreateScale(1, 2, 1) * Matrix4x4.CreateTranslation(0, 1, 0) * p).ToList());
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
				AddBatch(mesh, part.Sub, r.Materials[part.Material], r.Placements.Select(p => part.Matrix * p).ToList());
			}
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

	private unsafe void AddBatch((uint Vbo, uint[] Ebos, int[] Counts) mesh, int sub, ModelStore.MaterialData mat, List<Matrix4x4> instances)
	{
		var b = new Batch { Material = mat, IndexCount = mesh.Counts[sub], Instances = instances.Count };
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
		var data = instances.ToArray();
		fixed (Matrix4x4* p = data)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.StaticDraw);
		}
		for (uint c = 0; c < 4; c++)
		{
			_gl.EnableVertexAttribArray(3 + c);
			_gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(c * 16));
			_gl.VertexAttribDivisor(3 + c, 1);
		}
		_gl.BindVertexArray(0);
		_batches.Add(b);
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
		// A few models per frame, so the view keeps moving while they arrive.
		for (int i = 0; i < 12 && _ready.TryDequeue(out var r); i++)
		{
			Upload(r);
		}
		bool moving = MoveWithKeys((now - _lastFrame) / 1000f);
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
			eye = _target + _distance * new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
			view = Matrix4x4.CreateLookAt(eye, _target, Vector3.UnitY);
		}
		proj = Perspective(60 * MathF.PI / 180, pw / (float)ph, 0.5f, 6000);
		var vp = view * proj;
		bool camMoved = view != _lastView;
		_lastView = view;
		var sun = GameLookGl.SunDirView;
		float time = _clock.ElapsedMilliseconds / 1000f;
		if (s != null && _terrainVao != 0)
		{
			if (_look != null)
			{
				_look.DrawSky(vp, eye, time);
				_look.UseTerrain(vp, eye, time, slope: false, contour: 0);
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
		_gl.BindVertexArray(0);
		_gl.UseProgram(0);
		double work = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
		Measure(now, work, camMoved, pw, ph);
		Automate(now, pw, ph);
		// Full speed while something happens; the idle timer draws a few times a second otherwise.
		if (camMoved || moving || now - _wokeAt < 1000 || !_ready.IsEmpty || _pending > 0)
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
	private static Matrix4x4 Perspective(float fovY, float aspect, float near, float far)
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
		lock (_keys)
		{
			if (_keys.Contains(Key.W) || _keys.Contains(Key.Up)) d.Z -= 1;
			if (_keys.Contains(Key.S) || _keys.Contains(Key.Down)) d.Z += 1;
			if (_keys.Contains(Key.A) || _keys.Contains(Key.Left)) d.X -= 1;
			if (_keys.Contains(Key.D) || _keys.Contains(Key.Right)) d.X += 1;
		}
		if (d == Vector3.Zero || dt <= 0)
		{
			return false;
		}
		lock (_camLock)
		{
			float speed = Math.Max(10, _distance) * Math.Min(dt, 0.1f);
			float c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
			_target += new Vector3(d.X * c + d.Z * sn, 0, -d.X * sn + d.Z * c) * speed;
			FollowGround();
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
			_dragFrom = p.Position;
			_dragButton = p.Properties.PointerUpdateKind;
			e.Pointer.Capture(surface);
			Wake();
		};
		surface.PointerReleased += (_, e) =>
		{
			_dragFrom = null;
			e.Pointer.Capture(null);
			Wake();
		};
		surface.PointerMoved += (_, e) => Drag(e.GetPosition(surface));
		surface.PointerWheelChanged += (_, e) =>
		{
			lock (_camLock)
			{
				_distance = Math.Clamp(_distance * MathF.Pow(0.88f, (float)e.Delta.Y), 3, 3000);
			}
			Wake();
		};
		window.KeyDown += (_, e) =>
		{
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
					_pitch = Math.Clamp(_pitch + dy * 0.005f, 0.05f, 1.55f);
				}
				else if (_dragButton is PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.LeftButtonPressed)
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
