using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Select tool, like the web editor's (editor/transform.js, editor/selecting.js): click objects to
/// select them (Shift adds), drag on empty ground to draw a zone and select what is in it, drag a
/// selected object to move the selection, turn it (, .), lift it (PgUp PgDn), drop it onto what is
/// under it (End), type an exact place, and delete it (Del). A move is shown live; when it ends the
/// originals are replaced by copies at the new place (EditSession.Move), in one undo step.
/// </summary>
public sealed partial class SelectTool
{
    /// <summary>The 3D view whose scene and selection the tool works on.</summary>
    private readonly GlView _view;
    /// <summary>Degrees to radians.</summary>
    private const float D = MathF.PI / 180;

    /// <summary>The tool for one 3D view.</summary>
    public SelectTool(GlView view)
    {
        _view = view;
        _commitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _commitTimer.Tick += (_, _) => Commit();
    }

    /// <summary>What to tell the user, in both languages (the flag (warn): shown as a problem).</summary>
    public event Action<Said, bool>? Message;
    /// <summary>
    /// Put each moved object on what is under it where it ends up: a floor, a table, another object, else the ground
    /// (see Landed; pieces as one block: see GroundBlock).
    /// </summary>
    public bool OnGround { get; set; } = true;
    /// <summary>Snap moved building pieces to the pieces around.</summary>
    public bool SnapToPieces { get; set; } = true;

    /// <summary>The view's open scene.</summary>
    private WorldScene? Scene => _view.Scene;
    /// <summary>The open scene's edit session.</summary>
    private EditSession? Session => _view.Scene?.Session;

    // ---- Where things are: world coordinates (x east, z north), heights in metres.

    /// <summary>A building piece: read as one, or a prefab the piece catalogue knows (added pieces).</summary>
    private static bool IsPiece(WorldScene.Thing t) => t.Piece || PieceCatalog.Get(t.Prefab) != null;

    /// <summary>A piece's snap points in the world, placed at <paramref name="at"/> turned by <paramref name="rot"/> (Euler degrees).</summary>
    private static IEnumerable<Vector3> SnapsAt(int prefab, Vector3 at, Vector3 rot) =>
        (PieceCatalog.Get(prefab)?.Snaps ?? []).Select(p => at + PlaceTool.RotU(new Vector3(p[0], p[1], p[2]), rot));

    // ---- A move in progress.
    /// <summary>One object being moved.</summary>
    internal sealed class Item
    {
        /// <summary>Its index in the scene's Things.</summary>
        public int Index;
        /// <summary>The object as it was when the move began.</summary>
        public WorldScene.Thing Thing;
        /// <summary>Height above the ground; a piece's bottom below its origin (from its snap points).</summary>
        public float Lift, Bottom;
        /// <summary>A building piece (moves with the block on the ground, can snap).</summary>
        public bool Piece;
        /// <summary>A part of a linked group (ObjectLinks: a ship): moves with it as one, keeping its height (a ship stays afloat).</summary>
        public bool Linked;
    }

    /// <summary>A move of the selection, shown live until it is committed.</summary>
    internal sealed class Move
    {
        /// <summary>What is moved (runestones and gone objects left out).</summary>
        public required List<Item> Items;
        /// <summary>
        /// The selection's middle when the move began (Cx, Cz: turns go around it); how far it moved (Dx, Dz) and was
        /// lifted (Dy), in metres; the turn in degrees (-180..180); the snap's shift (Sx, Sy, Sz); how far the
        /// pieces' block goes up or down to sit on the ground, or the whole selection to follow the ground under its
        /// middle when it is not put on the ground (Block).
        /// </summary>
        public float Cx, Cz, Dx, Dz, Dy, Turn, Sx, Sy, Sz, Block;
        /// <summary>Dropped with End (each item keeps the height it dropped to); typed exactly (no snapping).</summary>
        public bool Dropped, NoSnap;
        /// <summary>
        /// Whether Claude started the move (setting a control: ClaudeTools.Acting): its step is Claude's whenever it
        /// is committed, and a move the user started is the user's even when a control Claude sets commits it.
        /// </summary>
        public bool ByClaude;
        /// <summary>The name of the piece the selection snapped to, for the message; null when not snapped.</summary>
        public string? Snapped;
        /// <summary>
        /// The snap points of the pieces around (within 60 m of where the selection's middle was when they were
        /// gathered: Ox, Oz), gathered again once the middle is more than 30 m from there.
        /// </summary>
        public List<(Vector3 P, string Name)>? Others;
        /// <summary>
        /// <see cref="Others"/> by the metre cell (x, z) each point is in, as indices into it in its order: a snap within
        /// 0.75 m is in the cell of the moved point or one beside it.
        /// </summary>
        public Dictionary<(int X, int Z), List<int>> OthersAt = [];
        /// <summary>Where the selection's middle was when <see cref="Others"/> were gathered.</summary>
        public float Ox, Oz;
        /// <summary>How many whole linked groups the move takes (their parts were added to the selection's).</summary>
        public int Groups;
        /// <summary>What such a group is called ("vehicle").</summary>
        public string Noun = LinkRule.DefaultName;
        /// <summary>The boxes of the drawn objects the moved ones can land on (not the moved ones), gathered at the first landing.</summary>
        public DropBoxes? Under;
    }

