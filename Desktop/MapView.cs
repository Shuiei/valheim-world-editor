using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using SkiaSharp;
using TerrainEditor.App;
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
	private bool _es;
	private uint _prog, _vao, _lineProg, _lineVao, _lineVbo;
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
	public event Action<float, float>? Picked;
	public event Action<float, float>? Hovered;
	public event Action<string>? Status;
	public bool Ready => _global != null;
	public static float MapMeters => MapData.GlobalSize * MapData.GlobalPixel;

	public MapView()
	{
		var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
		idle.Tick += (_, _) => RequestNextFrameRendering();
		idle.Start();
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
		RequestNextFrameRendering();
	}

	protected override void OnOpenGlInit(GlInterface gli)
	{
		_gl = GL.GetApi(name => gli.GetProcAddress(name));
		_es = GlVersion.Type == GlProfileType.OpenGLES;
		_prog = Program(MapShader.Vertex, MapShader.Fragment);
		_vao = _gl.GenVertexArray();
		_gl.BindVertexArray(_vao);
		uint vbo = _gl.GenBuffer();
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
		_lineVao = _gl.GenVertexArray();
		_lineVbo = _gl.GenBuffer();
		_gl.BindVertexArray(_lineVao);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
		_gl.EnableVertexAttribArray(0);
		unsafe
		{
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
		}
		_gl.BindVertexArray(0);
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
		_gl.AttachShader(p, Compile(ShaderType.VertexShader, vs));
		_gl.AttachShader(p, Compile(ShaderType.FragmentShader, fs));
		_gl.LinkProgram(p);
		_gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
		if (linked == 0)
		{
			throw new InvalidOperationException(_gl.GetProgramInfoLog(p));
		}
		return p;
	}

	// A texture on its own unit, bound to the sampler of that name.
	private unsafe void Texture(string name, Action<uint> fill)
	{
		if (!_tex.TryGetValue(name, out var t))
		{
			t = (_gl.GenTexture(), _tex.Count);
			_tex[name] = t;
		}
		_gl.ActiveTexture(TextureUnit.Texture0 + t.Unit);
		_gl.BindTexture(TextureTarget.Texture2D, t.Tex);
		fill(t.Tex);
		_gl.UseProgram(_prog);
		_gl.Uniform1(_gl.GetUniformLocation(_prog, name), t.Unit);
	}

	// Plain stand-ins (RGBA) when the game's map textures were not copied yet.
	private static readonly Dictionary<string, byte[]> Fallback = new()
	{
		["_BackgroundTex"] = new byte[] { 200, 186, 150, 255 }, ["_FogLayerTex"] = new byte[] { 255, 255, 255, 0 }, ["_WaterTex"] = new byte[] { 128, 128, 128, 255 },
		["_lavaTex"] = new byte[] { 128, 128, 128, 255 }, ["_MountainTex"] = new byte[] { 255, 255, 255, 255 }, ["_CloudTex"] = new byte[] { 0, 0, 0, 0 },
		["_ForestTex"] = new byte[] { 128, 128, 128, 0 }, ["_SpaceTex"] = new byte[] { 10, 10, 16, 255 },
	};

	private unsafe void LoadTextures()
	{
		_texturesLoaded = true;
		foreach (var (u, f) in new[] { ("Background", "background"), ("FogLayer", "foglayer"), ("Water", "water"), ("lava", "lava"), ("Mountain", "mountain"), ("Cloud", "cloud"), ("Forest", "forest"), ("Space", "space") })
		{
			string name = $"_{u}Tex", path = Path.Combine(GameLook.Dir, "maptex", f + ".png");
			// The lava mask is data, every other map texture a colour.
			bool srgb = u != "lava";
			using var bmp = File.Exists(path) ? SKBitmap.Decode(path)?.Copy(SKColorType.Rgba8888) : null;
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
				// Pictures are stored top row first; the web uploads them as they are (no flip), so do the same.
				_gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)bmp.Width, (uint)bmp.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)bmp.GetPixels());
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

	protected override unsafe void OnOpenGlRender(GlInterface gli, int fb)
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
		_gl.UseProgram(_prog);
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
		_gl.Uniform4(U("detailRect"), d?.X0 ?? 0, d?.Z0 ?? 0, d?.Size ?? 1, d != null ? 1 : 0);
		_gl.Uniform1(U("showPaint"), ShowPaint ? 1f : 0f);
		_gl.Uniform1(U("showClouds"), ShowClouds ? 1f : 0f);
		_gl.BindVertexArray(_vao);
		_gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
		DrawLines(w, h, mpp);
		_gl.BindVertexArray(0);
		_gl.UseProgram(0);
		// --map with --shot (and no --map-edit): a picture of the map once it is drawn.
		if (Options.Shot is string shot && Options.MapEdit == null && ++_shotFrames == 30)
		{
			byte[] px = new byte[w * h * 4];
			fixed (byte* p = px)
			{
				_gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
			}
			using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
			for (int y = 0; y < h; y++)
			{
				System.Runtime.InteropServices.Marshal.Copy(px, (h - 1 - y) * w * 4, bmp.GetPixels() + y * w * 4, w * 4);
			}
			using (var f = File.Create(shot))
			{
				bmp.Encode(f, SKEncodedImageFormat.Png, 90);
			}
			Options.Say($"picture: {shot}");
			Dispatcher.UIThread.Post(() => (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
		}
	}

	// Close up (under 6 m a pixel): the ground in view at 1 m, with the edits, read in the background.
	private bool _asking;
	private int _shotFrames;
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
			e.Handled = true;
		};
	}

	// Looks at a zone, close enough to see it.
	public void LookAt(float x, float z, float metersPerPixel = 2)
	{
		Center = new Vector2(x, z);
		MetersPerPixel = metersPerPixel;
		RequestNextFrameRendering();
	}
}
