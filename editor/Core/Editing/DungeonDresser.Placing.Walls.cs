namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A column of the material's segments from floor to roof at (u, v), keeping the floor round it: the last may rise
    /// into the roof slab, never past it into the room above.
    /// </summary>

    private void Column(Frame f, float u, float v)
    {
        if (!f.Inside(u, v))
        {
            return;
        }
        var p = f.W(u, v, 0);
        DungeonShell.Stack(_mat.Column, _o, p.X, p.Z, f.F, f.F + f.H);
        f.Take(u, v, 0.8f);
    }

    /// <summary>Up to three shelves against the walls, each spot at least clear metres from the corners.</summary>
    private void Shelves(Frame f, float clear)
    {
        foreach (var s in f.WallSpots(clear, 2.8f).OrderBy(_ => _rnd.Next()).Take(3))
        {
            var (u, v) = In(s, 0.12f);
            Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
        }
    }

    /// <summary>A wall of the material inside a room: across (u0..u1 at depth v) or along (v0..v1 at u), y0..y1 above the floor.</summary>
    private void InnerWall(Frame f, float u0, float v0, float u1, float v1, float y0, float y1)
    {
        var a = f.W(u0, v0, 0);
        var b = f.W(u1, v1, 0);
        bool alongZ = MathF.Abs(a.X - b.X) < 0.01f;
        float line = alongZ ? a.X : a.Z, t0 = alongZ ? Math.Min(a.Z, b.Z) : Math.Min(a.X, b.X), t1 = alongZ ? Math.Max(a.Z, b.Z) : Math.Max(a.X, b.X);
        DungeonShell.Fill(_mat, _o, alongZ, line, t0, t1, f.F + y0, f.F + y1);
    }
}
