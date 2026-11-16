using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Snapping: the closest pair of snap points (one of a moved piece, one of a piece around) within
    /// 0.75 m; the whole selection shifts so they meet. Not snapped, moved pieces go by the grid when it is on
    /// (<see cref="GridSnap"/>).
    /// </summary>
    private void Snap(Move m)
    {
        m.Sx = m.Sy = m.Sz = 0;
        m.Snapped = null;
        if (m.NoSnap || m.Dropped)
        {
            return;
        }
        if (!SnapToPieces || !m.Items.Any(it => !it.Linked && PieceCatalog.Get(it.Thing.Prefab)?.Snaps.Length > 0))
        {
            GridSnap(m);
            return;
        }
        // The pieces around where the selection is now; gathered again once it has moved far from where they were
        // gathered, so a long drag still snaps to what stands at its end.
        float cx = m.Cx + m.Dx, cz = m.Cz + m.Dz;
        if (m.Others == null || MathF.Sqrt(MathF.Pow(cx - m.Ox, 2) + MathF.Pow(cz - m.Oz, 2)) > 30)
        {
            GatherOthers(m, cx, cz);
        }
        if (NearestSnap(m) is not { } b)
        {
            GridSnap(m);
            return;
        }
        m.Sx = b.O.X - b.M.X;
        m.Sz = b.O.Z - b.M.Z;
        // Heights follow the ground where the pieces end up, so the height is matched after the shift.
        var first = m.Items.First(it => !it.Linked && PieceCatalog.Get(it.Thing.Prefab)?.Snaps.Length > 0);
        float before = Target(m, first, shift: false).Y;
        m.Sy = 0;
        float after = Target(m, first).Y;
        m.Sy = b.O.Y - b.M.Y - (after - before);
        m.Snapped = b.Name;
    }

    /// <summary>
    /// The grid (the building bar's, 0: off): moved across the ground, a selection with building pieces shifts so that
    /// its first piece stands on the grid, counted from the area's middle as Build counts it. Turning and lifting on
    /// the spot leave it where it is.
    /// </summary>
    private void GridSnap(Move m)
    {
        if (GridStep <= 0 || MathF.Abs(m.Dx) + MathF.Abs(m.Dz) < 0.01f || Scene is not { } s || m.Items.FirstOrDefault(it => !it.Linked && it.Piece) is not { } first)
        {
            return;
        }
        var at = Target(m, first, shift: false);
        float mx = s.OriginX + (s.W - 1) / 2f, mz = s.OriginZ + (s.H - 1) / 2f;
        m.Sx = mx + MathF.Round((at.X - mx) / GridStep) * GridStep - at.X;
        m.Sz = mz + MathF.Round((at.Z - mz) / GridStep) * GridStep - at.Z;
    }
}