    /// <summary>The move in progress, if any.</summary>
    private Move? _move;
    /// <summary>Commits a key-driven move (turn, lift, drop) once the keys stop for 700 ms.</summary>
    private readonly DispatcherTimer _commitTimer;
    /// <summary>A move is in progress (not committed yet).</summary>
    public bool Moving => _move != null;

    /// <summary>A turn smaller than this (degrees) is none: float residue, not a move, and not said.</summary>
    private const float NoTurn = 0.001f;
    /// <summary>A lift smaller than this (metres) is none: float residue, not a move, and not said.</summary>
    private const float NoLift = 0.001f;

    /// <summary>
    /// The boxes (view space) of the drawn objects a drop can land on, by the 4 m cell (x, z) they cover, so each point
    /// of a drop looks only at the boxes over it.
    /// </summary>
    internal sealed class DropBoxes
    {
        /// <summary>The cell size, in metres.</summary>
        private const float Cell = 4;
        /// <summary>The boxes over each cell.</summary>
        private readonly Dictionary<(int X, int Z), List<(Vector3 Lo, Vector3 Hi)>> _cells = [];
        /// <summary>Boxes too wide (or not finite) to file by cell: looked at for every point.</summary>
        private readonly List<(Vector3 Lo, Vector3 Hi)> _everywhere = [];

        /// <summary>Files a box under each cell it covers.</summary>
        public void Add(Vector3 lo, Vector3 hi)
        {
            float x0 = MathF.Floor(lo.X / Cell), x1 = MathF.Floor(hi.X / Cell), z0 = MathF.Floor(lo.Z / Cell), z1 = MathF.Floor(hi.Z / Cell);
            if (!float.IsFinite(x0 + x1 + z0 + z1) || (x1 - x0 + 1) * (z1 - z0 + 1) > 1024)
            {
                _everywhere.Add((lo, hi));
                return;
            }
            for (int cz = (int)z0; cz <= (int)z1; cz++)
            {
                for (int cx = (int)x0; cx <= (int)x1; cx++)
                {
                    (_cells.TryGetValue((cx, cz), out var l) ? l : _cells[(cx, cz)] = []).Add((lo, hi));
                }
            }
        }

        /// <summary>The boxes that may be over the point (x, z): those of its cell and the ones filed everywhere.</summary>
        public IEnumerable<(Vector3 Lo, Vector3 Hi)> At(float x, float z) =>
            float.IsFinite(x) && float.IsFinite(z) && _cells.TryGetValue(((int)MathF.Floor(x / Cell), (int)MathF.Floor(z / Cell)), out var l) ? l.Concat(_everywhere) : _everywhere;
    }

    // ---- Exact place: the selection's middle (height: its lowest object) and the first one's turn.

    // ---- More ways to select.

    // ---- The handles (Gizmo): the one under the mouse or being dragged is drawn highlighted.
    /// <summary>The handle under the mouse, if any.</summary>
    private Gizmo.Handle? _hover;
    /// <summary>
    /// A handle being dragged: which, the gizmo's centre, where along its axis (or heading round the ring) the drag
    /// began, and the move's offsets and turn then.
    /// </summary>
    private (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn)? _handleDrag;
    /// <summary>The handle to draw highlighted: the one dragged, else the one under the mouse.</summary>
    public Gizmo.Handle? HotHandle => _handleDrag?.Handle ?? _hover;

    /// <summary>The pointer is over a move handle (a click there drags, it does not pick).</summary>
    public bool OverHandle => _hover != null;

    // ---- The mouse (left button; the view passes it on in the Select tool).
    /// <summary>A press on a selected object: where on screen, and the ground point under it.</summary>
    private (Point At, Vector3 From)? _drag;
    /// <summary>That press moved far enough (5 px) to be a move, not a click.</summary>
    private bool _dragMoving;
    /// <summary>
    /// A press on empty ground: where on screen, the zone's ground points (x, z), whether it adds to the selection
    /// (Shift), and whether it is being drawn (moved past 6 px) rather than a click.
    /// </summary>
    private (Point At, List<Vector2> Points, bool Add, bool Drawing)? _lasso;
    /// <summary>A copy of the zone's points for the drawing thread.</summary>
    public Vector2[]? Lasso { get; private set; }

    /// <summary>Shift (not Ctrl) held: a click takes the row from the last object clicked (GlView.Pick).</summary>
    public bool Range { get; set; }
}
