using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool, like the web editor's (editor/plant.js): any kind of object, painted with a brush (a density and a
/// minimum spacing, like WorldPainter's tree layers; Shift + drag removes the chosen kinds), or along lines, circles
/// and rectangles (pieces end to end at their snap points, like the hammer), in grids and in zones. Positions are grid
/// points of the block (x east, z north).
/// </summary>
public sealed class PlaceTool
{
    /// <summary>
    /// How objects are laid out: painted with a brush, along a line (or circle, rectangle), in a grid, or scattered in
    /// a zone.
    /// </summary>
    public enum Modes
    {
        /// <summary>Painted with a brush.</summary>
        Brush,
        /// <summary>Along a line, circle or rectangle.</summary>
        Line,
        /// <summary>In a grid.</summary>
        Grid,
        /// <summary>Scattered in a zone.</summary>
        Zone,
    }
    /// <summary>
    /// Line mode: points clicked along the way, a circle (centre and radius) or a rectangle (two corners).
    /// </summary>
    public enum LineShapes
    {
        /// <summary>Points clicked along the way.</summary>
        Points,
        /// <summary>A circle: centre and radius.</summary>
        Circle,
        /// <summary>A rectangle: two corners.</summary>
        Rect,
    }
    /// <summary>
    /// Where objects stand: on the ground, <c>Elev</c> metres above it, or all at the one height <c>Elev</c>.
    /// </summary>
    public enum Elevations
    {
        /// <summary>On the ground.</summary>
        Ground,
        /// <summary><c>Elev</c> metres above the ground.</summary>
        Above,
        /// <summary>All at the one height <c>Elev</c>.</summary>
        At,
    }

    // ---- What the tool needs from the scene (set by the window).
    /// <summary>The scene objects go in (null while no world is open).</summary>
    public Func<WorldScene?> Scene { get; set; } = () => null;
    /// <summary>The mask now: a weight per grid point (0 keeps objects off it), or null for none.</summary>
    public Func<Func<int, float>?> Mask { get; set; } = () => null;
    /// <summary>A kind's model box in its own frame (Unity axes, scaled), or null while unknown.</summary>
    public Func<string, (Vector3 Min, Vector3 Max)?> ModelBox { get; set; } = _ => null;
    /// <summary>
    /// The brush's radius (shared with the sculpt brushes) and strength; its noise also drives Clumping.
    /// </summary>
    public Brush Brush { get; set; } = new();

