using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool, like the web editor's (editor.html, Path tool): draw a line, then apply an action along it with a
/// width and a soft edge: flatten to a height, ramp from start to end, raise or lower, smooth, dig a river, dig a cave,
/// or paint. The line is kept after applying, so another action can follow. Cave: a trench along the line (its floor
/// follows the ground's lie below it, deepest in the middle, with an entrance slope at each end), roofed over with the
/// game's boulders where it is deep enough (CaveRoof): the ground cannot overhang (one height per point), so the roof
/// is rocks, like the game's own caves. Points are grid points of the block (x east, z north, one per metre).
/// </summary>
public sealed partial class PathTool
{
    /// <summary>What Apply does along the line; the panel lists them in this order.</summary>
    public enum Action
    {
        /// <summary>Flattens the ground to a height.</summary>
        Flatten,
        /// <summary>A slope from the start's height to the end's.</summary>
        Ramp,
        /// <summary>Raises the ground.</summary>
        Raise,
        /// <summary>Lowers the ground.</summary>
        Lower,
        /// <summary>Evens out bumps.</summary>
        Smooth,
        /// <summary>Digs a river bed.</summary>
        River,
        /// <summary>Digs a trench roofed over with the game's boulders.</summary>
        Cave,
        /// <summary>Paints dirt.</summary>
        PaintDirt,
        /// <summary>Paints paving.</summary>
        PaintPaved,
        /// <summary>Paints cultivated ground.</summary>
        PaintCultivated,
        /// <summary>Removes paint.</summary>
        PaintClear,
    }

    /// <summary>
    /// The boulders a cave's roof is made of. Auto picks them by the biome under each boulder; the others are the
    /// game's big world rocks of that biome (see <see cref="RoofRocks"/>).
    /// </summary>
    public enum CaveRock
    {
        /// <summary>Picked by the biome under each boulder.</summary>
        Auto,
        /// <summary>Forest boulders (rock4_forest); Auto's pick in most biomes.</summary>
        Forest,
        /// <summary>Coast boulders (rock4_coast); Auto's pick over Ocean.</summary>
        Coast,
        /// <summary>Heath boulders (rock4_heath); Auto's pick in the Plains.</summary>
        Heath,
        /// <summary>Mountain boulders (rock3_mountain); Auto's pick in the Mountains and Deep North.</summary>
        Mountain,
    }

    /// <summary>
    /// Each roof boulder's prefab, how wide it is at least (m, unscaled) and how far its lowest point is below its
    /// middle (CaveRoof hangs them by their real underside). These rocks keep a saved size. Auto has no entry: it
    /// stands for one of the others.
    /// </summary>
    public static readonly Dictionary<CaveRock, (string Prefab, float Footprint, float Bottom)> RoofRocks = new()
    {
        [CaveRock.Forest] = ("rock4_forest", 24.2f, 12.9f),
        [CaveRock.Coast] = ("rock4_coast", 24.2f, 12.9f),
        [CaveRock.Heath] = ("rock4_heath", 24.2f, 12.9f),
        [CaveRock.Mountain] = ("rock3_mountain", 12.7f, 8.2f),
    };
    /// <summary>The action Apply does.</summary>
    public Action Act { get; set; } = Action.Flatten;
    /// <summary>The full width of the path in metres.</summary>
    public float Width { get; set; } = 8;
    /// <summary>The soft edge in metres: how far beyond the width the action fades out (0: a hard edge).</summary>
    public float Soft { get; set; } = 4;
    /// <summary>Flatten: the height to flatten to, in metres.</summary>
    public float Height { get; set; } = 32;
    /// <summary>Raise and Lower: by how much, in metres.</summary>
    public float Amount { get; set; } = 2;
    /// <summary>Ramp: the height at the line's first point, in metres.</summary>
    public float Start { get; set; } = 32;
    /// <summary>Ramp: the height at the line's last point, in metres.</summary>
    public float End { get; set; } = 32;
    /// <summary>River: how far below the water level the bed goes in the middle, in metres.</summary>
    public float Depth { get; set; } = 2;
    /// <summary>
    /// Whether the line is a smooth curve through the points (with 3 or more points) instead of straight stretches.
    /// </summary>
    public bool Curved { get; set; } = true;
    /// <summary>Cave: depth below the ground in the middle, in metres.</summary>
    public float CaveDepth { get; set; } = 7.5f;
    /// <summary>Cave: headroom inside, in metres.</summary>
    public float Headroom { get; set; } = 4.5f;
    /// <summary>Cave: the roof's boulders.</summary>
    public CaveRock Rock { get; set; }
    /// <summary>Cave: the seed that turns and sizes the roof boulders.</summary>
    public int Seed { get; set; } = Random.Shared.Next();
    /// <summary>
    /// Natural look: the width varies along the line, the edges are ragged and the surface gets bumps (from the brush
    /// noise).
    /// </summary>
    public bool Natural { get; set; }
    /// <summary>
    /// Whether the ramp's ends were typed in or picked with Alt; until then they follow the ground at the line's ends.
    /// </summary>
    public bool RampEdited { get; set; }

    /// <summary>
    /// The points the user placed, in grid coordinates of the block. Changed under its own lock: the drawing thread
    /// reads it.
    /// </summary>
    public List<Vector2> Points { get; } = [];
    /// <summary>Something about the line or the values changed (to redraw and refill the panel).</summary>
    public event System.Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();
    // ---- The mouse: click points along the route, or hold and drag; drag a point to move it, drag the
    // line to add a point there, Ctrl + click a point to remove it; Alt + click picks the height (Alt +
    // Shift: the ramp's end). screen: where a grid point is on the view (null behind the camera).
    /// <summary>The point being dragged (index into <see cref="Points"/>), or null.</summary>
    private int? _drag;

    /// <summary>A drag is drawing the line now (the view then leaves out the cursor's preview).</summary>
    public bool Drawing { get; private set; }
    /// <summary>
    /// The mouse moved: moves the dragged point, or while drawing adds a point once the mouse is 2 m from the last one.
    /// </summary>
    public void Moved(Vector2? hit)
    {
        if (hit is not { } h)
        {
            return;
        }
        if (_drag is int i && i < Points.Count)
        {
            lock (Points) { Points[i] = h; }
            Notify();
        }
        else if (Drawing && (Points.Count == 0 || Vector2.Distance(Points[^1], h) >= 2))
        {
            lock (Points) { Points.Add(h); }
            Notify();
        }
    }

    /// <summary>The mouse was let go: ends any drag or drawing.</summary>
    public void Up()
    {
        _drag = null;
        Drawing = false;
    }

    /// <summary>Removes the line, and lets the ramp ends follow the ground again.</summary>
    public void Clear()
    {
        lock (Points) { Points.Clear(); }
        RampEdited = false;
        Notify();
    }

    /// <summary>Removes the last point (Backspace).</summary>
    public void RemoveLast()
    {
        if (Points.Count > 0)
        {
            lock (Points) { Points.RemoveAt(Points.Count - 1); }
            Notify();
        }
    }

    /// <summary>The point within 10 px of the mouse, or -1.</summary>
    public int PointAt(Point at, Func<Vector2, Point?> screen) => Lines.PointNear(Points, at, screen);

    /// <summary>
    /// The stretch of the line within 8 px of the mouse: the index of the point it starts from, or -1.
    /// </summary>
    public int SegmentAt(Point at, Func<Vector2, Point?> screen) => Lines.StretchNear(Curve(), at, screen);
}
