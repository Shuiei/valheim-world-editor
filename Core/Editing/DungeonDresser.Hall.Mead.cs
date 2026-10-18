namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The mead hall: a fire in the middle, benches round it, trestle tables along the sides with their food and
    /// tankards, trophies and pilasters.
    /// </summary>
    private void HallMead(Frame f)
    {
        Put(f, One("prop_bonfire", "Morkhalla_firepit"), 0, -1, R(0, 360), 1.6f);
        foreach (int side in new[] { -1, 1 })
        {
            Put(f, "piece_bench01", side * 2.6f, -1, side < 0 ? 90 : -90, 0.5f);
            Put(f, "piece_logbench01", 0, -1 + side * 2.6f, side < 0 ? 0 : 180, 0.5f);
        }
        for (float v = 2.5f; v <= f.HalfD - 2.5f; v += 4)
        {
            foreach (int side in new[] { -1, 1 })
            {
                if (Put(f, "piece_table", side * (f.HalfA - 1.4f), v, 90, 1.1f))
                {
                    Put(f, Chance(0.6) ? FeastFood : "prop_Tankard", side * (f.HalfA - 1.4f), v, R(0, 360), 0, y: 0.82f, force: true);
                }
            }
        }
        Trophies(f, 3);
        Hangings(f, (int)(f.HalfD / 3), Drapes);
        Pilasters(f, 8);
    }
}
