using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Snapping: the closest pair of snap points (one of a moved piece, one of a piece around) within
    /// 0.75 m; the whole selection shifts so they meet.
    /// </summary>
    private void Snap(Move m)
    {
        m.Sx = m.Sy = m.Sz = 0;
        m.Snapped = null;
        if (!SnapToPieces || m.NoSnap || m.Dropped || !m.Items.Any(it => !it.Linked && PieceCatalog.Get(it.Thing.Prefab)?.Snaps.Length > 0))
        {
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
}
