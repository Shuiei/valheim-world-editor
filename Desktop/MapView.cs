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

/// <summary>
/// The world map, drawn like the game's (the web editor's mapview.js, with Valheim's own map shader): the whole world
/// from the game's map data, and a 1 m close-up that includes the edits when zoomed in. Drag to slide, wheel to zoom
/// (toward the mouse), click to pick a spot. Lines on top: the chosen area, edited zones, zones marked for reset, and
/// the zone grid.
/// </summary>
public sealed class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// The OpenGL functions of the current context.
    /// </summary>
    private GL _gl = null!;
    /// <summary>
    /// Every OpenGL object the map made, deleted with its context (see GlObjects).
    /// </summary>
    private GlObjects _own = null!;
    /// <summary>
    /// Whether the context is OpenGL ES (it needs another version line on the shaders).
    /// </summary>
    private bool _es;
    /// <summary>
    /// The map shader's, the overlay lines' and the overlay fills' programs, vertex arrays and buffers.
    /// </summary>
    private uint _prog, _vao, _lineProg, _lineVao, _lineVbo, _fillProg, _fillVao, _fillVbo;
    /// <summary>
    /// The map shader's textures by sampler name, each on its own texture unit.
    /// </summary>
    private readonly Dictionary<string, (uint Tex, int Unit)> _tex = [];
    /// <summary>
    /// The open world's map data (made from its terrain and edits), or null before a world is shown.
    /// </summary>
    private MapData? _data;
    /// <summary>
    /// Stops the world map's build when another world is shown.
    /// </summary>
    private CancellationTokenSource? _build;
    /// <summary>
    /// The world shown, or null before one is.
    /// </summary>
    private WorldSession? _session;
    /// <summary>
    /// The whole world's map layers, set by a worker thread once worked out; null until then.
    /// </summary>
    private volatile MapData.Layers? _global;
    /// <summary>
    /// The latest 1 m close-up read (with its corner, size in metres and edit version), and the one uploaded to the
    /// textures.
    /// </summary>
    private (MapData.Layers Layers, int X0, int Z0, int Size, int Version)? _detail, _detailShown;

    /// <summary>
    /// The last close-up whose reading failed (logged): not asked for again until the view, the edits or the map's
    /// data change, so a failure that would repeat is not retried on every frame.
    /// </summary>
    private (int X0, int Z0, int Size, int Version)? _detailFailed;
    /// <summary>
    /// Whether the whole-world layers, and the game's map textures, are uploaded into the current context.
    /// </summary>
    private bool _globalShown, _texturesLoaded;
    /// <summary>
    /// Time since the map was made, for the shader's moving water, lava and clouds.
    /// </summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// The view: the world point at the middle (x east, y north).
    /// </summary>
    public Vector2 Center { get; set; }
    /// <summary>
    /// Metres per pixel (screen pixels, not device ones); the wheel keeps it within 0.25..40.
    /// </summary>
    public float MetersPerPixel { get; set; } = 12;
    /// <summary>
    /// Draw the zone grid (zoomed in under 8 m a pixel).
    /// </summary>
    public bool ShowGrid { get; set; }
    /// <summary>
    /// Outline the zones with ground or paint edits, and those changed since the world was loaded.
    /// </summary>
    public bool ShowEdited { get; set; } = true;
    /// <summary>
    /// Tint painted ground (dirt, cultivated, paved) in the close-up.
    /// </summary>
    public bool ShowPaint { get; set; } = true;
    /// <summary>
    /// Draw the game's map clouds.
    /// </summary>
    public bool ShowClouds { get; set; }
    /// <summary>
    /// The area chosen to edit: its middle zone and size in zones (null: none).
    /// </summary>
    public (int X, int Z, int Size)? Chosen { get; set; }
    /// <summary>
    /// Draw player-built pieces, and the ruins' pieces joined to them, as their footprints (dots further out; none
    /// beyond 10 m a pixel).
    /// </summary>
    public bool ShowBuildings { get; set; } = true;
    /// <summary>
    /// Player-built pieces (PieceCatalog.Encode), read in the background: <c>PieceCatalog.Stride</c> floats per piece.
    /// </summary>
    private volatile float[]? _pieces;
    /// <summary>
    /// Draw the ruins' pieces too (builder-less pieces generated with the world: spoilers), fainter than buildings.
    /// The ones joined to a player's building are drawn with it (<c>ShowBuildings</c>) whatever this is.
    /// </summary>
    public bool ShowRuins { get; set; }
    /// <summary>
    /// The ruins' pieces (PieceCatalog.EncodeRuins), read with the player-built ones; same layout.
    /// </summary>
    private volatile float[]? _ruins;
    /// <summary>
    /// For each ruin piece, whether it is joined to a player's building (JoinedRuins).
    /// </summary>
    private bool[] _ruinJoined = [];
    /// <summary>
    /// Player-built and ruin piece indices, lowest top first, so roofs are drawn over what is under them.
    /// </summary>
    private (bool Ruin, int Index)[] _pieceOrder = [];
    /// <summary>
    /// How many player-built pieces were read.
    /// </summary>
    public int PieceCount => (_pieces?.Length ?? 0) / PieceCatalog.Stride;
    /// <summary>
    /// Raised on the UI thread when the player-built pieces have been read.
    /// </summary>
    public event Action? PiecesRead;
    /// <summary>
    /// Live: the players online, by name and world position. Kept as a copy of the list given, so changing that list
    /// afterwards changes nothing (the pins' shapes are made again only when a new one is given).
    /// </summary>
    public IReadOnlyList<(string Name, float X, float Z)> Players { get; set => field = [.. value]; } = [];
    /// <summary>
    /// Search results, as world points (x east, y north). Kept as a copy of the list given, like <see cref="Players"/>.
    /// </summary>
    public IReadOnlyList<Vector2> Pins { get; set => field = [.. value]; } = [];
    /// <summary>
    /// The chosen search result (drawn larger, with a ring), or -1 for none.
    /// </summary>
    public int PinChosen { get; set; } = -1;
    /// <summary>
    /// The zone filter's matching zones, filled in blue.
    /// </summary>
    public IReadOnlyList<(int X, int Z)> Matches { get; set; } = [];
    /// <summary>
    /// The view moved or zoomed (labels over the map follow).
    /// </summary>
    public event Action? ViewChanged;
    /// <summary>
    /// A left click (not a drag) on the map, with the world point clicked (x east, z north).
    /// </summary>
    public event Action<float, float>? Picked;
    /// <summary>
    /// The mouse moved over the map, with the world point under it.
    /// </summary>
    public event Action<float, float>? Hovered;
    /// <summary>
    /// A line for the status bar; empty once the work it told of is done.
    /// </summary>
    public event Action<string>? Status;
    /// <summary>
    /// Whether the whole-world map has been worked out.
    /// </summary>
    public bool Ready => _global != null;
    /// <summary>
    /// The world map's side in metres.
    /// </summary>
    public static float MapMeters => MapData.GlobalSize * MapData.GlobalPixel;

    /// <summary>
    /// A map that redraws ten times a second while it is in the window (for the shader's moving water and clouds).
    /// </summary>
    public MapView()
    {
        // Only while the map is shown (see GlView: a running timer keeps its view alive).
        var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        idle.Tick += (_, _) => RequestNextFrameRendering();
        AttachedToVisualTree += (_, _) => idle.Start();
        DetachedFromVisualTree += (_, _) => idle.Stop();
        ClipToBounds = true;
    }

    /// <summary>
    /// Builds the whole-world overview of a world's map data, on the build's thread (tests give one that fails).
    /// </summary>
    internal Func<MapData, MapData.Layers> BuildGlobal { get; set; } = static data => data.Global;

    /// <summary>
    /// Shows a world (its map data is worked out in the background: a few seconds).
    /// </summary>
    public void Show(WorldSession session)
    {
        _session = session;
        // The last world's map is no longer wanted: its build stops, and gives its threads back.
        StopBuild();
        var build = _build = new CancellationTokenSource();
        _data = new MapData(session.Terrain, session.Edits, build.Token);
        _global = null;
        _globalShown = false;
        _detail = null;
        _detailShown = null;
        _detailFailed = null;
        ReadPieces();
        Status?.Invoke("Building the world map (same as the game does)…");
        var data = _data;
        // On a thread of its own (LongRunning), not one of the pool's: the build keeps all the cores but one
        // busy, and the pool keeps the rest for everything else.
        Task.Factory.StartNew(() =>
        {
            MapData.Layers g;
            try
            {
                g = BuildGlobal(data);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Said and logged, not lost in a task nobody waits for (the map left blank, still "Building…").
                Console.WriteLine($"World map: could not build it: {ex}");
                Dispatcher.UIThread.Post(() =>
                {
                    if (_data == data)
                    {
                        Status?.Invoke($"Could not build the world map: {ex.Message}");
                    }
                });
                return;
            }
            // Kept on the UI thread, where Show and Dispose change _data: only if this world is still the one shown.
            Dispatcher.UIThread.Post(() =>
            {
                if (_data != data)
                {
                    return;
                }
                _global = g;
                Status?.Invoke("");
                RequestNextFrameRendering();
            });
        }, build.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Stops the world map's build, if one runs; one that finishes anyway is not shown.
    /// </summary>
    private void StopBuild()
    {
        _data = null;
        _build?.Cancel();
        _build?.Dispose();
        _build = null;
    }

    /// <summary>
    /// Leaving the map for good: its build is stopped.
    /// </summary>
    public void Dispose() => StopBuild();

    /// <summary>
    /// The map data changed (edits saved, discarded): the close-up and the player-built pieces are read again.
    /// </summary>
    public void Refresh()
    {
        _detailShown = null;
        _detail = null;
        _detailFailed = null;
        ReadPieces();
        RequestNextFrameRendering();
    }

    /// <summary>Counts the pieces' reads started: only the latest one's result is shown.</summary>
    private long _piecesTurn;

    /// <summary>
    /// Reads the world's player-built pieces in the background (deleted ones left out); dropped if another world is
    /// shown or another read started meanwhile; a failure is logged.
    /// </summary>
    private void ReadPieces()
    {
        var s = _session;
        if (s == null)
        {
            return;
        }
        var world = s.World;
        var deleted = s.Edits.Deleted;
        // Two reads can run at once (a refresh while one runs): only the latest is shown, else an older one finishing
        // last would bring back pieces deleted since.
        long turn = ++_piecesTurn;
        Task.Run(() =>
        {
            float[] data, ruins;
            bool[] joined;
            (bool Ruin, int Index)[] order;
            try
            {
                data = PieceCatalog.Encode(world, deleted);
                (ruins, joined) = PieceCatalog.EncodeRuins(world, deleted);
                const int S = PieceCatalog.Stride;
                // Lower pieces first so roofs end up on top, like looking down from above.
                order = [.. Enumerable.Range(0, data.Length / S).Select(i => (Ruin: false, Index: i))
                    .Concat(Enumerable.Range(0, ruins.Length / S).Select(i => (Ruin: true, Index: i)))
                    .OrderBy(e => (e.Ruin ? ruins : data)[e.Index * S + 8])];
            }
            catch (Exception ex)
            {
                // Logged: the map shows the buildings it had (none on a first read).
                Console.Error.WriteLine($"The map's buildings could not be read: {ex}");
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (_session != s || _piecesTurn != turn)
                {
                    return;
                }
                _pieceOrder = order;
                _ruins = ruins;
                _ruinJoined = joined;
                _pieces = data;
                _buildingsKey = default;
                PiecesRead?.Invoke();
                RequestNextFrameRendering();
            });
        });
    }

    /// <summary>
    /// Where a world point is on the control (its own units, not device pixels).
    /// </summary>
    public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - Center.X) / MetersPerPixel, Bounds.Height / 2 - (z - Center.Y) / MetersPerPixel);

    /// <summary>
    /// Compiles the map, line and fill programs and makes their buffers.
    /// </summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        // In the log, as the 3D view's: what draws the map.
        Options.Say($"map: OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}");
        _prog = Program(MapShader.Vertex, MapShader.Fragment);
        _vao = _own.VertexArray();
        _gl.BindVertexArray(_vao);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        float[] tri = [-1, -1, 3, -1, -1, 3];
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

    /// <summary>
    /// Shapes in world metres with a colour per corner (buildings, zone fills, pins, players).
    /// </summary>
    private const string FillVs = """
		layout(location = 0) in vec2 aPos;
		layout(location = 1) in vec4 aCol;
		uniform vec4 uView;
		out vec4 vCol;
		void main() { gl_Position = vec4((aPos - uView.xy) * uView.zw, 0.0, 1.0); vCol = aCol; }
		""";

    /// <summary>
    /// The fill program's fragment shader: the corner colours as they are.
    /// </summary>
    private const string FillFs = """
		in vec4 vCol;
		out vec4 frag;
		void main() { frag = vCol; }
		""";

    /// <summary>
    /// The map left the window (the 3D editor is shown): its OpenGL context goes. Its textures, buffers and programs
    /// are shared with Avalonia's own context and would outlive it: they are deleted, then uploaded again into the next
    /// context.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _own.DeleteAll();
        _tex.Clear();
        _texturesLoaded = false;
        _globalShown = false;
        _detailShown = null;
    }

    /// <summary>
    /// A linked program from vertex and fragment source, with the version line for this context put in front.
    /// </summary>
    /// <returns>The program, owned by <c>_own</c>.</returns>
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

    /// <summary>
    /// A texture on its own unit, bound to the sampler of that name.
    /// </summary>
    /// <param name="name">The map shader's sampler name; the texture is made on first use.</param>
    /// <param name="fill">Uploads the pixels and sets the filters, the texture already bound.</param>
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

    /// <summary>
    /// Plain stand-ins (RGBA) when the game's map textures cannot be read (Valheim not found).
    /// </summary>
    private static readonly Dictionary<string, byte[]> Fallback = new()
    {
        ["_BackgroundTex"] = [200, 186, 150, 255],
        ["_FogLayerTex"] = [255, 255, 255, 0],
        ["_WaterTex"] = [128, 128, 128, 255],
        ["_lavaTex"] = [128, 128, 128, 255],
        ["_MountainTex"] = [255, 255, 255, 255],
        ["_CloudTex"] = [0, 0, 0, 0],
        ["_ForestTex"] = [128, 128, 128, 0],
        ["_SpaceTex"] = [10, 10, 16, 255],
    };

    /// <summary>
    /// The game's map textures as decoded for the last bundles reader asked for: decoded once, not again each time the
    /// map comes back (a new context uploads them again); another Valheim folder is read again.
    /// </summary>
    private static readonly KeptRead<GameBundles, IReadOnlyDictionary<string, GameLookData.Picture>> MapPictures = new(GameLookData.ReadMap);

    /// <summary>
    /// Uploads the game's map textures (or the plain stand-ins) and a small placeholder close-up.
    /// </summary>
    private unsafe void LoadTextures()
    {
        _texturesLoaded = true;
        IReadOnlyDictionary<string, GameLookData.Picture> pictures = new Dictionary<string, GameLookData.Picture>();
        try
        {
            if (GameLook.Bundles is { } game)
            {
                pictures = MapPictures.Of(game);
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

    /// <summary>
    /// Uploads map layers as the shader's height, main and mask textures (and paint for the close-up).
    /// </summary>
    /// <param name="prefix">"g" for the whole world, "d" for the 1 m close-up.</param>
    /// <param name="l">The layers.</param>
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

    /// <summary>
    /// Material values from the game's "minimap shader" material, and clear-midday lighting. sRGB colours, made linear
    /// when set.
    /// </summary>
    private static readonly (string Name, float[] V)[] Material =
    [
        ("_ForestColor", new[] { 0.9191f, 0.8218f, 0.6758f, 1 }), ("_WaterColor", new[] { 0.8429f, 0.9373f, 1.0917f, 1 }), ("_WaterColorDeep", new[] { 0.343f, 0.5337f, 0.7647f, 1 }),
        ("_WaterColorAshlands", new[] { 0.2078f, 0.2078f, 0.2078f, 1 }), ("_WaterColorAshlandsDeep", new[] { 0.2235f, 0.251f, 0.3686f, 1 }),
        ("_lightColor", new[] { 1.2f, 1.2f, 1.2f }), ("_ambientLightColor", new[] { 0.8088f, 0.8418f, 1.0f }), ("_lavaColor1", new[] { 0.4157f, 0.0235f, 0.0f }), ("_lavaColor2", new[] { 4.5126f, 0.2599f, 0.0f }),
        ("_SunColor", new[] { 1.0f, 0.97f, 0.9f, 1 }), ("_AmbientColor", new[] { 0.36f, 0.38f, 0.44f, 1 }), ("_SunFogColor", new[] { 0.9f, 0.9f, 1, 1 }),
    ];

    /// <summary>
    /// One sRGB channel (0..1) to linear.
    /// </summary>
    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>
    /// Device pixels per control unit.
    /// </summary>
    private double Scaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <inheritdoc/>
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
            if (loc < 0)
            {
                continue;
            }

            float[] c = [.. v.Select((x, i) => i < 3 ? ToLinear(x) : x)];
            if (c.Length == 3)
            {
                _gl.Uniform3(loc, c[0], c[1], c[2]);
            }
            else
            {
                _gl.Uniform4(loc, c[0], c[1], c[2], c[3]);
            }
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
        _gl.Uniform4(U("detailRect"), d?.X0 ?? 0, d?.Z0 ?? 0, d?.Size ?? 1, d != null ? 1f : 0f);
        _gl.Uniform1(U("showPaint"), ShowPaint ? 1f : 0f);
        _gl.Uniform1(U("showClouds"), ShowClouds ? 1f : 0f);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GlBlend.Over(_gl);
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
        FramesDrawn++;
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
        if (_picture is { } req && FramesDrawn > 10 && (mpp >= 6 || (_detailShown != null && _detail == _detailShown && !_asking)))
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

    /// <summary>
    /// Close up (under 6 m a pixel): the ground in view at 1 m, with the edits, read in the background. True while one
    /// is being read.
    /// </summary>
    private bool _asking;
    /// <summary>
    /// The test driver's pending picture, saved once the map is drawn.
    /// </summary>
    private volatile GlPicture.Request? _picture;

    /// <summary>
    /// For the test driver: saves a picture of the map once the world map is in, a few frames are drawn and (zoomed in)
    /// the close-up is shown.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <returns>Done when the picture is saved.</returns>
    internal Task Picture(string path)
    {
        var r = new GlPicture.Request { Path = path };
        _picture = r;
        RequestNextFrameRendering();
        return r.Done.Task;
    }

    /// <summary>
    /// Frames drawn since the map was made.
    /// </summary>
    internal int FramesDrawn { get; private set; }

    /// <summary>
    /// The 1 m close-up is drawn (zoomed in, once read).
    /// </summary>
    internal bool DetailShown => _detailShown != null;

    /// <summary>
    /// The test driver checks drawing raised no OpenGL error (counted only when driven).
    /// </summary>
    internal int GlErrors { get; private set; }
    /// <summary>
    /// Zoomed in under 6 m a pixel: reads a close-up covering the view (a bit more, in whole zones) in the background,
    /// unless the one read covers it at the current edit version.
    /// </summary>
    /// <param name="mpp">Metres per device pixel.</param>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    private void AskDetail(float mpp, int w, int h)
    {
        if (_data == null || _session == null || mpp >= 6 || _asking)
        {
            return;
        }
        int size = Math.Clamp((int)MathF.Ceiling(MathF.Max(w, h) * mpp * 1.3f / 64) * 64, 64, 2048);
        int x0 = (int)MathF.Floor((Center.X - size / 2f) / 64) * 64, z0 = (int)MathF.Floor((Center.Y - size / 2f) / 64) * 64;
        int version = _session.Edits.Version;
        if (_detail is { } d && d.Version == version
            && ((d.X0 <= Center.X - w * mpp / 2 && d.Z0 <= Center.Y - h * mpp / 2 && d.X0 + d.Size >= Center.X + w * mpp / 2 && d.Z0 + d.Size >= Center.Y + h * mpp / 2)
                // The largest close-up can be narrower than the view: the one there is that one, not asked for again
                // each frame.
                || (d.X0 == x0 && d.Z0 == z0 && d.Size == size)))
        {
            return;
        }
        if (_detailFailed == (x0, z0, size, version))
        {
            return;
        }
        _asking = true;
        var data = _data;
        Task.Run(() =>
        {
            MapData.Layers? layers = null;
            bool failed = false;
            try
            {
                layers = data.Detail(x0, z0, size, version);
            }
            // A stopped build (another world shown) is no failure.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"The map's close-up could not be read: {ex}");
                failed = true;
            }
            finally
            {
                // As the world map: kept on the UI thread, only if this world is still the one shown. Asking again
                // only once it is there (or failed), else a frame drawn meanwhile would ask for the same close-up.
                Dispatcher.UIThread.Post(() =>
                {
                    if (layers != null && _data == data)
                    {
                        _detail = (layers, x0, z0, size, version);
                    }
                    if (failed && _data == data)
                    {
                        _detailFailed = (x0, z0, size, version);
                    }
                    _asking = false;
                    RequestNextFrameRendering();
                });
            }
        });
    }

    // ---- Shapes over the map in world metres (one colour per corner, drawn in order).
    /// <summary>
    /// Draws shapes from <paramref name="data"/>: x, z then RGBA, 6 floats per corner.
    /// </summary>
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

    /// <summary>
    /// Adds a quad (two triangles a-b-c, a-c-d) of one colour.
    /// </summary>
    private static void Quad(List<float> to, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 col)
    {
        Corner(to, a.X, a.Y, col);
        Corner(to, b.X, b.Y, col);
        Corner(to, c.X, c.Y, col);
        Corner(to, a.X, a.Y, col);
        Corner(to, c.X, c.Y, col);
        Corner(to, d.X, d.Y, col);
    }

    /// <summary>Adds one corner: x, z then its colour (no array made for it).</summary>
    private static void Corner(List<float> to, float x, float z, Vector4 col)
    {
        to.Add(x);
        to.Add(z);
        to.Add(col.X);
        to.Add(col.Y);
        to.Add(col.Z);
        to.Add(col.W);
    }

    /// <summary>
    /// Adds a disc of one colour, as a fan of <paramref name="sides"/> triangles.
    /// </summary>
    /// <param name="to">Where the corners go.</param>
    /// <param name="x">Middle, east.</param>
    /// <param name="z">Middle, north.</param>
    /// <param name="r">Radius in metres.</param>
    /// <param name="col">Its colour.</param>
    /// <param name="sides">How many triangles.</param>
    private static void Disc(List<float> to, float x, float z, float r, Vector4 col, int sides = 12)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
            Corner(to, x, z, col);
            Corner(to, x + r * MathF.Cos(a0), z + r * MathF.Sin(a0), col);
            Corner(to, x + r * MathF.Cos(a1), z + r * MathF.Sin(a1), col);
        }
    }

    /// <summary>
    /// The web map's piece colours per category (fill, edge): misc, crafting, wood, stone, furniture.
    /// </summary>
    private static readonly (Vector4 Fill, Vector4 Edge)[] PieceStyle =
    [
        (new(150 / 255f, 72 / 255f, 34 / 255f, 0.92f), new(60 / 255f, 25 / 255f, 10 / 255f, 0.9f)),
        (new(128 / 255f, 56 / 255f, 40 / 255f, 0.9f), new(55 / 255f, 22 / 255f, 14 / 255f, 0.9f)),
        (new(92 / 255f, 64 / 255f, 40 / 255f, 0.88f), new(38 / 255f, 24 / 255f, 14 / 255f, 0.9f)),
        (new(110 / 255f, 104 / 255f, 96 / 255f, 0.92f), new(45 / 255f, 42 / 255f, 38 / 255f, 0.9f)),
        (new(140 / 255f, 102 / 255f, 62 / 255f, 0.8f), new(60 / 255f, 40 / 255f, 22 / 255f, 0.85f)),
    ];

    /// <summary>
    /// Built once per zoom kind: footprints close up (edges very close), dots of two pixels further out. What the
    /// building shapes were last built for (the pieces, the kind, for dots the zoom, and which are shown).
    /// </summary>
    private (float[]? Pieces, int Kind, float Mpp, bool Buildings, bool Ruins) _buildingsKey;
    /// <summary>
    /// The building shapes last built: footprints or dots, and close up their edges.
    /// </summary>
    private float[] _buildingFill = [], _buildingEdge = [];

    /// <summary>
    /// Draws the player-built pieces: footprints under 2.5 m a pixel (outlined under 0.8 m), dots up to 10 m, nothing
    /// further out.
    /// </summary>
    private void DrawBuildings()
    {
        float[]? built = _pieces;
        float[] ruins = _ruins ?? [];
        float mpp = MetersPerPixel;
        if (built == null || !ShowBuildings && !ShowRuins || mpp > 10)
        {
            return;
        }
        int kind = mpp < 0.8f ? 2 : mpp < 2.5f ? 1 : 0;
        if (_buildingsKey.Pieces != built || _buildingsKey.Kind != kind || (kind == 0 && _buildingsKey.Mpp != mpp) || _buildingsKey.Buildings != ShowBuildings || _buildingsKey.Ruins != ShowRuins)
        {
            _buildingsKey = (built, kind, mpp, ShowBuildings, ShowRuins);
            var fill = new List<float>();
            var edge = new List<float>();
            const int S = PieceCatalog.Stride;
            foreach (var (ruin, i) in _pieceOrder)
            {
                // A ruin's piece joined to a building is drawn with it; the other ruins' fainter.
                bool joined = ruin && _ruinJoined[i], faint = ruin && !joined;
                if (!(faint ? ShowRuins : ShowBuildings))
                {
                    continue;
                }
                float[] pc = ruin ? ruins : built;
                int o = i * S;
                float px = pc[o], pz = pc[o + 2];
                var style = PieceStyle[Math.Clamp((int)pc[o + 9], 0, PieceStyle.Length - 1)];
                if (faint)
                {
                    style = (style.Fill with { W = style.Fill.W * 0.55f }, style.Edge with { W = style.Edge.W * 0.55f });
                }
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
                        edge.AddRange([p.X, p.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W, q.X, q.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W]);
                    }
                }
            }
            _buildingFill = [.. fill];
            _buildingEdge = [.. edge];
        }
        Fill(_buildingFill, PrimitiveType.Triangles);
        Fill(_buildingEdge, PrimitiveType.Lines);
    }

    /// <summary>
    /// The zone filter's matches (blue) and the zones marked for reset (red).
    /// </summary>
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
        Fill([.. data], PrimitiveType.Triangles);
    }

    /// <summary>What the pins' shapes were last made for: the pins, the chosen one, the zoom and the players.</summary>
    private (IReadOnlyList<Vector2>? Pins, int Chosen, float Mpp, IReadOnlyList<(string, float, float)>? Players) _pinsKey;

    /// <summary>The pins' shapes last made (<see cref="PinShapes"/>).</summary>
    private float[] _pinShapes = [];

    /// <summary>
    /// Search results (orange pins, the chosen one larger with a ring) and, live, the players (blue).
    /// </summary>
    private void DrawPins() => Fill(PinShapes(), PrimitiveType.Triangles);

    /// <summary>
    /// The shapes of <c>DrawPins</c>, as triangles (x, z then RGBA per corner): made again only when the pins, the
    /// chosen one, the zoom or the players changed (the pins and players are copies made when set, so a new one is a
    /// change), else the last ones as they are.
    /// </summary>
    internal float[] PinShapes()
    {
        float m = MetersPerPixel;
        var key = (Pins, PinChosen, m, Players);
        if (_pinsKey == key)
        {
            return _pinShapes;
        }
        _pinsKey = key;
        bool chosen = PinChosen >= 0 && PinChosen < Pins.Count;
        // Corners: 2 discs of 8 sides a pin; the chosen one 24 quads and 2 discs of 12; 2 discs of 12 a player.
        var data = new List<float>(6 * ((Pins.Count - (chosen ? 1 : 0)) * 48 + (chosen ? 216 : 0) + Players.Count * 72));
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
        if (chosen)
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
        _pinShapes = [.. data];
        return _pinShapes;
    }

    // ---- Lines over the map (in screen pixels): the chosen area, edited zones, resets, the grid.
    /// <summary>
    /// Draws the lines: the zone grid, edited zones (yellow; orange where changed since the world was loaded),
    /// resets and the chosen area.
    /// </summary>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    /// <param name="mpp">Metres per device pixel.</param>
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
                to.AddRange([p.X, p.Y, 0, q.X, q.Y, 0]);
            }
        }
        float halfW = w / 2f * mpp, halfH = h / 2f * mpp;
        // Shown from 8 m a screen pixel, whatever the screen's scaling (mpp is per device pixel).
        if (ShowGrid && MetersPerPixel < 8)
        {
            var grid = new List<float>();
            for (float x = MathF.Floor((Center.X - halfW + 32) / 64) * 64 - 32; x <= Center.X + halfW; x += 64)
            {
                var a = C(x, Center.Y - halfH); var b = C(x, Center.Y + halfH);
                grid.AddRange([a.X, a.Y, 0, b.X, b.Y, 0]);
            }
            for (float z = MathF.Floor((Center.Y - halfH + 32) / 64) * 64 - 32; z <= Center.Y + halfH; z += 64)
            {
                var a = C(Center.X - halfW, z); var b = C(Center.X + halfW, z);
                grid.AddRange([a.X, a.Y, 0, b.X, b.Y, 0]);
            }
            groups.Add((grid.ToArray(), new Vector4(1, 1, 1, 0.25f)));
        }
        if (ShowEdited)
        {
            var edited = new List<float>();
            var changed = new List<float>();
            foreach (var (zx, zz, isChanged) in s.Edits.EditedZones())
            {
                Box(isChanged ? changed : edited, zx * 64 - 32, zz * 64 - 32, zx * 64 + 32, zz * 64 + 32);
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
        GlBlend.Over(_gl);
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
    /// <summary>
    /// Where the mouse was pressed, and where it was last while dragging; null when not pressed.
    /// </summary>
    private Point? _press, _last;

    /// <summary>
    /// The world point (x east, z north) under a point of the control.
    /// </summary>
    public Vector2 WorldAt(Point p) => new(Center.X + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, Center.Y - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

    /// <summary>
    /// The OpenGL picture cannot be hit by the pointer (like the 3D view): a transparent surface laid over it takes the
    /// mouse and hands it here.
    /// </summary>
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

    /// <summary>
    /// Looks at a zone, close enough to see it.
    /// </summary>
    /// <param name="x">World point to centre on, east.</param>
    /// <param name="z">World point to centre on, north.</param>
    /// <param name="metersPerPixel">The zoom to show it at.</param>
    public void LookAt(float x, float z, float metersPerPixel = 2)
    {
        Center = new Vector2(x, z);
        MetersPerPixel = metersPerPixel;
        RequestNextFrameRendering();
        ViewChanged?.Invoke();
    }
}
