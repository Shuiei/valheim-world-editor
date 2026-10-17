using System.Numerics;
using Silk.NET.OpenGL;

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

    /// <summary>The six planes of a view and projection (row vectors), each facing into the view.</summary>
    /// <param name="vp">The view and projection.</param>
    /// <param name="planes">Where the planes go (six).</param>
    internal static void ViewPlanes(Matrix4x4 vp, Span<Vector4> planes)
    {
        // The fourth column plus or minus each of the others.
        planes[0] = new(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
        planes[1] = new(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
        planes[2] = new(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
        planes[3] = new(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
        planes[4] = new(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
        planes[5] = new(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);
    }

    /// <summary>
    /// Whether a box may show in a view (<see cref="ViewPlanes"/>): false only when it is wholly behind one of the
    /// planes, so a box partly in view counts as in it. An empty box (min past max) is never in view.
    /// </summary>
    /// <param name="planes">The view's six planes.</param>
    /// <param name="min">The box's low corner.</param>
    /// <param name="max">The box's high corner.</param>
    internal static bool InView(ReadOnlySpan<Vector4> planes, Vector3 min, Vector3 max)
    {
        if (min.X > max.X)
        {
            return false;
        }
        foreach (var pl in planes)
        {
            // The box's corner farthest along the plane's normal: outside when even it is behind.
            var far = new Vector3(pl.X >= 0 ? max.X : min.X, pl.Y >= 0 ? max.Y : min.Y, pl.Z >= 0 ? max.Z : min.Z);
            if (pl.X * far.X + pl.Y * far.Y + pl.Z * far.Z + pl.W < 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The box a model's instance covers: its model's box (<paramref name="box"/>) placed by
    /// <paramref name="placement"/>, or a metre round where it stands while that box is not known.
    /// </summary>
    internal static (Vector3 Min, Vector3 Max) PlacedBox((Vector3 Min, Vector3 Max)? box, Matrix4x4 placement) =>
        box is { } b ? Picking.Transform(b.Min, b.Max, placement) : (placement.Translation - Vector3.One, placement.Translation + Vector3.One);

    /// <summary>Grows a cell's box to hold <paramref name="box"/> (cells' boxes only grow, until they are made again).</summary>
    internal static void Grow((Vector3 Min, Vector3 Max)[] cellBounds, int cell, (Vector3 Min, Vector3 Max) box)
    {
        var (min, max) = cellBounds[cell];
        cellBounds[cell] = (Vector3.Min(min, box.Min), Vector3.Max(max, box.Max));
    }

    /// <summary>
    /// Instances' runs by cell, in their order: each run the instances one after another in the same cell (one run per
    /// cell when they are sorted by cell).
    /// </summary>
    /// <param name="cells">Each instance's cell, in the order of the instances.</param>
    internal static (int Cell, int Start, int Count)[] Runs(IReadOnlyList<int> cells)
    {
        var runs = new List<(int Cell, int Start, int Count)>();
        for (int k = 0; k < cells.Count; k++)
        {
            if (runs.Count > 0 && runs[^1].Cell == cells[k])
            {
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
            }
            else
            {
                runs.Add((cells[k], k, 1));
            }
        }
        return [.. runs];
    }

    /// <summary>A group's instances (and its things' boxes) from where its things are now.</summary>
    private unsafe void Rebuild(WorldScene s, Group g)
    {
        EnsureCells(s);
        var list = new List<(int Cell, Matrix4x4 M, Vector3 Tint)>();
        var support = _support;
        var previews = _previews;
        // The largest scale an instance is drawn at (its own, or the model's when it has none): culling reaches as
        // far as the largest one needs.
        float rootMost = MathF.Max(g.RootScale.X, MathF.Max(g.RootScale.Y, g.RootScale.Z)), most = 0;
        lock (s.Things)
        {
            lock (_objLock)
            {
                foreach (int i in g.Things)
                {
                    var t = previews.TryGetValue(i, out var p) ? p : s.Things[i];
                    var m = Placement(s, t, g.RootScale);
                    // Without its model's box yet: a metre around it (the stand-in box).
                    var bounds = PlacedBox(g.Box, m);
                    if (g.Box != null)
                    {
                        _bounds[i] = bounds;
                        _boundsAt[i] = t.Position;
                    }
                    bool known = !t.Gone && g.Box != null;
                    // A box known for the first time: the selection's outline (which left it out) is made again.
                    _selectionDirty |= known && !_known[i];
                    _known[i] = known;
                    if (!t.Gone)
                    {
                        int cell = CellOf(s, m.Translation);
                        Grow(_cellBounds, cell, bounds);
                        list.Add((cell, m, support != null && support.TryGetValue(i, out float sv) ? SupportTint(sv) : Vector3.Zero));
                        most = MathF.Max(most, t.Scale > 0 ? t.Scale : rootMost);
                    }
                }
            }
        }
        // By cell, so each cell's instances are one run of the buffer.
        list.Sort((a, b) => a.Cell.CompareTo(b.Cell));
        var cells = Runs([.. list.Select(e => e.Cell)]);
        float size = g.Box is { } gb ? Vector3.Distance(gb.Min, gb.Max) * (list.Count > 0 ? most : rootMost) : 1;
        var data = new Matrix4x4[list.Count];
        foreach (var (b, pre) in g.Batches)
        {
            for (int k = 0; k < list.Count; k++)
            {
                var w = pre * list[k].M;
                (w.M14, w.M24, w.M34) = (list[k].Tint.X, list[k].Tint.Y, list[k].Tint.Z);
                data[k] = w;
            }
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
            fixed (Matrix4x4* ptr = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), ptr, BufferUsageARB.DynamicDraw);
            }
            b.Instances = data.Length;
            b.Cells = [.. cells];
            b.Size = size;
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

    /// <summary>
    /// Draws the runs of a batch's instances (its vertex array bound) whose cell shows (<see cref="Shows"/>), each
    /// row of neighbouring runs at once, or all of them at once when they are few and any shows. OpenGL ES 3.0 and
    /// OpenGL 4.0 cannot start at an instance, so the instance attributes are pointed at the row's first one. Returns
    /// how many instances were drawn.
    /// </summary>
    /// <param name="b">The batch.</param>
    /// <param name="vbo">The buffer of the instances: the batch's own, or another one (pointed at anew).</param>
    /// <param name="instances">How many instances it holds.</param>
    /// <param name="runs">The instances' runs by cell (see <see cref="Runs"/>).</param>
    /// <param name="reach">How far from the eye a cell's instances are drawn.</param>
    /// <param name="calls">The draw calls made.</param>
    private unsafe int DrawRuns(Batch b, uint vbo, int instances, (int Cell, int Start, int Count)[] runs, float reach, out int calls)
    {
        bool own = vbo == b.InstanceVbo;
        int pointed = own ? b.PointedAt : -1;
        int start = -1, count = 0, drawn = 0, made = 0;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        void Flush()
        {
            if (count == 0)
            {
                return;
            }
            if (pointed != start)
            {
                PointInstances(start * 64L);
                pointed = start;
            }
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
            made++;
            drawn += count;
            count = 0;
        }
        if (instances <= SmallBatch || runs.Length == 1)
        {
            if (runs.Any(r => Shows(r.Cell, reach)))
            {
                start = 0;
                count = instances;
                Flush();
            }
        }
        else
        {
            foreach (var (cell, first, n) in runs)
            {
                if (!Shows(cell, reach))
                {
                    Flush();
                    continue;
                }
                if (count > 0 && start + count == first)
                {
                    count += n;
                }
                else
                {
                    Flush();
                    start = first;
                    count = n;
                }
            }
            Flush();
        }
        if (own)
        {
            b.PointedAt = pointed;
        }
        calls = made;
        return drawn;
    }
}
