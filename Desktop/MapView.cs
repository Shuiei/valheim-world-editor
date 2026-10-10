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
using TerrainEditor.Terrain;
using MapData = ValheimGen.MapData;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

// The world map, drawn like the game's (the web editor's mapview.js, with Valheim's own map shader):
// the whole world from the game's map data, and a 1 m close-up that includes the edits when zoomed in.
// Drag to slide, wheel to zoom (toward the mouse), click to pick a spot. Lines on top: the chosen
// area, edited zones, zones marked for reset, and the zone grid.
public sealed class MapView : OpenGlControlBase
{
	private GL _gl = null!;
	// Every OpenGL object the map made, deleted with its context (see GlObjects).
	private GlObjects _own = null!;
	private bool _es;
	private uint _prog, _vao, _lineProg, _lineVao, _lineVbo, _fillProg, _fillVao, _fillVbo;
	private readonly Dictionary<string, (uint Tex, int Unit)> _tex = new();
	private MapData? _data;
	private WorldSession? _session;
	private volatile MapData.Layers? _global;
	private (MapData.Layers Layers, int X0, int Z0, int Size, int Version)? _detail, _detailShown;
	private bool _globalShown, _texturesLoaded;
	private readonly Stopwatch _clock = Stopwatch.StartNew();

	// The view: the world point at the middle, and metres per pixel (screen pixels, not device ones).
	public Vector2 Center { get; set; }
	public float MetersPerPixel { get; set; } = 12;
	public bool ShowGrid { get; set; }
	public bool ShowEdited { get; set; } = true;
	public bool ShowPaint { get; set; } = true;
	public bool ShowClouds { get; set; }
	// The area chosen to edit: its middle zone and size in zones (null: none).
	public (int X, int Z, int Size)? Chosen { get; set; }
	// Player-built pieces (PieceCatalog.Encode), read in the background; drawn as their footprints.
	public bool ShowBuildings { get; set; } = true;
	private volatile float[]? _pieces;
	private int[] _pieceOrder = Array.Empty<int>();
	public int PieceCount => (_pieces?.Length ?? 0) / PieceCatalog.Stride;
	public event Action? PiecesRead;
	// Live: the players online. Search results (the chosen one larger), and the zone filter's matches.
	public IReadOnlyList<(string Name, float X, float Z)> Players { get; set; } = Array.Empty<(string, float, float)>();
	public IReadOnlyList<Vector2> Pins { get; set; } = Array.Empty<Vector2>();
	public int PinChosen { get; set; } = -1;
	public IReadOnlyList<(int X, int Z)> Matches { get; set; } = Array.Empty<(int, int)>();
	// The view moved or zoomed (labels over the map follow).
	public event Action? ViewChanged;
	public event Action<float, float>? Picked;
	public event Action<float, float>? Hovered;
	public event Action<string>? Status;
	public bool Ready => _global != null;
	public static float MapMeters => MapData.GlobalSize * MapData.GlobalPixel;

	public MapView()
	{
		// Only while the map is shown (see GlView: a running timer keeps its view alive).
		var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
		idle.Tick += (_, _) => RequestNextFrameRendering();
		AttachedToVisualTree += (_, _) => idle.Start();
		DetachedFromVisualTree += (_, _) => idle.Stop();
		ClipToBounds = true;
	}

	// Shows a world (its map data is worked out in the background: a few seconds).
	public void Show(WorldSession session)
	{
		_session = session;
		_data = new MapData(session.Terrain, session.Edits);
		_global = null;
		_globalShown = false;
		_detail = null;
		_detailShown = null;
		ReadPieces();
		Status?.Invoke("Building the world map (same as the game does)…");
		var data = _data;
		Task.Run(() =>
		{
			var g = data.Global;
			if (_data == data)
			{
				_global = g;
				Dispatcher.UIThread.Post(() => { Status?.Invoke(""); RequestNextFrameRendering(); });
			}
		});
		RequestNextFrameRendering();
	}

	// The map data changed (edits saved, discarded): the close-up is read again.
	public void Refresh()
	{
		_detailShown = null;
		_detail = null;
		ReadPieces();
		RequestNextFrameRendering();
	}