    // ---- Settings.
    /// <summary>The layout mode.</summary>
    public Modes Mode { get; set; } = Modes.Brush;
    /// <summary>Line mode: the shape drawn.</summary>
    public LineShapes LineShape { get; set; } = LineShapes.Points;
    /// <summary>The kinds to place, by prefab name; each placement picks one by weight.</summary>
    public List<string> Chosen { get; } = ["Beech1"];
    /// <summary>Each chosen kind's weight in the mix (1 when not set; the panel offers 1 to 10).</summary>
    public Dictionary<string, int> Weights { get; } = [];
    /// <summary>Brush and Zone: objects per 100 m².</summary>
    public float Density { get; set; } = 3;
    /// <summary>
    /// Brush and Zone: the least distance between objects in metres, also from objects already there.
    /// </summary>
    public float Spacing { get; set; } = 4;
    /// <summary>
    /// Brush and Zone: clumping from 0 to 100%: how much objects gather in groves and leave clearings (0: even).
    /// </summary>
    public float Clump { get; set; }
    /// <summary>Clumping: the size of the groves and clearings' pattern, in metres.</summary>
    public float Patch { get; set; } = 30;
    /// <summary>The smallest random scale, in percent.</summary>
    public float SizeMin { get; set; } = 80;
    /// <summary>The largest random scale, in percent (at least SizeMin).</summary>
    public float SizeMax { get; set; } = 120;
    /// <summary>The most an object leans at random about x and z, in degrees.</summary>
    public float Tilt { get; set; }
    /// <summary>
    /// The turn added to every object's facing, in degrees from -180 to 180; the brush layout turns with it too.
    /// </summary>
    public float Rotation { get; set; }
    /// <summary>Objects face a random way (plus the rotation); off: all face the rotation.</summary>
    public bool RandomYaw { get; set; } = true;
    /// <summary>
    /// Brush: one object exactly at the cursor instead of a scatter; only an object right there (0.3 m) blocks it.
    /// </summary>
    public bool OneAtATime { get; set; }
    /// <summary>Line: the distance between objects along the line, in metres.</summary>
    public float Every { get; set; } = 4;
    /// <summary>Line: how far objects stray to either side of the line at random, in metres.</summary>
    public float Wiggle { get; set; }
    /// <summary>
    /// Line (Points): objects face along the line, plus the rotation, instead of a random facing. Circles and
    /// rectangles always do.
    /// </summary>
    public bool Along { get; set; } = true;
    /// <summary>
    /// Line (Points): a smooth curve through the points (with 3 or more) instead of straight stretches.
    /// </summary>
    public bool Curve { get; set; } = true;
    /// <summary>
    /// Line: pieces end to end at their snap points, like the hammer, instead of every <c>Every</c> metres.
    /// </summary>
    public bool EndToEnd { get; set; }
    /// <summary>
    /// End to end: how many pieces high each one is stacked (1 to 20; pieces under 0.1 m tall get one).
    /// </summary>
    public int Layers { get; set; } = 1;
    /// <summary>Line (Points): the line goes back to its first point (with 3 or more points).</summary>
    public bool Loop { get; set; }
    /// <summary>Grid: the spacing, centre to centre, in metres.</summary>
    public float Cell { get; set; } = 4;
    /// <summary>Where objects stand (see <c>Elev</c>).</summary>
    public Elevations Elevation { get; set; } = Elevations.Ground;
    /// <summary>The height above the ground, or the one height for all, in metres (unused On the ground).</summary>
    public float Elev { get; set; }
    /// <summary>
    /// Pieces snap to the pieces already there (beside or on top, and end-to-end runs start from one).
    /// </summary>
    public bool SnapTo { get; set; } = true;
    /// <summary>
    /// Building: where the build ray meets the ground outside (not a piece, not a dungeon floor) is rounded to this
    /// grid, in metres from the area's middle (0: off).
    /// </summary>
    public float GridStep { get; set; }
    /// <summary>Building pieces one by one (the Workshop's Build): no brush circle, no painting by dragging.</summary>
    public bool Building { get; set; }
    /// <summary>
    /// Building: the build ray (world space: origin, direction, where it meets the ground). The piece goes where the
    /// game's hammer would put it (Hammer).
    /// </summary>
    public (Vector3 O, Vector3 D, float? GroundT)? AimRay { get; set; }
    /// <summary>Building: Ctrl + wheel lifts (or lowers) the piece from where the cursor points, in metres.</summary>
    public float HeightNudge { get; set; }
    /// <summary>Building: the cut (world height); pieces above it are not pointed at.</summary>
    public float? CutY { get; set; }
    /// <summary>
    /// Brush, a single piece: snap on top of the piece under the cursor instead of beside the nearest one.
    /// </summary>
    public bool OnTop { get; set; }
    /// <summary>
    /// Saplings and crops: keep their grow radius free (from what stands and from each other), so the game lets them
    /// grow.
    /// </summary>
    public bool GrowRoom { get; set; } = true;
    /// <summary>Settings or the line changed: the preview is worked out again.</summary>
    public event Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>The largest value of each spacing setting (the panel's sliders end there).</summary>
    public const float MaxSpacing = 15, MaxEvery = 30, MaxCell = 30;

    /// <summary>
    /// The chosen kind that is widest at its largest size (m, across its model's box), for the fit hint; (0, null)
    /// while no chosen kind's model is known.
    /// </summary>
    public (float Width, string? Who) Widest()
    {
        float w = 0;
        string? who = null;
        foreach (string n in Chosen)
        {
            if (ModelBox(n) is var (lo, hi) && MathF.Max(hi.X - lo.X, hi.Z - lo.Z) is float x && x > w)
            {
                (w, who) = (x, n);
            }
        }
        return (w * MathF.Max(SizeMin, SizeMax) / 100, who);
    }

    /// <summary>
    /// The spacing the mode goes by: centre to centre in Grid and Line, the least distance in Brush and Zone; none for
    /// End to end lines (the pieces' own length sets it).
    /// </summary>
    public float? SpacingNow => Mode switch
    {
        Modes.Grid => Cell,
        Modes.Line => EndToEnd ? null : Every,
        _ => Spacing,
    };

    /// <summary>
    /// Says when the widest chosen kind is wider than the spacing (so they overlap), as the web editor did; null when
    /// they fit.
    /// </summary>
    public string? FitHint()
    {
        if (SpacingNow is not float v || Widest() is not (var w, string who) || w <= v + 0.25f)
        {
            return null;
        }
        return $"{who} is about {w:0.0} m wide: {v:0.##} m apart they overlap.";
    }

    /// <summary>
    /// Fit: the spacing set to the widest chosen kind (to the next half metre, within the setting's range), so they
    /// stand side by side without overlapping. False when there is nothing to fit.
    /// </summary>
    public bool Fit()
    {
        var (w, who) = Widest();
        if (SpacingNow == null || who == null || w <= 0)
        {
            return false;
        }
        float Up(float max) => MathF.Min(max, MathF.Ceiling(w * 2) / 2);
        switch (Mode)
        {
            case Modes.Grid:
                Cell = Up(MaxCell);
                break;
            case Modes.Line:
                Every = Up(MaxEvery);
                break;
            default:
                Spacing = Up(MaxSpacing);
                break;
        }
        NewLayout();
        return true;
    }

    /// <summary>The random draws for layouts, scatters and paint strokes.</summary>
    private readonly Random _rand = new();
    /// <summary>Offsets the clumping noise; a new layout picks a new one.</summary>
    private float _clumpSeed = Random.Shared.NextSingle() * 1000;
    /// <summary>The most objects a line, grid or zone places at once.</summary>
    public const int MaxShape = 2000;

    /// <summary>A kind's weight in the mix (1 when not set).</summary>
    public int WeightOf(string n) => Weights.GetValueOrDefault(n, 1);

    /// <summary>The kind for a random draw t in [0, 1), by weight.</summary>
    public string PickName(IReadOnlyList<string> names, float t)
    {
        float total = names.Sum(WeightOf), x = t * total;
        foreach (string n in names)
        {
            x -= WeightOf(n);
            if (x < 0)
            {
                return n;
            }
        }
        return names[^1];
    }

    /// <summary>A piece's snap points (x, y, z in its own frame), or none for kinds that are not pieces.</summary>
    public static float[][] SnapsOf(string name) => PieceCatalog.Get(StableHash.Of(name))?.Snaps ?? [];
    /// <summary>A building piece the game snaps: one with at least two snap points.</summary>
    public static bool IsPiece(string name) => SnapsOf(name).Length >= 2;
    /// <summary>Whether any chosen kind is a piece the game snaps.</summary>
    public bool PiecesChosen => Chosen.Any(IsPiece);

    /// <summary>
    /// End to end was set by hand: <see cref="AutoSnap"/> leaves it alone. End to end otherwise switches itself on when
    /// every chosen kind is a piece the game snaps (fences, walls…).
    /// </summary>
    public bool EndToEndByHand { get; set; }
    /// <summary>
    /// One at a time was set by hand: <see cref="AutoSnap"/> leaves it alone. Otherwise it switches itself on when
    /// every chosen kind is a piece the game snaps, so pieces go one at a time under the cursor.
    /// </summary>
    public bool OneAtATimeByHand { get; set; }
    /// <summary>
    /// Turns End to end and One at a time on when every chosen kind is a snapping piece (off otherwise), unless they
    /// were set by hand.
    /// </summary>
    public void AutoSnap()
    {
        bool pieces = Chosen.Count > 0 && Chosen.All(IsPiece);
        if (!EndToEndByHand)
        {
            EndToEnd = pieces;
        }
        if (!OneAtATimeByHand)
        {
            OneAtATime = pieces;
        }
    }

    /// <summary>
    /// Saplings and crops: the game lets them grow only with nothing within their grow radius; some only on cultivated
    /// ground. By prefab name.
    /// </summary>
    private static IReadOnlyDictionary<string, (float Radius, bool Cultivated)> GrowInfo => PrefabCatalog.GrowRoom;

    /// <summary>Whether any chosen kind needs room to grow.</summary>
    public bool GrowMatters => Chosen.Any(n => GrowInfo.TryGetValue(n, out var g) && g.Radius > 0);
    /// <summary>The chosen kinds that only grow on cultivated ground.</summary>
    public List<string> NeedCultivated => [.. Chosen.Where(n => GrowInfo.TryGetValue(n, out var g) && g.Cultivated)];
    /// <summary>The room a kind needs to grow, in metres (0 when it needs none or Leave room to grow is off).</summary>
    private float GrowNeed(string name) => GrowMatters && GrowRoom && GrowInfo.TryGetValue(name, out var g) ? g.Radius : 0;

    // ---- Where objects can go.
    /// <summary>
    /// A spatial hash of points (grid positions), to test quickly whether a spot is the spacing away from all of them.
    /// </summary>
    private sealed class Hash
    {
        /// <summary>The size of a hash cell, in metres.</summary>
        public readonly float Cell;
        /// <summary>The points in each cell.</summary>
        private readonly Dictionary<(int, int), List<Vector2>> _map = [];
        /// <summary>An empty hash with cells this many metres wide.</summary>
        public Hash(float cell) => Cell = cell;
        /// <summary>Adds a point.</summary>
        public void Add(Vector2 p)
        {
            var k = ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell));
            (_map.TryGetValue(k, out var l) ? l : _map[k] = []).Add(p);
        }
        /// <summary>Whether no point is closer than <c>spacing</c> to <c>p</c>.</summary>
        public bool Free(Vector2 p, float spacing)
        {
            int cx = (int)MathF.Floor(p.X / Cell), cz = (int)MathF.Floor(p.Y / Cell), n = Math.Max(1, (int)MathF.Ceiling(spacing / Cell));
            for (int dz = -n; dz <= n; dz++)
            {
                for (int dx = -n; dx <= n; dx++)
                {
                    if (_map.TryGetValue((cx + dx, cz + dz), out var l) && l.Any(q => Vector2.Distance(q, p) < spacing))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /// <summary>Every object standing in the block (grid positions), for the spacing check.</summary>
    private static Hash Standing(WorldScene s, float cell)
    {
        var h = new Hash(cell);
        float ox = s.OriginX, oz = s.OriginZ;
        lock (s.Things)
        {
            foreach (var t in s.Things)
            {
                if (!t.Gone)
                {
                    h.Add(new Vector2(t.Position.X - ox, t.Position.Z - oz));
                }
            }
        }
        return h;
    }

    /// <summary>
    /// One object's random draws, each from 0 to 1 unless said, kept so the preview stays put while the cursor moves.
    /// </summary>
    /// <param name="T">Picks the kind, by weight.</param>
    /// <param name="K">Clumping: kept where it is under the noise pattern's density.</param>
    /// <param name="Rx">Tilt about x, from -1 to 1 (times Tilt).</param>
    /// <param name="Ry">Facing, as a fraction of a turn (Random facing).</param>
    /// <param name="Rz">Tilt about z, from -1 to 1 (times Tilt).</param>
    /// <param name="S">Size between SizeMin and SizeMax.</param>
    /// <param name="W">Line: the sideways wiggle.</param>
    public sealed record Draw(float T, float K, float Rx, float Ry, float Rz, float S, float W);
    /// <summary>A fresh set of random draws.</summary>
    private Draw NewDraw() => new(_rand.NextSingle(), _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle());

    /// <summary>A placement: the kind, where (world position, and the grid point), turned and scaled.</summary>
    /// <param name="Name">The kind, by prefab name.</param>
    /// <param name="Position">World position.</param>
    /// <param name="Rotation">Unity Euler rotation, in degrees.</param>
    /// <param name="Scale">Uniform scale (0: as the template object, as for pieces).</param>
    /// <param name="G">The grid point of the block it stands on.</param>
    public sealed record Placement(string Name, Vector3 Position, Vector3 Rotation, float Scale, Vector2 G);

    /// <summary>The ground's height at a grid point of the block.</summary>
    private static float HeightAt(WorldScene s, Vector2 g) => Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Y - (s.H - 1) / 2f));

    /// <summary>
    /// Clumping (Brush and Zone): the density follows a noise pattern in world coordinates, so objects gather in groves
    /// and leave clearings. 0: even.
    /// </summary>
    private bool ClumpKeeps(WorldScene s, Vector2 g, float k)
    {
        float c = Clump / 100;
        if (c <= 0)
        {
            return true;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        float n = Math.Clamp(0.5f + 0.5f * Brush.Noise.Fbm((ox + g.X) / Patch + _clumpSeed, (oz + g.Y) / Patch - _clumpSeed), 0, 1);
        float lo = c * 0.75f - 0.1f, t = Math.Clamp((n - lo) / 0.25f, 0, 1);
        return k < t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Kelp and seaweed may go under water; other kinds on the ground are left out below the water line.
    /// </summary>
    private static bool UnderwaterOk(IEnumerable<string> names) => names.Any(n => n.Contains("kelp", StringComparison.OrdinalIgnoreCase) || n.Contains("seaweed", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A piece's bottom below its origin (its lowest snap point: walls have their origin in the middle).
    /// </summary>
    private static float? BottomOf(string name) => SnapsOf(name) is { Length: > 0 } sp ? sp.Min(p => p[1]) : null;

    /// <summary>
    /// The height an object's origin goes at for the Elevation: on the ground, a height above it, or one height for
    /// all; a piece's bottom (its lowest snap point) is what stands there.
    /// </summary>
    /// <param name="name">The kind, by prefab name.</param>
    /// <param name="groundY">The ground's height under it.</param>
    public float Elevated(string name, float groundY)
    {
        float? b = BottomOf(name);
        return Elevation == Elevations.At ? Elev - (b ?? 0) : groundY - (b ?? 0.05f) + (Elevation == Elevations.Above ? Elev : 0);
    }

    /// <summary>
    /// Zone and grid shapes can be turned as a whole, by this many degrees (-180 to 180); their objects turn with them.
    /// </summary>
    public float ShapeTurn { get; private set; }
    /// <summary>The point the shape turns about: its bounding box's middle when first turned.</summary>
    private Vector2? _pivot;
    /// <summary>The mode's shape can be turned (Zone and Grid).</summary>
    private bool Turnable => Mode is Modes.Zone or Modes.Grid;
    /// <summary>The rotation plus the shape's turn where it turns.</summary>
    private float RotationNow => Rotation + (Turnable ? ShapeTurn : 0);

    /// <summary>
    /// One object at a grid point, or null where it cannot go: off the block's edge, masked, thinned out by clumping,
    /// too close to another (or its grow room), or under water.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="g">The grid point.</param>
    /// <param name="d">The object's random draws.</param>
    /// <param name="names">The kinds to pick from.</param>
    /// <param name="hash">What stands already, for the spacing.</param>
    /// <param name="mask">The mask, or null.</param>
    /// <param name="minDist">The distance to keep from others instead of the mode's own.</param>
    /// <param name="yaw">The facing to use instead of a random one (degrees, before the rotation).</param>
    private Placement? PlacementAt(WorldScene s, Vector2 g, Draw d, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, float? minDist = null, float? yaw = null)
    {
        if (g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2)
        {
            return null;
        }
        if (mask != null && mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0)
        {
            return null;
        }
        if (Mode is Modes.Brush or Modes.Zone && !ClumpKeeps(s, g, d.K))
        {
            return null;
        }
        string name = PickName(names, d.T);
        // One at a time: you pick the spot, so only an object right on top (0.3 m) blocks it; a piece that
        // snaps to the pieces there goes where the snapping puts it (On top: right over the one below).
        float near = OneAtATime ? SnapTo && IsPiece(name) && Mode == Modes.Brush ? 0 : 0.3f : Spacing;
        if (!hash.Free(g, MathF.Max(minDist ?? near, GrowNeed(name))))
        {
            return null;
        }
        float y = HeightAt(s, g);
        if (Elevation == Elevations.Ground && y < s.Water - 0.3f && !UnderwaterOk(names))
        {
            return null;
        }
        float smin = SizeMin / 100, smax = MathF.Max(smin, SizeMax / 100);
        float ry = (yaw ?? (RandomYaw ? d.Ry * 360 : 0)) + RotationNow;
        float ox = s.OriginX, oz = s.OriginZ;
        return new Placement(name, new Vector3(ox + g.X, Elevated(name, y), oz + g.Y), new Vector3(d.Rx * Tilt, ry, d.Rz * Tilt), smin + d.S * (smax - smin), g);
    }

    // ---- Brush.
    /// <summary>
    /// Brush: the layout under the cursor (offsets from it, with their draws), kept until the settings change or R.
    /// </summary>
    private List<(Vector2 D, Draw Draw)> _pattern = [];
    /// <summary>The settings the brush layout was made for (empty: make a new one).</summary>
    private string _patternKey = "";
    /// <summary>The settings the brush layout depends on.</summary>
    private string SettingsKey => $"{Brush.Radius}|{Density}|{Spacing}|{OneAtATime}|{string.Join(",", Chosen)}";

    /// <summary>
    /// New random choices (R): a new brush layout, new draws for shapes, new zone spots and new clumping.
    /// </summary>
    public void NewLayout()
    {
        _patternKey = "";
        _draws.Clear();
        _scatter = null;
        _clumpSeed = _rand.NextSingle() * 1000;
        Notify();
    }

    /// <summary>
    /// Makes the brush layout: one object at the cursor (One at a time), or random points in the disc at least the
    /// spacing apart (one at least, so a click with a small brush places something; 400 at most).
    /// </summary>
    private void MakePattern()
    {
        float r = Brush.Radius;
        int n = Math.Clamp((int)MathF.Round(Density / 100 * MathF.PI * r * r), 1, 400);
        _pattern = [];
        if (OneAtATime)
        {
            _pattern.Add((Vector2.Zero, NewDraw()));
        }
        else
        {
            // Dart throwing: random points in the disc, at least the spacing apart.
            for (int tries = 0; _pattern.Count < n && tries < n * 30; tries++)
            {
                float a = _rand.NextSingle() * MathF.Tau, dist = MathF.Sqrt(_rand.NextSingle()) * r;
                var p = new Vector2(MathF.Cos(a) * dist, MathF.Sin(a) * dist);
                if (_pattern.Any(q => Vector2.Distance(q.D, p) < Spacing))
                {
                    continue;
                }
                _pattern.Add((p, NewDraw()));
            }
        }
        _patternKey = SettingsKey;
    }

    // ---- Line, grid and zone shapes.
    /// <summary>
    /// Line and Zone: the points clicked, grid points of the block (a zone's kept unturned). Changed under its own
    /// lock: the drawing thread reads it.
    /// </summary>
    public List<Vector2> Points { get; } = [];
    /// <summary>Grid: the box's first corner.</summary>
    public Vector2? GridA { get; set; }
    /// <summary>Grid: the box's opposite corner.</summary>
    public Vector2? GridB { get; set; }
    /// <summary>Circle: its centre; Rectangle: its first corner.</summary>
    public Vector2? FigA { get; set; }
    /// <summary>Circle: a point on its edge; Rectangle: the opposite corner (before end-to-end snapping).</summary>
    public Vector2? FigB { get; set; }
    /// <summary>Shapes: each object's draws, by its index in the shape.</summary>
    private readonly List<Draw> _draws = [];
    /// <summary>The draws of a shape's i-th object, made as needed.</summary>
    private Draw DrawAt(int i)
    {
        while (_draws.Count <= i)
        {
            _draws.Add(NewDraw());
        }
        return _draws[i];
    }

    /// <summary>A point turned by the shape's turn about its pivot.</summary>
    public Vector2 Xf(Vector2 p)
    {
        if (ShapeTurn == 0 || _pivot is not { } c)
        {
            return p;
        }
        float t = ShapeTurn * MathF.PI / 180, co = MathF.Cos(t), sn = MathF.Sin(t);
        var d = p - c;
        return new Vector2(c.X + d.X * co + d.Y * sn, c.Y - d.X * sn + d.Y * co);
    }

    /// <summary>A point turned back: the opposite of <see cref="Xf"/>.</summary>
    public Vector2 Inv(Vector2 p)
    {
        if (ShapeTurn == 0 || _pivot is not { } c)
        {
            return p;
        }
        float t = -ShapeTurn * MathF.PI / 180, co = MathF.Cos(t), sn = MathF.Sin(t);
        var d = p - c;
        return new Vector2(c.X + d.X * co + d.Y * sn, c.Y - d.X * sn + d.Y * co);
    }

    /// <summary>A point as kept: zone outlines are kept unturned.</summary>
    public Vector2 Local(Vector2 p) => Mode == Modes.Zone ? Inv(p) : p;

    /// <summary>Turns the zone or the grid box by this many degrees; false when there is none to turn.</summary>
    public bool TurnShape(float degrees)
    {
        var pts = Mode == Modes.Zone ? [.. Points] : GridA is { } a && GridB is { } b ? new List<Vector2> { a, b } : [];
        if (pts.Count == 0)
        {
            return false;
        }
        _pivot ??= new Vector2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
        ShapeTurn = ((ShapeTurn + degrees + 180) % 360 + 360) % 360 - 180;
        Notify();
        return true;
    }

    /// <summary>A new grid box starts unturned.</summary>
    public void ClearTurn()
    {
        ShapeTurn = 0;
        _pivot = null;
    }

    /// <summary>Removes the line, zone, figure and grid box, and their turn.</summary>
    public void ClearShape()
    {
        lock (Points)
        {
            Points.Clear();
        }
        FigA = FigB = GridA = GridB = null;
        ShapeTurn = 0;
        _pivot = null;
        Notify();
    }

    /// <summary>Turns the rotation by this many degrees (kept within -180 to 180).</summary>
    public void TurnBy(float degrees)
    {
        Rotation = ((Rotation + degrees + 180) % 360 + 360) % 360 - 180;
        Notify();
    }

    /// <summary>
    /// The line through the points: a smooth curve or straight stretches (Seg: the point each starts from).
    /// </summary>
    public List<(Vector2 P, int Seg)> LineCurve()
    {
        List<Vector2> p;
        lock (Points)
        {
            p = [.. Points];
        }
        if (p.Count < 3 || !Curve)
        {
            return [.. p.Select((q, i) => (q, Math.Min(i, Math.Max(0, p.Count - 2))))];
        }
        var outp = new List<(Vector2, int)>();
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Count - 1, i + 2)];
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                Vector2 Cr(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => 0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                outp.Add((Cr(p0, p1, p2, p3), i));
            }
        }
        outp.Add((p[^1], p.Count - 2));
        return outp;
    }

    /// <summary>
    /// End to end: a piece's two ends are the middles of the faces along its longer side, from its snap points or, for
    /// kinds without any, from its model's box.
    /// </summary>
    /// <param name="A">The start end, in the piece's own frame, at its lowest height.</param>
    /// <param name="B">The far end, likewise.</param>
    /// <param name="Len">The distance between the ends along the ground, in metres.</param>
    /// <param name="H">How tall it is (highest less lowest point), for stacking layers.</param>
    /// <param name="Heading">The direction from A to B in its own frame, in radians (Unity yaw: clockwise from
    /// +z).</param>
    public sealed record Ends(Vector3 A, Vector3 B, float Len, float H, float Heading);

    /// <summary>
    /// The ends of a set of points (snap points or box corners) given as separate x, y and z lists; null when shorter
    /// than 5 cm.
    /// </summary>
    private static Ends? EndsFrom(float[] xs, float[] ys, float[] zs)
    {
        float x0 = xs.Min(), x1 = xs.Max(), z0 = zs.Min(), z1 = zs.Max(), y = ys.Min();
        bool alongX = x1 - x0 >= z1 - z0 - 1e-3f;
        float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        var a = alongX ? new Vector3(x0, y, cz) : new Vector3(cx, y, z0);
        var b = alongX ? new Vector3(x1, y, cz) : new Vector3(cx, y, z1);
        float len = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        return len > 0.05f ? new Ends(a, b, len, ys.Max() - y, MathF.Atan2(b.X - a.X, b.Z - a.Z)) : null;
    }

    /// <summary>A kind's ends, from its snap points or else its model box; null while neither is known.</summary>
    public Ends? EndsOf(string name)
    {
        float[][] sp = SnapsOf(name);
        if (sp.Length >= 2 && EndsFrom([.. sp.Select(p => p[0])], [.. sp.Select(p => p[1])], [.. sp.Select(p => p[2])]) is { } e)
        {
            return e;
        }
        return ModelBox(name) is var (lo, hi) ? EndsFrom([lo.X, hi.X], [lo.Y, hi.Y], [lo.Z, hi.Z]) : null;
    }

    /// <summary>
    /// End to end: the figure's size snaps so that whole pieces of the first chosen kind close it; returns the corner
    /// or edge point to use instead of <c>b</c>.
    /// </summary>
    /// <param name="a">The circle's centre or the rectangle's first corner.</param>
    /// <param name="b">The point dragged to.</param>
    public Vector2 SnapFigure(Vector2 a, Vector2 b)
    {
        var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
        if (e == null)
        {
            return b;
        }
        if (LineShape == LineShapes.Rect)
        {
            float Fit(float d) => MathF.Sign(d == 0 ? 1 : d) * MathF.Max(1, MathF.Round(MathF.Abs(d) / e.Len)) * e.Len;
            return new Vector2(a.X + Fit(b.X - a.X), a.Y + Fit(b.Y - a.Y));
        }
        float r = Vector2.Distance(a, b);
        int n = Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len));
        float radius = e.Len / (2 * MathF.Sin(MathF.PI / n)), t = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        return new Vector2(a.X + MathF.Cos(t) * radius, a.Y + MathF.Sin(t) * radius);
    }

