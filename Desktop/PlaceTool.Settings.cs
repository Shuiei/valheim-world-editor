using System.Numerics;

namespace TerrainEditor.Desktop;

// The settings the window and the panel set: the kind, the shape, the spacing, the elevation, and what the tool reports back.
public sealed partial class PlaceTool
{
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
}
