namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// The tiles of a room of this shape in a box entered along dir: the whole box less the corners the shape
    /// cuts (n tiles square: 1, or 2 in a big room), never the two middle tiles at the way in.
    /// </summary>
    /// <param name="box">The room's box.</param>
    /// <param name="dir">The way in.</param>
    /// <param name="shape">Its shape.</param>
    public static List<(int, int)> ShapeTiles((int X0, int Z0, int W, int H) box, (int X, int Z) dir, DungeonPlan.Shape shape)
    {
        var probe = new DungeonPlan.Space { X0 = box.X0, Z0 = box.Z0, W = box.W, H = box.H, Dir = dir };
        int across = probe.Across, deep = probe.Deep;
        int n = across >= 10 && deep >= 10 ? 2 : 1;
        bool Cut(int a, int b) => shape switch
        {
            DungeonPlan.Shape.Cross => (a < n || a >= across - n) && (b < n || b >= deep - n),
            DungeonPlan.Shape.Apse => (a < n || a >= across - n) && b >= deep - n,
            DungeonPlan.Shape.L => a < across / 2 - 1 && b < deep / 2,
            DungeonPlan.Shape.T => (a < across / 2 - 1 || a > across / 2) && b < deep / 2,
            _ => false,
        };
        var tiles = new List<(int, int)>();
        for (int a = 0; a < across; a++)
        {
            for (int b = 0; b < deep; b++)
            {
                if (!Cut(a, b))
                {
                    tiles.Add(probe.TileAt(a, b));
                }
            }
        }
        return tiles;
    }
}
