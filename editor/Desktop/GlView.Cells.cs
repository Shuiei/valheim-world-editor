using System.Numerics;

namespace TerrainEditor.Desktop;

// The area's cells: each batch's instances sorted by cell, so only the cells in view (and near enough for the
// model's size) are drawn.
public sealed partial class GlView
{
    /// <summary>The side of the area's cells objects are drawn by (metres): only cells in view are drawn.</summary>
    internal const float CellSize = 128;
    /// <summary>
    /// A model is not drawn in cells farther than this many times its size (a 1 m chest past 400 m is two or three
    /// pixels), and never nearer than <see cref="MinCullDistance"/>.
    /// </summary>
    internal const float CullPerMetre = 400;
    /// <summary>Models are drawn within this distance whatever their size.</summary>
    internal const float MinCullDistance = 150;
    /// <summary>The scene the cells were made for (another one makes them again).</summary>
    private WorldScene? _cellScene;
    /// <summary>The cells across and down the area.</summary>
    private int _cellsX, _cellsZ;
    /// <summary>How many times the cells were made (each time with empty boxes).</summary>
    private int _cellsMade;
    /// <summary>
    /// Each cell's box in view space: the boxes of every object put in it (grown only, until the area is read
    /// again), so a tall or wide one standing over the cell's edge is still drawn when it shows.
    /// </summary>
    private (Vector3 Min, Vector3 Max)[] _cellBounds = [];
    /// <summary>This frame: each cell in view or not, and its distance from the eye.</summary>
    private bool[] _cellSeen = [];
    /// <summary>This frame: each cell's distance from the eye (0 inside it).</summary>
    private float[] _cellDistance = [];

    /// <summary>The cells for this scene, made when it is new.</summary>
    private void EnsureCells(WorldScene s)
    {
        if (_cellScene == s)
        {
            return;
        }
        _cellScene = s;
        _cellsMade++;
        _cellsX = Math.Max(1, (int)MathF.Ceiling(s.W / CellSize));
        _cellsZ = Math.Max(1, (int)MathF.Ceiling(s.H / CellSize));
        _cellBounds = new (Vector3, Vector3)[_cellsX * _cellsZ];
        Array.Fill(_cellBounds, (new Vector3(float.MaxValue), new Vector3(float.MinValue)));
        _cellSeen = new bool[_cellBounds.Length];
        _cellDistance = new float[_cellBounds.Length];
    }

    /// <summary>The cell a view-space point is in (points outside go to the nearest edge cell).</summary>
    private int CellOf(WorldScene s, Vector3 p)
    {
        int x = Math.Clamp((int)MathF.Floor((p.X + (s.W - 1) / 2f) / CellSize), 0, _cellsX - 1);
        int z = Math.Clamp((int)MathF.Floor((p.Z + (s.H - 1) / 2f) / CellSize), 0, _cellsZ - 1);
        return z * _cellsX + x;
    }

    /// <summary>
    /// Which cells the camera sees this frame (their box against the view's six planes), and how far each is.
    /// </summary>
    private void SeeCells(Matrix4x4 vp, Vector3 eye)
    {
        Span<Vector4> planes = stackalloc Vector4[6];
        ViewPlanes(vp, planes);
        for (int c = 0; c < _cellBounds.Length; c++)
        {
            var (min, max) = _cellBounds[c];
            bool seen = InView(planes, min, max);
            _cellSeen[c] = seen;
            _cellDistance[c] = seen ? Vector3.Distance(eye, Vector3.Clamp(eye, min, max)) : float.MaxValue;
        }
    }

    /// <summary>Instances drawn in the last frame (after culling), for the frame-rate measure.</summary>
    private int _drawnInstances;
    /// <summary>Draw calls of objects in the last frame, for the frame-rate measure.</summary>
    private int _drawCalls;
    /// <summary>
    /// Batches with at most this many instances are drawn whole when any of their cells shows: splitting them
    /// costs more calls than the instances it saves.
    /// </summary>
    internal const int SmallBatch = 2048;

    /// <summary>
    /// Draws a batch's instances in the cells in view and near enough for its size (its vertex array bound). Returns
    /// how many instances were drawn.
    /// </summary>
    private int DrawCells(Batch b)
    {
        if (b.Instances == 0)
        {
            return 0;
        }
        int drawn = DrawRuns(b, b.InstanceVbo, b.Instances, b.Cells, MathF.Max(MinCullDistance, b.Size * CullPerMetre), out int calls);
        _drawCalls += calls;
        return drawn;
    }

    /// <summary>
    /// Whether a cell's instances are drawn this frame: in view and within <paramref name="reach"/> of the eye; always
    /// when the cells were not seen for this frame's area.
    /// </summary>
    private bool Shows(int cell, float reach) =>
        _cellSeen.Length != _cellsX * _cellsZ || _cellScene == null || cell >= _cellSeen.Length || _cellSeen[cell] && _cellDistance[cell] <= reach;
}
