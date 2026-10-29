namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A study: shelves along the walls round a table; or shelves in rows forming aisles with a desk at the end; or a
    /// scriptorium of several desks with chairs and lanterns. A chest.
    /// </summary>
    private void Library(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(2, 2.8f))
                {
                    var (u, v) = In(s, 0.12f);
                    Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
                }
                Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, 0, 0, 0, force: true);
                Put(f, "dvergrprops_table", 0, 0, 0, 1.2f);
                Put(f, "dvergrprops_chair", 0, -1.1f, 0, 0.4f);
                Put(f, "dvergrprops_chair", 0, 1.1f, 180, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.5f, 0, 0, 0, y: 0.78f, force: true);
                Chest(f, 0, f.HalfD - 2.2f, 180);
                break;
            case 1:
                for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 3)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 3.5f; v += 1.1f)
                    {
                        Put(f, "dvergrprops_shelf", u, v, 90, 0.45f);
                    }
                }
                Put(f, "dvergrprops_table", 0, f.HalfD - 1.5f, 90, 1.2f);
                Put(f, "dvergrprops_chair", 0, f.HalfD - 2.6f, 0, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.6f, f.HalfD - 1.5f, 0, 0, y: 0.78f, force: true);
                Chest(f, -(f.HalfA - 1), f.HalfD - 1, 180);
                break;
            default:
                foreach (var (u, v) in new[] { (-2.2f, -1.5f), (2.2f, -1.5f), (-2.2f, 1.5f), (2.2f, 1.5f) })
                {
                    if (Put(f, "piece_table", u, v, 0, 1f))
                    {
                        Put(f, "piece_chair", u, v - 1f, 0, 0.3f);
                        Put(f, "piece_hoodedlantern", u + 0.5f, v, 0, 0, y: 0.82f, force: true);
                    }
                }
                Shelves(f, 2.4f);
                Chest(f, 0, f.HalfD - 0.9f, 180);
                break;
        }
        Clusters(f, 1, "scribe");
        Lights();
        Foes(f, 0.3f);
        Decay();
    }
}