    /// <summary>
    /// The line as runs of points: one run, or a rectangle's four sides each on their own when placing end to end (so
    /// its corners stay exact).
    /// </summary>
    public List<List<Vector2>> LineRuns()
    {
        if (LineShape == LineShapes.Points)
        {
            var c = LineCurve().Select(q => q.P).ToList();
            if (Loop && c.Count >= 3)
            {
                c.Add(c[0]);
            }
            return c.Count >= 2 ? new() { c } : new();
        }
        if (FigA is not { } a || FigB is not { } fb)
        {
            return [];
        }
        var b = SnapFigure(a, fb);
        if (LineShape == LineShapes.Rect)
        {
            if (MathF.Abs(b.X - a.X) < 0.5f || MathF.Abs(b.Y - a.Y) < 0.5f)
            {
                return [];
            }
            var c = new List<Vector2> { a, new(b.X, a.Y), b, new(a.X, b.Y) };
            return EndToEnd ? [.. Enumerable.Range(0, 4).Select(i => new List<Vector2> { c[i], c[(i + 1) % 4] })] : new() { c.Append(c[0]).ToList() };
        }
        float r = Vector2.Distance(a, b);
        if (r < 0.5f)
        {
            return [];
        }
        // End to end: one point per piece, so the pieces meet at the corners of a regular polygon.
        var e = EndToEnd && Chosen.Count > 0 ? EndsOf(Chosen[0]) : null;
        int n = e != null ? Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len)) : Math.Max(16, (int)MathF.Ceiling(MathF.Tau * r / 1.5f));
        float t0 = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        return [[.. Enumerable.Range(0, n + 1).Select(i => a + new Vector2(MathF.Cos(t0 + i / (float)n * MathF.Tau), MathF.Sin(t0 + i / (float)n * MathF.Tau)) * r)]];
    }

    /// <summary>How much of the line is left at the end once no whole piece fits (end to end).</summary>
    public float SnapGap { get; private set; }

    /// <summary>
    /// The point along the run at a straight distance dist from s, searching from segment seg at fraction t0 (forwards
    /// only), or null past its end.
    /// </summary>
    private static (Vector2 Q, int Seg, float T)? AlongRun(List<Vector2> pts, Vector2 s, int seg, float t0, float dist)
    {
        for (; seg < pts.Count - 1; seg++, t0 = 0)
        {
            Vector2 a = pts[seg], d = pts[seg + 1] - a, f = a - s;
            float dd = d.LengthSquared(), b = 2 * Vector2.Dot(f, d), c = f.LengthSquared() - dist * dist, disc = b * b - 4 * dd * c;
            if (dd < 1e-9f || disc < 0)
            {
                continue;
            }
            float t = (-b + MathF.Sqrt(disc)) / (2 * dd);
            // A millimetre of slack at the end: float rounding over a whole ring of pieces otherwise
            // leaves the last one just past the end, and the ring one piece short.
            float slack = 1e-3f / MathF.Sqrt(dd);
            if (t >= t0 - 1e-6f && t <= 1 + slack)
            {
                t = MathF.Min(t, 1);
                return (a + d * t, seg, t);
            }
        }
        return null;
    }

    /// <summary>Unity's rotation (Euler degrees: z, then x, then y) of a local point.</summary>
    public static Vector3 RotU(Vector3 p, Vector3 r)
    {
        const float D = MathF.PI / 180;
        float cz = MathF.Cos(r.Z * D), sz = MathF.Sin(r.Z * D), cx = MathF.Cos(r.X * D), sx = MathF.Sin(r.X * D), cy = MathF.Cos(r.Y * D), sy = MathF.Sin(r.Y * D);
        float x = p.X, y = p.Y, z = p.Z;
        (x, y) = (x * cz - y * sz, x * sz + y * cz);
        (y, z) = (y * cx - z * sx, y * sx + z * cx);
        return new Vector3(x * cy + z * sy, y, -x * sy + z * cy);
    }

    /// <summary>A piece already in the block, for snapping.</summary>
    /// <param name="Index">Its index in the scene's things.</param>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="G">Its grid position.</param>
    /// <param name="Rotation">Its Unity Euler rotation, in degrees.</param>
    /// <param name="Pts">Its snap points (grid x, height, grid z).</param>
    /// <param name="Low">The height of its lowest snap point.</param>
    private sealed record Other(int Index, string Name, Vector2 G, Vector3 Rotation, List<Vector3> Pts, float Low);
    /// <summary>The snap points of every piece in the block.</summary>
    private static List<Other> OtherSnaps(WorldScene s, Func<int, string?> nameOf)
    {
        var list = new List<Other>();
        float ox = s.OriginX, oz = s.OriginZ;
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (t.Gone || PieceCatalog.Get(t.Prefab) is not { Snaps.Length: > 0 } info)
                {
                    continue;
                }
                var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
                var pts = info.Snaps.Select(p => { var q = RotU(new Vector3(p[0], p[1], p[2]), t.Rotation); return new Vector3(g.X + q.X, t.Position.Y + q.Y, g.Y + q.Z); }).ToList();
                list.Add(new Other(i, nameOf(t.Prefab) ?? info.Name, g, t.Rotation, pts, pts.Min(p => p.Y)));
            }
        }
        return list;
    }

    /// <summary>A prefab hash's name (null when unknown); set by the window.</summary>
    public Func<int, string?> NameOf { get; set; } = _ => null;

    /// <summary>The snap point of a piece already there nearest to p (along the ground), within d metres.</summary>
    private static Vector3? NearestSnap(List<Other> others, Vector2 p, float d)
    {
        Vector3? best = null;
        float bd = d;
        foreach (var o in others)
        {
            if (MathF.Abs(o.G.X - p.X) > d + 8 || MathF.Abs(o.G.Y - p.Y) > d + 8)
            {
                continue;
            }
            foreach (var q in o.Pts)
            {
                float dd = Vector2.Distance(new Vector2(q.X, q.Z), p);
                if (dd < bd)
                {
                    bd = dd;
                    best = q;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Places pieces end to end along one run, from its start; on a slope each one is a little higher or lower than the
    /// last, touching it. Adds the leftover length to <see cref="SnapGap"/>.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="pts">The run.</param>
    /// <param name="names">The kinds, used in turn.</param>
    /// <param name="k">Pieces placed so far (picks the next kind; stops at MaxShape, as does outp, layers
    /// included).</param>
    /// <param name="outp">Where the placements go.</param>
    /// <param name="mask">The mask, or null.</param>
    /// <param name="others">Pieces already there to start from (null: no snapping).</param>
    /// <returns>The pieces laid so far, this run included (masked-out spots count; extra layers do not).</returns>
    private int SnappedRun(WorldScene s, List<Vector2> pts, List<string> names, int k, List<Placement> outp, Func<int, float>? mask, List<Other>? others)
    {
        if (pts.Count < 2)
        {
            return k;
        }
        Vector2 st = pts[0];
        int seg = 0, sSeg = 0;
        float t0 = 0;
        // Started next to a piece already there (within 1 m): the run continues it, at its level.
        var start = others != null ? NearestSnap(others, st, 1) : null;
        if (start is { } st0)
        {
            st = new Vector2(st0.X, st0.Z);
        }
        int layers = Math.Clamp(Layers, 1, 20);
        float ox = s.OriginX, oz = s.OriginZ;
        for (; k < MaxShape && outp.Count < MaxShape; k++)
        {
            string name = names[k % names.Count];
            sSeg = seg;
            if (EndsOf(name) is not { } e || AlongRun(pts, st, seg, t0, e.Len) is not var (q, hseg, ht))
            {
                break;
            }
            // Turn so that the piece's start-to-end runs along st → q (Unity yaw, clockwise seen from above).
            float yaw = (MathF.Atan2(q.X - st.X, q.Y - st.Y) - e.Heading) * 180 / MathF.PI;
            float r = yaw * MathF.PI / 180, c = MathF.Cos(r), sn = MathF.Sin(r);
            float px = st.X - (e.A.X * c + e.A.Z * sn), pz = st.Y - (-e.A.X * sn + e.A.Z * c);
            float bottom = Elevation == Elevations.At ? Elev : start is { } sv && Elevation == Elevations.Ground ? sv.Y
                : MathF.Min(MathF.Min(HeightAt(s, st), HeightAt(s, q)), HeightAt(s, (st + q) / 2)) + (Elevation == Elevations.Above ? Elev : 0);
            if (px >= 1 && pz >= 1 && px <= s.W - 2 && pz <= s.H - 2 && (mask == null || mask((int)MathF.Round(pz) * s.W + (int)MathF.Round(px)) > 0))
            {
                for (int l = 0; l < (e.H > 0.1f ? layers : 1) && outp.Count < MaxShape; l++)
                {
                    outp.Add(new Placement(name, new Vector3(ox + px, bottom - e.A.Y + l * e.H, oz + pz), new Vector3(0, (yaw % 360 + 360) % 360, 0), 0, new Vector2(px, pz)));
                }
            }
            st = q;
            seg = hseg;
            t0 = ht;
        }
        for (int i = sSeg; i < pts.Count - 1; i++)
        {
            SnapGap += Vector2.Distance(i == sSeg ? st : pts[i], pts[i + 1]);
        }
        return k;
    }

    /// <summary>Zone: the spots, with a key of the zone and settings they were made for.</summary>
    private (float Key, List<Vector2> Pts)? _scatter;
    /// <summary>
    /// Random spots inside the zone (Density, Spacing), kept while the zone stays the same (R: new ones).
    /// </summary>
    private List<Vector2> ZoneSpots()
    {
        List<Vector2> poly;
        lock (Points)
        {
            poly = [.. Points];
        }
        float key = poly.Sum(p => p.X * 31 + p.Y * 17) + Density * 1000 + Spacing * 7;
        if (_scatter is { } sc && sc.Key == key)
        {
            return sc.Pts;
        }
        float x0 = poly.Min(p => p.X), x1 = poly.Max(p => p.X), z0 = poly.Min(p => p.Y), z1 = poly.Max(p => p.Y);
        int n = Math.Min(MaxShape, (int)MathF.Round(Density / 100 * AreaTool.Area(poly)));
        var pts = new List<Vector2>();
        var h = new Hash(MathF.Max(0.5f, Spacing));
        for (int tries = 0; pts.Count < n && tries < n * 30; tries++)
        {
            var p = new Vector2(x0 + _rand.NextSingle() * (x1 - x0), z0 + _rand.NextSingle() * (z1 - z0));
            if (!AreaTool.Inside(poly, p.X, p.Y) || !h.Free(p, Spacing))
            {
                continue;
            }
            h.Add(p);
            pts.Add(p);
        }
        _scatter = (key, pts);
        return pts;
    }

    /// <summary>
    /// What the line, grid or zone would place (Zone: needs 3 points). Also works out <see cref="SnapGap"/>.
    /// </summary>
    private List<Placement> ShapePlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, List<Other>? others)
    {
        var outp = new List<Placement>();
        SnapGap = 0;
        if (Mode == Modes.Zone)
        {
            if (Points.Count < 3)
            {
                return outp;
            }
            var spots = ZoneSpots();
            for (int i = 0; i < spots.Count; i++)
            {
                if (PlacementAt(s, Xf(spots[i]), DrawAt(i), names, hash, mask, Spacing) is { } o)
                {
                    outp.Add(o);
                }
            }
            return outp;
        }
        if (Mode == Modes.Line && EndToEnd)
        {
            var kinds = names.Where(n => EndsOf(n) != null).ToList();
            if (kinds.Count == 0)
            {
                return outp;
            }
            int k = 0;
            foreach (var run in LineRuns())
            {
                k = SnappedRun(s, run, kinds, k, outp, mask, SnapTo ? others : null);
            }
            return outp;
        }
        if (Mode == Modes.Line)
        {
            var c = LineRuns().SelectMany(r => r).ToList();
            if (c.Count < 2)
            {
                return outp;
            }
            float walked = 0, next = 0;
            int i = 0;
            for (int sgi = 1; sgi < c.Count; sgi++)
            {
                Vector2 a = c[sgi - 1], b = c[sgi];
                float len = Vector2.Distance(a, b);
                // 1 mm of slack: a line a whole number of spacings long gets its end piece whatever the
                // rounding of the points picked on the ground.
                while (next <= walked + len + 1e-3f)
                {
                    float t = len > 0 ? (next - walked) / len : 0, dx = (b.X - a.X) / (len == 0 ? 1 : len), dz = (b.Y - a.Y) / (len == 0 ? 1 : len);
                    var d = DrawAt(i);
                    float side = (d.W - 0.5f) * 2 * Wiggle;
                    var g = new Vector2(a.X + (b.X - a.X) * t - dz * side, a.Y + (b.Y - a.Y) * t + dx * side);
                    // Unity yaw: clockwise from north (+z): the heading of (dx, dz) is atan2(dx, dz). A kind with
                    // a known length turns its length along the line; others face along it.
                    var e = EndsOf(PickName(names, d.T));
                    float? yaw = Along || LineShape != LineShapes.Points ? (MathF.Atan2(dx, dz) - (e?.Heading ?? 0)) * 180 / MathF.PI : null;
                    if (PlacementAt(s, g, d, names, hash, mask, 0.3f, yaw) is { } o)
                    {
                        outp.Add(o);
                    }
                    i++;
                    next += MathF.Max(0.1f, Every);
                    if (i >= MaxShape)
                    {
                        return outp;
                    }
                }
                walked += len;
            }
            return outp;
        }
        if (Mode == Modes.Grid && GridA is { } ga && GridB is { } gb)
        {
            float x0 = MathF.Min(ga.X, gb.X), x1 = MathF.Max(ga.X, gb.X), z0 = MathF.Min(ga.Y, gb.Y), z1 = MathF.Max(ga.Y, gb.Y);
            // As many whole cells as fit best (at least one each way), centred in the box.
            int nx = Math.Max(1, (int)MathF.Round((x1 - x0) / Cell)), nz = Math.Max(1, (int)MathF.Round((z1 - z0) / Cell));
            float ox = (x0 + x1) / 2 - nx * Cell / 2, oz = (z0 + z1) / 2 - nz * Cell / 2;
            int i = 0;
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    if (i >= MaxShape)
                    {
                        return outp;
                    }
                    if (PlacementAt(s, Xf(new Vector2(ox + (ix + 0.5f) * Cell, oz + (iz + 0.5f) * Cell)), DrawAt(i++), names, hash, mask, 0.3f) is { } o)
                    {
                        outp.Add(o);
                    }
                }
            }
        }
        return outp;
    }

    /// <summary>
    /// Snapping a single piece to the pieces already there: beside one (end to end, at its level) or on top of the one
    /// under the cursor; its turn follows that piece (in quarter turns). What the last preview snapped to ("beside X",
    /// "on top of X"), or null.
    /// </summary>
    public string? SnappedTo { get; private set; }
    /// <summary>A piece's facing: the reference facing plus the rotation rounded to quarter turns.</summary>
    private float AlignYaw(float reference) => reference + 90 * MathF.Round(Rotation / 90);

    /// <summary>
    /// Snaps one piece to the pieces within 12 m: on top of the one under the cursor (or the nearest within 3 m), or
    /// beside the nearest where a pair of snap points meets within 1 m. Returns it unchanged when nothing snaps.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="o">The piece where the cursor puts it.</param>
    /// <param name="others">The pieces already there.</param>
    /// <param name="under">The thing under the cursor, for On top.</param>
    private Placement SnapSingle(WorldScene s, Placement o, List<Other> others, int? under)
    {
        SnappedTo = null;
        var sp = SnapsOf(o.Name).Select(p => new Vector3(p[0], p[1], p[2])).ToList();
        var near = others.Where(x => MathF.Abs(x.G.X - o.G.X) < 12 && MathF.Abs(x.G.Y - o.G.Y) < 12).ToList();
        if (near.Count == 0)
        {
            return o;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        Placement Place(Vector2 g, float y, float ry) => o with { G = g, Position = new Vector3(ox + g.X, y, oz + g.Y), Rotation = new Vector3(0, (ry % 360 + 360) % 360, 0), Scale = 0 };
        if (OnTop)
        {
            var below = near.FirstOrDefault(x => x.Index == under)
                ?? near.Where(x => Vector2.Distance(x.G, o.G) < 3).OrderBy(x => Vector2.Distance(x.G, o.G)).FirstOrDefault();
            if (below != null)
            {
                float top = below.Pts.Max(p => p.Y);
                var tops = below.Pts.Where(p => p.Y > top - 0.05f).ToList();
                float ry = AlignYaw(below.Rotation.Y);
                var mine = sp.Select(p => RotU(p, new Vector3(0, ry, 0))).ToList();
                float low = mine.Min(p => p.Y);
                var bottoms = mine.Where(p => p.Y < low + 0.05f).ToList();
                (float D, Vector2 G)? best = null;
                foreach (var t in tops)
                {
                    foreach (var m in bottoms)
                    {
                        var g = new Vector2(t.X - m.X, t.Z - m.Z);
                        float d = Vector2.Distance(g, o.G);
                        if (best == null || d < best.Value.D)
                        {
                            best = (d, g);
                        }
                    }
                }
                SnappedTo = $"on top of {below.Name}";
                return Place(best!.Value.G, top - low, ry);
            }
        }
        // Beside: turned like the nearest piece (the quarter turn that meets best, the facing asked for
        // first), then the closest pair of snap points along the ground (within 1 m) meets.
        var close = near.OrderBy(x => Vector2.Distance(x.G, o.G)).First();
        float[] yaws = Vector2.Distance(close.G, o.G) < 5 ? [.. new[] { 0f, 180, 90, 270 }.Select(k => AlignYaw(close.Rotation.Y) + k)] : [o.Rotation.Y];
        (float Score, Vector3 Q, Vector3 M, float Ry, string Name)? bestB = null;
        for (int k = 0; k < yaws.Length; k++)
        {
            var mine = sp.Select(p => RotU(p, new Vector3(0, yaws[k], 0)) + new Vector3(o.G.X, o.Position.Y, o.G.Y)).ToList();
            float myLow = mine.Min(p => p.Y);
            foreach (var x in near)
            {
                foreach (var q in x.Pts)
                {
                    foreach (var m in mine)
                    {
                        float d = MathF.Sqrt((q.X - m.X) * (q.X - m.X) + (q.Z - m.Z) * (q.Z - m.Z));
                        if (d > 1)
                        {
                            continue;
                        }
                        float score = d + MathF.Abs(q.Y - x.Low - (m.Y - myLow)) + 0.01f * k;
                        if (bestB == null || score < bestB.Value.Score)
                        {
                            bestB = (score, q, m, yaws[k], x.Name);
                        }
                    }
                }
            }
        }
        if (bestB is not { } bb)
        {
            return o;
        }
        SnappedTo = $"beside {bb.Name}";
        return Place(o.G + new Vector2(bb.Q.X - bb.M.X, bb.Q.Z - bb.M.Z), o.Position.Y + bb.Q.Y - bb.M.Y, bb.Ry);
    }

    /// <summary>Saplings and crops: the grow radius is kept between the placements themselves too.</summary>
    private List<Placement> RoomToGrow(List<Placement> list)
    {
        if (!GrowMatters || !GrowRoom || !list.Any(o => GrowNeed(o.Name) > 0))
        {
            return list;
        }
        var kept = new List<(Vector2 G, float Need)>();
        var outp = new List<Placement>();
        foreach (var o in list)
        {
            float need = GrowNeed(o.Name);
            if (kept.Any(k => Vector2.Distance(k.G, o.G) < MathF.Max(need, k.Need)))
            {
                continue;
            }
            kept.Add((o.G, need));
            outp.Add(o);
        }
        return outp;
    }

    /// <summary>The preview: what a click (Brush) or Enter (shapes) would place.</summary>
    /// <param name="at">The cursor's grid point (null off the ground).</param>
    /// <param name="under">The thing under the cursor (for On top).</param>
    public List<Placement> Preview(Vector2? at, int? under = null)
    {
        var s = Scene();
        var names = Chosen.ToList();
        SnappedTo = null;
        if (s == null || names.Count == 0 || Mode == Modes.Brush && at == null)
        {
            return [];
        }
        if (Building)
        {
            return BuildAt(s, names[0]) is { } one ? new() { one } : new();
        }
        var mask = Mask();
        var hash = Standing(s, MathF.Max(1, Spacing));
        var others = PiecesChosen && (SnapTo || Mode == Modes.Line) ? OtherSnaps(s, NameOf) : null;
        List<Placement> placed;
        if (Mode != Modes.Brush)
        {
            placed = ShapePlacements(s, names, hash, mask, others);
        }
        else
        {
            if (SettingsKey != _patternKey)
            {
                MakePattern();
            }
            // The layout turns with the rotation (clockwise from above, like the objects' facing).
            float t = RotationNow * MathF.PI / 180, c = MathF.Cos(t), sn = MathF.Sin(t);
            placed = [];
            foreach (var (d, dr) in _pattern)
            {
                var g = new Vector2(at!.Value.X + d.X * c + d.Y * sn, at.Value.Y - d.X * sn + d.Y * c);
                if (PlacementAt(s, g, dr, names, hash, mask) is { } o)
                {
                    placed.Add(o);
                }
            }
            if (placed.Count == 1 && SnapTo && others != null && IsPiece(placed[0].Name))
            {
                placed[0] = SnapSingle(s, placed[0], others, under);
            }
        }
        return RoomToGrow(placed);
    }

    /// <summary>
    /// Building (the Workshop): one piece where the game's hammer puts it (Hammer): on what the build ray hits, turned
    /// by Rotation, lifted by HeightNudge, snapped (SnapTo). On the ground, the Grid can round where the ray meets it
    /// (from the area's middle) first.
    /// </summary>
    private Placement? BuildAt(WorldScene s, string name)
    {
        if (AimRay is not var (o, d, groundT))
        {
            return null;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        var pieces = new List<Hammer.Placed>();
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (!t.Gone && NameOf(t.Prefab) is string n && Hammer.Get(n) != null)
                {
                    pieces.Add(new Hammer.Placed(i, n, t.Position, BlueprintFormats.FromEuler(t.Rotation)));
                }
            }
        }
        if (Hammer.Ray(o, d, pieces, groundT, CutY ?? float.MaxValue) is not { } hit)
        {
            return null;
        }
        // The grid rounds on the ground outside only (in a dungeon the ray meets a room's floor).
        if (hit.Piece == null && GridStep > 0 && hit.Point.Y < GlView.InteriorHeight)
        {
            float mx = ox + (s.W - 1) / 2f, mz = oz + (s.H - 1) / 2f;
            var gp = new Vector3(mx + MathF.Round((hit.Point.X - mx) / GridStep) * GridStep, 0, mz + MathF.Round((hit.Point.Z - mz) / GridStep) * GridStep);
            gp.Y = HeightAt(s, new Vector2(gp.X - ox, gp.Z - oz));
            hit = hit with { Point = gp };
        }
        float yaw = (Rotation % 360 + 360) % 360;
        var (pos, to) = Hammer.Place(name, BlueprintFormats.FromEuler(new Vector3(0, yaw, 0)), hit, pieces, HeightNudge, SnapTo);
        var g = new Vector2(pos.X - ox, pos.Z - oz);
        if (g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2)
        {
            return null;
        }
        SnappedTo = to is int k && NameOf(s.Things[k].Prefab) is string other ? $"snapped to {PieceCost.PieceName(other)}" : null;
        return new Placement(name, pos, new Vector3(0, yaw, 0), 0, g);
    }

    /// <summary>
    /// Brush painting: the placements a frame of a drag adds (around the cursor), kept apart from what stands and from
    /// what this stroke already placed. More per second with a stronger brush.
    /// </summary>
    /// <param name="at">The cursor's grid point.</param>
    /// <param name="dt">Seconds since the last step (counted as 0.1 at most).</param>
    /// <param name="already">What this stroke has placed so far.</param>
    public List<Placement> PaintStep(Vector2 at, float dt, List<Placement> already)
    {
        var s = Scene();
        var names = Chosen.ToList();
        var outp = new List<Placement>();
        if (s == null || names.Count == 0)
        {
            return outp;
        }
        var hash = Standing(s, MathF.Max(1, Spacing));
        foreach (var o in already)
        {
            hash.Add(o.G);
        }
        float r = Brush.Radius, want = Density / 100 * MathF.PI * r * r * MathF.Min(dt, 0.1f) * (0.5f + 2.5f * Brush.Strength);
        int n = (int)MathF.Floor(want) + (_rand.NextSingle() < want % 1 ? 1 : 0);
        var mask = Mask();
        for (int tries = 0; n > 0 && tries < n * 6; tries++)
        {
            float a = _rand.NextSingle() * MathF.Tau, d = MathF.Sqrt(_rand.NextSingle()) * r;
            if (PlacementAt(s, at + new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d), NewDraw(), names, hash, mask) is not { } o)
            {
                continue;
            }
            hash.Add(o.G);
            outp.Add(o);
            n--;
        }
        return outp;
    }

    /// <summary>
    /// Shift + drag: the things of the chosen kinds under the brush (indices into the scene's things), where the mask
    /// allows.
    /// </summary>
    public List<int> EraseAt(Vector2 at)
    {
        var s = Scene();
        var outp = new List<int>();
        if (s == null)
        {
            return outp;
        }
        var mask = Mask();
        float ox = s.OriginX, oz = s.OriginZ;
        var chosen = Chosen.Select(StableHash.Of).ToHashSet();
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
                if (t.Gone || !chosen.Contains(t.Prefab) || Vector2.Distance(g, at) > Brush.Radius)
                {
                    continue;
                }
                if (mask != null && mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0)
                {
                    continue;
                }
                outp.Add(i);
            }
        }
        return outp;
    }

    /// <summary>
    /// Alt + click: one height for all, from the top of the piece clicked (to build on it) or the ground.
    /// </summary>
    /// <param name="thing">The thing clicked, if any.</param>
    /// <param name="groundY">The height clicked on the ground.</param>
    /// <returns>The line for the status bar.</returns>
    public string PickElevation(int? thing, float groundY)
    {
        var s = Scene();
        float y = groundY;
        string where = " (the ground there)";
        if (s != null && thing is int i && PieceCatalog.Get(s.Things[i].Prefab) is { Snaps.Length: > 0 } info)
        {
            var t = s.Things[i];
            y = info.Snaps.Max(p => RotU(new Vector3(p[0], p[1], p[2]), t.Rotation).Y) + t.Position.Y;
            where = $" (the top of {NameOf(t.Prefab) ?? info.Name})";
        }
        Elevation = Elevations.At;
        Elev = MathF.Round(y, 2);
        Notify();
        return $"Elevation: everything at {Elev:0.00} m{where}. PgUp / PgDn change it.";
    }

    /// <summary>
    /// PgUp / PgDn: the elevation up or down by <c>d</c> metres; from the ground they start lifting above it. Returns
    /// the line for the status bar.
    /// </summary>
    public string NudgeElevation(float d)
    {
        if (Elevation == Elevations.Ground)
        {
            Elevation = Elevations.Above;
            Elev = 0;
        }
        Elev = MathF.Round(Elev + d, 2);
        Notify();
        return $"Elevation: {(Elevation == Elevations.At ? "at" : "above the ground by")} {Elev} m.";
    }

    /// <summary>A preset: kinds with their weights and the scatter settings.</summary>
    /// <param name="Name">The preset as listed.</param>
    /// <param name="Kinds">The kinds by prefab name, with their weights.</param>
    /// <param name="Density">Objects per 100 m².</param>
    /// <param name="Spacing">The least distance between objects, in metres.</param>
    /// <param name="SizeMin">Smallest scale, in percent.</param>
    /// <param name="SizeMax">Largest scale, in percent.</param>
    /// <param name="Tilt">Most random lean, in degrees.</param>
    /// <param name="Clump">Clumping, 0 to 100%.</param>
    /// <param name="Patch">The clumping pattern's size, in metres.</param>
    public sealed record Preset(string Name, Dictionary<string, int> Kinds, float Density, float Spacing, float SizeMin, float SizeMax, float Tilt, float Clump = 0, float Patch = 30);

    /// <summary>The presets that come with the editor.</summary>
    public static readonly Preset[] BuiltIn =
    [
        new("Meadows woods", new() { ["Beech1"] = 4, ["Birch1"] = 1, ["Birch2"] = 1, ["Oak1"] = 1, ["Bush01"] = 2 }, 3, 4, 80, 120, 3, 50, 35),
        new("Black forest", new() { ["FirTree"] = 3, ["Pinetree_01"] = 3, ["FirTree_big"] = 1, ["Bush01"] = 1 }, 4, 3.5f, 80, 130, 2, 30, 40),
        new("Swamp", new() { ["SwampTree1"] = 3, ["SwampTree2"] = 1 }, 2, 5, 80, 120, 4),
        new("Berry patch", new() { ["RaspberryBush"] = 2, ["BlueberryBush"] = 1 }, 6, 3, 90, 110, 0, 70, 15),
        new("Forest floor", new() { ["Pickable_Branch"] = 3, ["Pickable_Stone"] = 2, ["Pickable_Mushroom"] = 1, ["Pickable_Dandelion"] = 1 }, 2, 3, 100, 100, 0),
        new("Meadows rocks", new() { ["Rock_3"] = 2, ["Rock_4"] = 1, ["Rock_7"] = 1 }, 1, 6, 60, 140, 10, 40, 25),
    ];

    /// <summary>Loads a preset (kinds the world cannot make are left out). Returns what to say.</summary>
    /// <param name="p">The preset.</param>
    /// <param name="canMake">Whether the world can make a kind (by prefab name).</param>
    public string Load(Preset p, Func<string, bool> canMake)
    {
        var kinds = p.Kinds.Where(k => canMake(k.Key)).ToList();
        if (kinds.Count == 0)
        {
            return $"None of the kinds of {p.Name} can be placed in this world.";
        }
        Chosen.Clear();
        foreach (var (n, w) in kinds)
        {
            Chosen.Add(n);
            Weights[n] = w;
        }
        (Density, Spacing, SizeMin, SizeMax, Tilt, Clump, Patch, RandomYaw) = (p.Density, p.Spacing, p.SizeMin, p.SizeMax, p.Tilt, p.Clump, p.Patch, true);
        AutoSnap();
        NewLayout();
        int left = p.Kinds.Count - kinds.Count;
        return $"Preset {p.Name}: {string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}"))}{(left > 0 ? $" ({left} kind(s) this world cannot place left out)" : "")}.";
    }
}
