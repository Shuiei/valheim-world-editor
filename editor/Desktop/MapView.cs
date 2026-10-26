using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using MapData = ValheimGen.MapData;
using Vector2 = System.Numerics.Vector2;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world map, drawn like the game's (the web editor's mapview.js, with Valheim's own map shader): the whole world
/// from the game's map data, and a 1 m close-up that includes the edits when zoomed in. Drag to slide, wheel to zoom
/// (toward the mouse), click to pick a spot. Lines on top: the chosen area, edited zones, zones marked for reset, and
/// the zone grid.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// The OpenGL functions of the current context.
    /// </summary>
    private GL _gl = null!;
    /// <summary>The programs' uniform locations, asked once each; made again with the context.</summary>
    private GlUniforms _u = null!;
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
    /// Draw the world's portals (violet discs) and the links between portals of one tag, with their tags when zoomed
    /// in (off at first: it shows where every portal is).
    /// </summary>
    public bool ShowPortals { get; set; }
    /// <summary>
    /// The world's portals and their links (PortalMap.Find), read with the pieces; null until read.
    /// </summary>
    public PortalMap.Result? Portals => _portals;
    /// <summary>
    /// <see cref="Portals"/>' store, set on the UI thread once read.
    /// </summary>
    private volatile PortalMap.Result? _portals;
    /// <summary>
    /// Why the portals could not be read (a chunk file missing or locked), or null; set with <see cref="Portals"/>.
    /// </summary>
    public string? PortalsError { get; private set; }
    /// <summary>
    /// Raised on the UI thread when the portals have been read (with <c>PiecesRead</c>).
    /// </summary>
    public event Action? PortalsRead;
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
    /// The world map's side in metres: the overview's 2048 texels at this world's texel size (the game's 12 m, more
    /// for a wider world); the game's 24 576 m before a world is shown.
    /// </summary>
    public float MapMeters => MapData.GlobalSize * (_data?.TexelMeters ?? MapData.GlobalPixel);
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
    /// <summary>
    /// Where a world point is on the control (its own units, not device pixels).
    /// </summary>
    public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - Center.X) / MetersPerPixel, Bounds.Height / 2 - (z - Center.Y) / MetersPerPixel);
    /// <summary>
    /// Device pixels per control unit.
    /// </summary>
    private double Scaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

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
