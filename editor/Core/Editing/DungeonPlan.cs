using TerrainEditor.App;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// The layout of a generated dungeon: levels of rooms and corridors on a 2 m grid (tiles), and the
/// doorways between them. Rooms are boxes, entered in the middle of a side (Dir: the way in) and
/// joined to the room they come from wall to wall or by a straight corridor, so the plan reads like
/// a building: axes, wings, twins on both sides; some take another shape within their box (a corner
/// cut away, the corners notched, an apse). Then loops: rooms far apart on the way that happen to
/// lie close are joined, so there is more than one way round (Jaquays). Floor and roof slabs cover
/// whole 8 m cells, so what needs a cell to itself (a stairwell, a hall two floors high) is aligned to
/// cells and keeps them. Level k's floor is 6k m below the first.
/// </summary>
internal sealed class DungeonPlan
{
    /// <summary>The shape of a room's floor within its box.</summary>
    public enum Shape
    {
        /// <summary>The whole box.</summary>
        Rect,
        /// <summary>The four corners notched: a cross, an octagon's feel.</summary>
        Cross,
        /// <summary>The two far corners notched: the far end narrows like an apse.</summary>
        Apse,
        /// <summary>One near corner cut away: an L.</summary>
        L,
        /// <summary>Both near corners cut away: a neck at the way in, then the full width.</summary>
        T,
    }

    /// <summary>A room or a corridor of the plan.</summary>
    public sealed class Space
    {
        /// <summary>Its floor's shape within its box (a corridor: whatever its tiles are).</summary>
        public Shape Shape;
        /// <summary>A floor finish of its own over the marble (null: the material's, if any), and how far its top is above the marble.</summary>
        public string? Floor;
        public float FloorTop;
        /// <summary>Its number in the order spaces were made.</summary>
        public int Id;
        /// <summary>The level it is on, 0 the first.</summary>
        public int Level;
        /// <summary>What kind of room it is (a corridor is a Hub with <see cref="Corridor"/> set).</summary>
        public Room Room;
        /// <summary>As shown on the plan's map and in the notes.</summary>
        public string Name = "";
        /// <summary>A corridor rather than a room.</summary>
        public bool Corridor;
        /// <summary>Rises through two floors (aligned to cells, nothing above it).</summary>
        public bool Tall;
        /// <summary>On the main way through (the entrance, the way's main rooms, both ends of the stairs).</summary>
        public bool Main;
        /// <summary>Holds the chest with the boss gate's key.</summary>
        public bool KeyChest;
        /// <summary>The tiles it covers (for a room, its whole box).</summary>
        public readonly HashSet<(int I, int J)> Tiles = [];
        /// <summary>A corridor that leads nowhere: one doorway, a cache at its far end.</summary>
        public bool DeadEnd;
        /// <summary>Its recesses (alcoves off a room, niches off a corridor): each one's tiles and the way out of the space into it.</summary>
        public readonly List<(List<(int I, int J)> Tiles, (int X, int Z) Out)> Recesses = [];
        /// <summary>Its box (tiles, world axes), grown to hold its recesses.</summary>
        public int X0, Z0, W, H;
        /// <summary>Its box before recesses grew it (null: it has none).</summary>
        public (int X0, int Z0, int W, int H)? Before;
        /// <summary>The way in.</summary>
        public (int X, int Z) Dir;
        /// <summary>The doorways on its walls, to either side.</summary>
        public readonly List<Doorway> Doors = [];
        /// <summary>The space it was placed off (null for the entrance).</summary>
        public Space? Parent;
        /// <summary>How it was placed off its parent: shifted across, corridor tiles.</summary>
        public int Shift, Length;

        /// <summary>Whether it is a room (not a corridor).</summary>
        public bool IsRoom => !Corridor;

        /// <summary>Its own box (tiles, world axes): its walls without its recesses, the box its layout is made for.</summary>
        public (int X0, int Z0, int W, int H) Own => Before ?? (X0, Z0, W, H);

        /// <summary>The middle of its box along x (metres).</summary>
        public float CenterX => (X0 + W / 2f) * 2;

