namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The room's lights, after everything else: they take free spots but keep none, so that the settings' Light
    /// changes nothing but them (their stream is the room's own, their spots never move what comes later).
    /// </summary>
    private void PlaceLights(Frame f, bool important)
    {
        var rnd = Local(f, 1);
        if (_s.Light <= 0 && !important && (LightOf(f.S.Room) > 1 || rnd.NextDouble() >= 0.3))
        {
            return;
        }
        float step = (_s.Light >= 2 ? 5 : _s.Light == 1 ? 8 : 12) * LightOf(f.S.Room);
        // How the room is lit: standing torches along the walls, sconces on them, or a brazier or two in the corners.
        switch (rnd.Next(5))
        {
            case 0 or 1 when _mat.Name is "Stone" or "Marble" or "Grausten":
                WallTorches(f, step);
                break;
            case 2 when f.Area >= 40:
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).Take(_s.Light >= 2 ? 4 : 2))
                {
                    var (bu, bv) = In((u, v, yaw), 1.1f);
                    Brazier(f, bu, bv, take: false);
                }
                break;
            default:
                string lamp = Lamp(rnd);
                foreach (var s in f.WallSpots(step, 2.5f))
                {
                    var (u, v) = In(s, 0.7f);
                    Put(f, lamp, u, v, s.Yaw, 0.4f, take: false);
                }
                break;
        }
    }
}