	private void ReadPieces()
	{
		var s = _session;
		if (s == null)
		{
			return;
		}
		var world = s.World;
		var deleted = s.Edits.Deleted;
		Task.Run(() =>
		{
			float[] data = PieceCatalog.Encode(world, deleted);
			// Lower pieces first so roofs end up on top, like looking down from above.
			int[] order = Enumerable.Range(0, data.Length / PieceCatalog.Stride).OrderBy(i => data[i * PieceCatalog.Stride + 8]).ToArray();
			Dispatcher.UIThread.Post(() =>
			{
				if (_session != s)
				{
					return;
				}
				_pieceOrder = order;
				_pieces = data;
				_buildingsKey = default;
				PiecesRead?.Invoke();
				RequestNextFrameRendering();
			});
		});
	}

	// Where a world point is on the control (its own units, not device pixels).
	public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - Center.X) / MetersPerPixel, Bounds.Height / 2 - (z - Center.Y) / MetersPerPixel);

	protected override void OnOpenGlInit(GlInterface gl)
	{
		_gl = GL.GetApi(name => gl.GetProcAddress(name));
		_own = new GlObjects(_gl);
		_es = GlVersion.Type == GlProfileType.OpenGLES;
		_prog = Program(MapShader.Vertex, MapShader.Fragment);
		_vao = _own.VertexArray();
		_gl.BindVertexArray(_vao);
		uint vbo = _own.Buffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
		float[] tri = { -1, -1, 3, -1, -1, 3 };
		unsafe
		{
			fixed (float* p = tri)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(tri.Length * 4), p, BufferUsageARB.StaticDraw);
			}
			uint loc = (uint)_gl.GetAttribLocation(_prog, "pos");
			_gl.EnableVertexAttribArray(loc);
			_gl.VertexAttribPointer(loc, 2, VertexAttribPointerType.Float, false, 0, (void*)0);
		}
		_lineProg = Program(Shaders.LineVs, Shaders.LineFs);
		_lineVao = _own.VertexArray();
		_lineVbo = _own.Buffer();
		_gl.BindVertexArray(_lineVao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
		_gl.EnableVertexAttribArray(0);
		unsafe
		{
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
		}
		_fillProg = Program(FillVs, FillFs);
		_fillVao = _own.VertexArray();
		_fillVbo = _own.Buffer();
		_gl.BindVertexArray(_fillVao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _fillVbo);
		_gl.EnableVertexAttribArray(0);
		_gl.EnableVertexAttribArray(1);
		unsafe
		{
			_gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 24, (void*)0);
			_gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 24, (void*)8);
		}
		_gl.BindVertexArray(0);
	}

	// Shapes in world metres with a colour per corner (buildings, zone fills, pins, players).
	private const string FillVs = """
		layout(location = 0) in vec2 aPos;
		layout(location = 1) in vec4 aCol;
		uniform vec4 uView;
		out vec4 vCol;
		void main() { gl_Position = vec4((aPos - uView.xy) * uView.zw, 0.0, 1.0); vCol = aCol; }
		""";

	private const string FillFs = """
		in vec4 vCol;
		out vec4 frag;
		void main() { frag = vCol; }
		""";

	// The map left the window (the 3D editor is shown): its OpenGL context goes. Its textures, buffers
	// and programs are shared with Avalonia's own context and would outlive it: they are deleted, then
	// uploaded again into the next context.
	protected override void OnOpenGlDeinit(GlInterface gl)
	{
		_own.DeleteAll();
		_tex.Clear();
		_texturesLoaded = false;
		_globalShown = false;
		_detailShown = null;
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
		return _own.Program(p);
	}

	// A texture on its own unit, bound to the sampler of that name.
	private unsafe void Texture(string name, Action<uint> fill)
	{
		if (!_tex.TryGetValue(name, out var t))
		{
			t = (_own.Texture(), _tex.Count);
			_tex[name] = t;
		}
		_gl.ActiveTexture(TextureUnit.Texture0 + t.Unit);
		_gl.BindTexture(TextureTarget.Texture2D, t.Tex);
		fill(t.Tex);
		_gl.UseProgram(_prog);
		_gl.Uniform1(_gl.GetUniformLocation(_prog, name), t.Unit);
	}

	// Plain stand-ins (RGBA) when the game's map textures cannot be read (Valheim not found).
	private static readonly Dictionary<string, byte[]> Fallback = new()
	{
		["_BackgroundTex"] = new byte[] { 200, 186, 150, 255 }, ["_FogLayerTex"] = new byte[] { 255, 255, 255, 0 }, ["_WaterTex"] = new byte[] { 128, 128, 128, 255 },
		["_lavaTex"] = new byte[] { 128, 128, 128, 255 }, ["_MountainTex"] = new byte[] { 255, 255, 255, 255 }, ["_CloudTex"] = new byte[] { 0, 0, 0, 0 },
		["_ForestTex"] = new byte[] { 128, 128, 128, 0 }, ["_SpaceTex"] = new byte[] { 10, 10, 16, 255 },
	};

	private unsafe void LoadTextures()
	{
		_texturesLoaded = true;
		var pictures = new Dictionary<string, GameLookData.Picture>();
		try
		{
			if (GameLook.Bundles is { } game)
			{
				pictures = GameLookData.ReadMap(game);
			}
		}
		catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
		{
			Console.WriteLine($"map: the game's map textures could not be read: {e.Message}");
		}
		foreach (var (u, f) in new[] { ("Background", "background"), ("FogLayer", "foglayer"), ("Water", "water"), ("lava", "lava"), ("Mountain", "mountain"), ("Cloud", "cloud"), ("Forest", "forest"), ("Space", "space") })
		{
			string name = $"_{u}Tex";
			// The lava mask is data, every other map texture a colour.
			bool srgb = u != "lava";
			var bmp = pictures.GetValueOrDefault(f);
			Texture(name, _ =>
			{
				_gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
				if (bmp == null)
				{
					fixed (byte* p = Fallback[name])
					{
						_gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
					}
					_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					return;
				}
				// Top row first; the web uploaded the pictures as they are (no flip), so the same.
				fixed (byte* px = bmp.Rgba)
				{
					_gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)bmp.Width, (uint)bmp.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, px);
				}
				_gl.GenerateMipmap(TextureTarget.Texture2D);
				_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
				_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
				_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
				_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
			});
		}
		// Placeholders until a close-up is loaded.
		Upload("d", new MapData.Layers(16, 16));
	}

	private unsafe void Upload(string prefix, MapData.Layers l)
	{
		void Set(string name, InternalFormat internalFormat, PixelFormat format, PixelType type, void* data) => Texture(name, _ =>
		{
			_gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
			_gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, (uint)l.Width, (uint)l.Height, 0, format, type, data);
			_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
		});
		fixed (Half* h = l.Heights)
		fixed (byte* b = l.Biome)
		fixed (byte* m = l.Mask)
		fixed (byte* p = l.Paint)
		{
			Set(prefix + "Height", InternalFormat.R16f, PixelFormat.Red, PixelType.HalfFloat, h);
			Set(prefix + "Main", InternalFormat.Srgb8Alpha8, PixelFormat.Rgba, PixelType.UnsignedByte, b);
			Set(prefix + "Mask", InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, m);
			if (prefix == "d")
			{
				Set("dPaint", InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, p);
			}
		}
	}

	// Material values from the game's "minimap shader" material, and clear-midday lighting.
	private static readonly (string Name, float[] V)[] Material =
	{
		("_ForestColor", new[] { 0.9191f, 0.8218f, 0.6758f, 1 }), ("_WaterColor", new[] { 0.8429f, 0.9373f, 1.0917f, 1 }), ("_WaterColorDeep", new[] { 0.343f, 0.5337f, 0.7647f, 1 }),
		("_WaterColorAshlands", new[] { 0.2078f, 0.2078f, 0.2078f, 1 }), ("_WaterColorAshlandsDeep", new[] { 0.2235f, 0.251f, 0.3686f, 1 }),
		("_lightColor", new[] { 1.2f, 1.2f, 1.2f }), ("_ambientLightColor", new[] { 0.8088f, 0.8418f, 1.0f }), ("_lavaColor1", new[] { 0.4157f, 0.0235f, 0.0f }), ("_lavaColor2", new[] { 4.5126f, 0.2599f, 0.0f }),
		("_SunColor", new[] { 1.0f, 0.97f, 0.9f, 1 }), ("_AmbientColor", new[] { 0.36f, 0.38f, 0.44f, 1 }), ("_SunFogColor", new[] { 0.9f, 0.9f, 1, 1 }),
	};

	private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

	private double Scaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

	protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
	{
		var g = _global;
		double scaling = Scaling;
		int w = Math.Max(1, (int)(Bounds.Width * scaling)), h = Math.Max(1, (int)(Bounds.Height * scaling));
		_gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
		_gl.Viewport(0, 0, (uint)w, (uint)h);
		_gl.ClearColor(0.08f, 0.09f, 0.11f, 1);
		_gl.Clear(ClearBufferMask.ColorBufferBit);
		if (g == null)
		{
			return;
		}
		if (!_texturesLoaded)
		{
			LoadTextures();
		}
		if (!_globalShown)
		{
			Upload("g", g);
			_globalShown = true;
		}
		float mpp = MetersPerPixel / (float)scaling;
		AskDetail(mpp, w, h);
		if (_detail is { } dt && !_detailShown.Equals(_detail))
		{
			Upload("d", dt.Layers);
			_detailShown = _detail;
		}
		// The 3D editor draws in the same OpenGL context: its textures, depth test and culling are
		// still set when the map comes back, so the map sets its own every frame.
		_gl.Disable(EnableCap.DepthTest);
		_gl.Disable(EnableCap.CullFace);
		_gl.Disable(EnableCap.ScissorTest);
		_gl.DepthMask(true);
		_gl.ColorMask(true, true, true, true);
		_gl.UseProgram(_prog);
		foreach (var (name, (tex, unit)) in _tex)
		{
			_gl.ActiveTexture(TextureUnit.Texture0 + unit);
			_gl.BindTexture(TextureTarget.Texture2D, tex);
			_gl.Uniform1(_gl.GetUniformLocation(_prog, name), unit);
		}
		_gl.ActiveTexture(TextureUnit.Texture0);
		int U(string n) => _gl.GetUniformLocation(_prog, n);
		float time = _clock.ElapsedMilliseconds / 1000f;
		_gl.Uniform4(U("_Time"), time / 20, time, time * 2, time * 3);
		foreach (var (name, v) in Material)
		{
			int loc = U(name);
			if (loc < 0) continue;
			var c = v.Select((x, i) => i < 3 ? ToLinear(x) : x).ToArray();
			if (c.Length == 3) _gl.Uniform3(loc, c[0], c[1], c[2]); else _gl.Uniform4(loc, c[0], c[1], c[2], c[3]);
		}
		_gl.Uniform3(U("_SunDir"), -0.35f, 0.86f, 0.36f);
		_gl.Uniform1(U("_normalIntensity"), 4f);
		float mapMeters = MapMeters;
		var d = _detailShown;
		_gl.Uniform1(U("_zoom"), w * mpp / mapMeters);
		// The game quantizes to ~3.5 m map "pixels"; the close-up goes down to 1 m.
		_gl.Uniform1(U("_quant"), d != null && mpp < 3.5f ? mapMeters : 7000f);
		_gl.Uniform1(U("normalWidthM"), d != null && mpp < 6 ? MathF.Max(1, mpp) : MapData.GlobalPixel * 2.048f);
		_gl.Uniform3(U("_mapCenter"), Center.X, 0, Center.Y);
		_gl.Uniform3(U("_CloudOffset"), time * 0.0015f, 0, time * 0.001f);
		_gl.Uniform2(U("viewCenter"), Center.X, Center.Y);
		_gl.Uniform2(U("canvasSize"), (float)w, h);
		_gl.Uniform1(U("metersPerPixel"), mpp);
		_gl.Uniform1(U("mapMeters"), mapMeters);
		// Floats (a vec4): whole numbers would pick glUniform4i, which a float uniform refuses.
		_gl.Uniform4(U("detailRect"), (float)(d?.X0 ?? 0), (float)(d?.Z0 ?? 0), (float)(d?.Size ?? 1), d != null ? 1f : 0f);
		_gl.Uniform1(U("showPaint"), ShowPaint ? 1f : 0f);
		_gl.Uniform1(U("showClouds"), ShowClouds ? 1f : 0f);
		_gl.BindVertexArray(_vao);
		_gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
		_gl.UseProgram(_fillProg);
		_gl.Uniform4(_gl.GetUniformLocation(_fillProg, "uView"), Center.X, Center.Y, 2 / (w * mpp), 2 / (h * mpp));
		DrawBuildings();
		DrawZoneFills();
		DrawLines(w, h, mpp);
		_gl.Enable(EnableCap.Blend);
		_gl.UseProgram(_fillProg);
		DrawPins();
		_gl.Disable(EnableCap.Blend);
		_gl.BindVertexArray(0);
		_gl.UseProgram(0);
		_drawn++;
		if (Options.Driver)
		{
			for (var e = _gl.GetError(); e != GLEnum.NoError; e = _gl.GetError())
			{
				GlErrors++;
				Options.Say($"OpenGL error (map) {e}");
			}
		}
		// The test driver's pictures: once the world map is in and a few frames are drawn.
		// Zoomed in, the 1 m close-up is read in the background: the picture waits for it.
		if (_picture is { } req && _drawn > 10 && (mpp >= 6 || (_detailShown != null && _detail == _detailShown && !_asking)))
		{
			_picture = null;
			try
			{
				GlPicture.Save(_gl, w, h, req.Path);
				req.Done.SetResult();
			}
			catch (Exception ex)
			{
				req.Done.SetException(ex);
			}
		}
	}

	// Close up (under 6 m a pixel): the ground in view at 1 m, with the edits, read in the background.
	private bool _asking;
	private int _drawn;
	private volatile GlPicture.Request? _picture;

	internal Task Picture(string path)
	{
		var r = new GlPicture.Request { Path = path };
		_picture = r;
		RequestNextFrameRendering();
		return r.Done.Task;
	}

	internal int FramesDrawn => _drawn;

	// The 1 m close-up is drawn (zoomed in, once read).
	internal bool DetailShown => _detailShown != null;

	// The test driver checks drawing raised no OpenGL error (counted only when driven).
	internal int GlErrors { get; private set; }
	private void AskDetail(float mpp, int w, int h)
	{
		if (_data == null || _session == null || mpp >= 6 || _asking)
		{
			return;
		}
		int size = Math.Clamp((int)MathF.Ceiling(MathF.Max(w, h) * mpp * 1.3f / 64) * 64, 64, 2048);
		int x0 = (int)MathF.Floor((Center.X - size / 2f) / 64) * 64, z0 = (int)MathF.Floor((Center.Y - size / 2f) / 64) * 64;
		int version = _session.Edits.Version;
		if (_detail is { } d && d.X0 <= Center.X - w * mpp / 2 && d.Z0 <= Center.Y - h * mpp / 2 && d.X0 + d.Size >= Center.X + w * mpp / 2 && d.Z0 + d.Size >= Center.Y + h * mpp / 2 && d.Version == version)
		{
			return;
		}
		_asking = true;
		var data = _data;
		Task.Run(() =>
		{
			try
			{
				var layers = data.Detail(x0, z0, size, version);
				if (_data == data)
				{
					_detail = (layers, x0, z0, size, version);
				}
			}
			finally
			{
				_asking = false;
				Dispatcher.UIThread.Post(RequestNextFrameRendering);
			}
		});
	}

	// ---- Shapes over the map in world metres (one colour per corner, drawn in order).
	private unsafe void Fill(float[] data, PrimitiveType type)
	{
		if (data.Length == 0)
		{
			return;
		}
		_gl.BindVertexArray(_fillVao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _fillVbo);
		fixed (float* p = data)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.StreamDraw);
		}
		_gl.DrawArrays(type, 0, (uint)(data.Length / 6));
	}

	private static void Quad(List<float> to, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 col)
	{
		foreach (var p in new[] { a, b, c, a, c, d })
		{
			to.AddRange(new[] { p.X, p.Y, col.X, col.Y, col.Z, col.W });
		}
	}

	private static void Disc(List<float> to, float x, float z, float r, Vector4 col, int sides = 12)
	{
		for (int i = 0; i < sides; i++)
		{
			float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
			to.AddRange(new[] { x, z, col.X, col.Y, col.Z, col.W });
			to.AddRange(new[] { x + r * MathF.Cos(a0), z + r * MathF.Sin(a0), col.X, col.Y, col.Z, col.W });
			to.AddRange(new[] { x + r * MathF.Cos(a1), z + r * MathF.Sin(a1), col.X, col.Y, col.Z, col.W });
		}
	}

	// The web map's piece colours per category (fill, edge): misc, crafting, wood, stone, furniture.
	private static readonly (Vector4 Fill, Vector4 Edge)[] PieceStyle =
	{
		(new(150 / 255f, 72 / 255f, 34 / 255f, 0.92f), new(60 / 255f, 25 / 255f, 10 / 255f, 0.9f)),
		(new(128 / 255f, 56 / 255f, 40 / 255f, 0.9f), new(55 / 255f, 22 / 255f, 14 / 255f, 0.9f)),
		(new(92 / 255f, 64 / 255f, 40 / 255f, 0.88f), new(38 / 255f, 24 / 255f, 14 / 255f, 0.9f)),
		(new(110 / 255f, 104 / 255f, 96 / 255f, 0.92f), new(45 / 255f, 42 / 255f, 38 / 255f, 0.9f)),
		(new(140 / 255f, 102 / 255f, 62 / 255f, 0.8f), new(60 / 255f, 40 / 255f, 22 / 255f, 0.85f)),
	};

	// Built once per zoom kind: footprints close up (edges very close), dots of two pixels further out.
	private (float[]? Pieces, int Kind, float Mpp) _buildingsKey;
	private float[] _buildingFill = Array.Empty<float>(), _buildingEdge = Array.Empty<float>();

	private void DrawBuildings()
	{
		float[]? pc = _pieces;
		float mpp = MetersPerPixel;
		if (pc == null || !ShowBuildings || mpp > 10)
		{
			return;
		}
		int kind = mpp < 0.8f ? 2 : mpp < 2.5f ? 1 : 0;
		if (_buildingsKey.Pieces != pc || _buildingsKey.Kind != kind || (kind == 0 && _buildingsKey.Mpp != mpp))
		{
			_buildingsKey = (pc, kind, mpp);
			var fill = new List<float>();
			var edge = new List<float>();
			const int S = PieceCatalog.Stride;
			foreach (int i in _pieceOrder)
			{
				int o = i * S;
				float px = pc[o], pz = pc[o + 2];
				var style = PieceStyle[Math.Clamp((int)pc[o + 9], 0, PieceStyle.Length - 1)];
				if (kind == 0)
				{
					float r = mpp;
					Quad(fill, new(px - r, pz - r), new(px + r, pz - r), new(px + r, pz + r), new(px - r, pz + r), style.Edge);
					continue;
				}
				// Unity Y rotation (clockwise seen from above): local (x, z) to world.
				float a = pc[o + 3] * MathF.PI / 180, c = MathF.Cos(a), sn = MathF.Sin(a);
				Vector2 W(float lx, float lz) => new(px + lx * c + lz * sn, pz - lx * sn + lz * c);
				Vector2 p0 = W(pc[o + 4], pc[o + 6]), p1 = W(pc[o + 5], pc[o + 6]), p2 = W(pc[o + 5], pc[o + 7]), p3 = W(pc[o + 4], pc[o + 7]);
				Quad(fill, p0, p1, p2, p3, style.Fill);
				if (kind == 2)
				{
					foreach (var (p, q) in new[] { (p0, p1), (p1, p2), (p2, p3), (p3, p0) })
					{
						edge.AddRange(new[] { p.X, p.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W, q.X, q.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W });
					}
				}
			}
			_buildingFill = fill.ToArray();
			_buildingEdge = edge.ToArray();
		}
		Fill(_buildingFill, PrimitiveType.Triangles);
		Fill(_buildingEdge, PrimitiveType.Lines);
	}

	// The zone filter's matches (blue) and the zones marked for reset (red).
	private void DrawZoneFills()
	{
		var s = _session;
		if (s == null)
		{
			return;
		}
		var marked = s.Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
		var data = new List<float>();
		void Zone(int x, int z, Vector4 col) => Quad(data, new(x * 64 - 32, z * 64 - 32), new(x * 64 + 32, z * 64 - 32), new(x * 64 + 32, z * 64 + 32), new(x * 64 - 32, z * 64 + 32), col);
		foreach (var (x, z) in Matches)
		{
			if (!marked.Contains((x, z)))
			{
				Zone(x, z, new Vector4(70 / 255f, 160 / 255f, 1, 0.35f));
			}
		}
		foreach (var (x, z) in marked)
		{
			Zone(x, z, new Vector4(1, 70 / 255f, 50 / 255f, 0.45f));
		}
		Fill(data.ToArray(), PrimitiveType.Triangles);
	}

	// Search results (orange pins, the chosen one larger with a ring) and, live, the players (blue).
	private void DrawPins()
	{
		float m = MetersPerPixel;
		var data = new List<float>();
		var dark = new Vector4(30 / 255f, 15 / 255f, 5 / 255f, 0.9f);
		for (int i = 0; i < Pins.Count; i++)
		{
			if (i == PinChosen)
			{
				continue;
			}
			Disc(data, Pins[i].X, Pins[i].Y, 4.5f * m, dark, 8);
			Disc(data, Pins[i].X, Pins[i].Y, 3.5f * m, new Vector4(1, 138 / 255f, 42 / 255f, 1), 8);
		}
		if (PinChosen >= 0 && PinChosen < Pins.Count)
		{
			var p = Pins[PinChosen];
			var gold = new Vector4(1, 210 / 255f, 122 / 255f, 1);
			for (int i = 0; i < 24; i++)
			{
				// The ring: a thin band of quads.
				float a0 = i * MathF.Tau / 24, a1 = (i + 1) * MathF.Tau / 24, r0 = 12.5f * m, r1 = 14 * m;
				Quad(data, new(p.X + r0 * MathF.Cos(a0), p.Y + r0 * MathF.Sin(a0)), new(p.X + r1 * MathF.Cos(a0), p.Y + r1 * MathF.Sin(a0)),
					new(p.X + r1 * MathF.Cos(a1), p.Y + r1 * MathF.Sin(a1)), new(p.X + r0 * MathF.Cos(a1), p.Y + r0 * MathF.Sin(a1)), gold);
			}
			Disc(data, p.X, p.Y, 7.5f * m, dark);
			Disc(data, p.X, p.Y, 6 * m, gold);
		}
		foreach (var (_, x, z) in Players)
		{
			Disc(data, x, z, 8 * m, new Vector4(11 / 255f, 32 / 255f, 48 / 255f, 1));
			Disc(data, x, z, 6 * m, new Vector4(79 / 255f, 195 / 255f, 1, 1));
		}
		Fill(data.ToArray(), PrimitiveType.Triangles);
	}

	// ---- Lines over the map (in screen pixels): the chosen area, edited zones, resets, the grid.
	private unsafe void DrawLines(int w, int h, float mpp)
	{
		var s = _session;
		if (s == null)
		{
			return;
		}
		var groups = new List<(float[] Data, Vector4 Color)>();
		// World metres → clip space.
		Vector2 C(float x, float z) => new((x - Center.X) / mpp / (w / 2f), (z - Center.Y) / mpp / (h / 2f));
		void Box(List<float> to, float x0, float z0, float x1, float z1)
		{
			var a = C(x0, z0); var b = C(x1, z0); var c = C(x1, z1); var d = C(x0, z1);
			foreach (var (p, q) in new[] { (a, b), (b, c), (c, d), (d, a) })
			{
				to.AddRange(new[] { p.X, p.Y, 0, q.X, q.Y, 0 });
			}
		}
		float halfW = w / 2f * mpp, halfH = h / 2f * mpp;
		if (ShowGrid && mpp < 8)
		{
			var grid = new List<float>();
			for (float x = MathF.Floor((Center.X - halfW + 32) / 64) * 64 - 32; x <= Center.X + halfW; x += 64)
			{
				var a = C(x, Center.Y - halfH); var b = C(x, Center.Y + halfH);
				grid.AddRange(new[] { a.X, a.Y, 0, b.X, b.Y, 0 });
			}
			for (float z = MathF.Floor((Center.Y - halfH + 32) / 64) * 64 - 32; z <= Center.Y + halfH; z += 64)
			{
				var a = C(Center.X - halfW, z); var b = C(Center.X + halfW, z);
				grid.AddRange(new[] { a.X, a.Y, 0, b.X, b.Y, 0 });
			}
			groups.Add((grid.ToArray(), new Vector4(1, 1, 1, 0.25f)));
		}
		if (ShowEdited)
		{
			var edited = new List<float>();
			var changed = new List<float>();
			foreach (var e in s.Edits.All().Where(e => e.HeightCount + e.PaintCount > 0 || e.Changed))
			{
				Box(e.Changed ? changed : edited, e.ZoneX * 64 - 32, e.ZoneZ * 64 - 32, e.ZoneX * 64 + 32, e.ZoneZ * 64 + 32);
			}
			groups.Add((edited.ToArray(), new Vector4(0.9f, 0.85f, 0.4f, 0.7f)));
			groups.Add((changed.ToArray(), new Vector4(1, 0.6f, 0.2f, 1)));
		}
		var resets = new List<float>();
		foreach (var r in s.Edits.Resets)
		{
			Box(resets, r.X * 64 - 32, r.Z * 64 - 32, r.X * 64 + 32, r.Z * 64 + 32);
		}
		groups.Add((resets.ToArray(), new Vector4(1, 0.31f, 0.25f, 1)));
		if (Chosen is var (cx, cz, size))
		{
			var area = new List<float>();
			int x0 = cx - size / 2, z0 = cz - size / 2;
			Box(area, x0 * 64 - 32, z0 * 64 - 32, (x0 + size) * 64 - 32, (z0 + size) * 64 - 32);
			groups.Add((area.ToArray(), new Vector4(0.37f, 0.83f, 1, 1)));
		}
		var id = Matrix4x4.Identity;
		_gl.UseProgram(_lineProg);
		_gl.UniformMatrix4(_gl.GetUniformLocation(_lineProg, "uViewProj"), 1, false, (float*)&id);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
		_gl.BindVertexArray(_lineVao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
		foreach (var (data, col) in groups.Where(gr => gr.Data.Length > 0))
		{
			fixed (float* p = data)
			{
				_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
			}
			_gl.Uniform4(_gl.GetUniformLocation(_lineProg, "uColor"), col.X, col.Y, col.Z, col.W);
			_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(data.Length / 3));
		}
		_gl.Disable(EnableCap.Blend);
	}

	// ---- The mouse.
	private Point? _press, _last;

	// The world point (x east, z north) under a point of the control.
	public Vector2 WorldAt(Point p) => new(Center.X + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, Center.Y - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

	// The OpenGL picture cannot be hit by the pointer (like the 3D view): a transparent surface laid
	// over it takes the mouse and hands it here.
	public void Attach(Control surface)
	{
		surface.PointerPressed += (_, e) =>
		{
			_press = _last = e.GetPosition(surface);
			e.Pointer.Capture(surface);
		};
		surface.PointerMoved += (_, e) =>
		{
			var p = e.GetPosition(surface);
			var w = WorldAt(p);
			Hovered?.Invoke(w.X, w.Y);
			if (_last is { } l)
			{
				Center += new Vector2(-(float)(p.X - l.X) * MetersPerPixel, (float)(p.Y - l.Y) * MetersPerPixel);
				_last = p;
				RequestNextFrameRendering();
				ViewChanged?.Invoke();
			}
		};
		surface.PointerReleased += (_, e) =>
		{
			var p = e.GetPosition(surface);
			if (_press is { } from && Point.Distance(from, p) < 5 && e.InitialPressMouseButton == MouseButton.Left)
			{
				var w = WorldAt(p);
				Picked?.Invoke(w.X, w.Y);
			}
			_press = _last = null;
			e.Pointer.Capture(null);
			RequestNextFrameRendering();
		};
		surface.PointerWheelChanged += (_, e) =>
		{
			// Toward the mouse: the point under it stays there.
			var p = e.GetPosition(surface);
			var before = WorldAt(p);
			MetersPerPixel = Math.Clamp(MetersPerPixel * MathF.Pow(0.8f, (float)e.Delta.Y), 0.25f, 40f);
			Center += before - WorldAt(p);
			RequestNextFrameRendering();
			ViewChanged?.Invoke();
			e.Handled = true;
		};
	}

	// Looks at a zone, close enough to see it.
	public void LookAt(float x, float z, float metersPerPixel = 2)
	{
		Center = new Vector2(x, z);
		MetersPerPixel = metersPerPixel;
		RequestNextFrameRendering();
		ViewChanged?.Invoke();
	}
}