        /// <summary>The middle of its box along z (metres).</summary>
        public float CenterZ => (Z0 + H / 2f) * 2;

        /// <summary>Tiles across (perpendicular to Dir).</summary>
        public int Across => Dir.X != 0 ? H : W;

        /// <summary>Tiles deep (along Dir).</summary>
        public int Deep => Dir.X != 0 ? W : H;

        /// <summary>The tile at (a across from the left of the way in, b deep from the way in) of its box.</summary>
        public (int, int) TileAt(int a, int b) => Dir switch
        {
            (0, 1) => (X0 + a, Z0 + b),
            (0, -1) => (X0 + W - 1 - a, Z0 + H - 1 - b),
            (1, 0) => (X0 + b, Z0 + H - 1 - a),
            _ => (X0 + W - 1 - b, Z0 + a),
        };

        /// <summary>Whether a tile is one of its own.</summary>
        public bool Has((int, int) t) => Tiles.Contains(t);
    }

    /// <summary>A 4 m doorway: A's tiles T1 and T2 along its side, B beyond (Dir from A to B).</summary>
    public sealed class Doorway
    {
        /// <summary>The space it opens from, and the one beyond.</summary>
        public Space A = null!, B = null!;
        /// <summary>A's two tiles along the side it opens on.</summary>
        public (int I, int J) T1, T2;
        /// <summary>The way from A to B.</summary>
        public (int X, int Z) Dir;
        /// <summary>What stands in it.</summary>
        public Door Kind;
        /// <summary>Made to close a loop rather than to reach a new space.</summary>
        public bool Loop;

        /// <summary>The space on the other side from <paramref name="s"/>.</summary>
        public Space Other(Space s) => s == A ? B : A;

        /// <summary>The middle of the doorway (metres), on the wall line.</summary>
        public (float X, float Z) Center => (T1.I + T2.I + 1 + Dir.X, T1.J + T2.J + 1 + Dir.Z);
    }

    /// <summary>One floor of the dungeon.</summary>
    public sealed class Level
    {
        /// <summary>0 the first, counting down.</summary>
        public int Index;
        /// <summary>The space each taken tile belongs to.</summary>
        public readonly Dictionary<(int, int), Space> Owner = [];
        /// <summary>Cells with no floor: a stairwell.</summary>
        public readonly HashSet<(int, int)> Holes = [];
        /// <summary>Cells with no roof: under a stairwell.</summary>
        public readonly HashSet<(int, int)> Open = [];
        /// <summary>Cells a tall room rises through: nothing on the level above may stand in them.</summary>
        public readonly HashSet<(int, int)> TallCells = [];
        /// <summary>The level's rooms and corridors, in the order made.</summary>
        public readonly List<Space> Spaces = [];
    }

    /// <summary>The levels, first (top) to last.</summary>
    public readonly List<Level> Levels = [];
    /// <summary>Every space of every level, in the order made.</summary>
    public readonly List<Space> Spaces = [];
    /// <summary>Every doorway of every level.</summary>
    public readonly List<Doorway> Doors = [];
    /// <summary>What could not be made, for the user (and in English for Claude).</summary>
    public readonly List<Said> Notes = [];
    /// <summary>The entrance hall on the first level, at the origin.</summary>
    public Space Entrance = null!;
    /// <summary>The boss's arena and the treasury beyond it (null when there was no room for them).</summary>
    public Space? Boss, Treasury;

    /// <summary>The 8 m cell (4 × 4 tiles) a tile is in.</summary>
    public static (int, int) CellOf((int I, int J) t) => (FloorDiv(t.I, 4), FloorDiv(t.J, 4));

    /// <summary>Integer division rounding down, also for negatives.</summary>
    public static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

    /// <summary>The height of a level's floor (metres): 6 m below the one above.</summary>
    public static float FloorY(int level) => -6f * level;

    /// <summary>How high a space's walls go above its floor (metres; 0 for none).</summary>
    public static float HeightOf(Space? s) => s == null ? 0 : s.Tall ? 10 : 4;

    /// <summary>The space that owns a tile on a level, if any.</summary>
    public Space? At(int level, (int, int) tile) => Levels[level].Owner.GetValueOrDefault(tile);
}
