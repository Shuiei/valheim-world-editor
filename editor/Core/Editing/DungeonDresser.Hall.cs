namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A great hall, one of several: colonnades with a set piece at the far end; a mead hall round a fire; a hall of
    /// the dead with a sarcophagus on a dais between the columns; a bare echoing hall with its pillars fallen.
    /// </summary>
    private void Hall(Frame f)
    {
        bool cols = f.HalfA >= 5;
        int variant = _rnd.Next(cols ? 4 : 2);
        switch (variant)
        {
            case 0:
                HallColonnades(f, cols);
                break;
            case 1:
                HallMead(f);
                break;
            case 2:
                HallDead(f);
                break;
            default:
                HallBare(f);
                break;
        }
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Clusters(f, 2, StyleClusters);
        Lights();
        Foes(f, 1.1f);
        Clutter(f, 0.012f);
        Decay();
    }

    /// <summary>
    /// The colonnade hall: columns down each side when the hall is wide enough, a feature at the far end, braziers and
    /// banners between the columns.
    /// </summary>
    private void HallColonnades(Frame f, bool cols)
    {
        if (Chance(0.5))
        {
            Loft(f);
        }
        if (cols)
        {
            for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
            {
                Pillar(f, -(f.HalfA - 2.2f), v);
                Pillar(f, f.HalfA - 2.2f, v);
            }
        }
        Feature(f, f.HalfD - 2.2f);
        Runner(f, -f.HalfD + 1.5f, f.HalfD - 4.2f);
        for (float v = -f.HalfD + 5; v <= f.HalfD - 5; v += 8)
        {
            Brazier(f, -(f.HalfA - 1.2f), v);
            Brazier(f, f.HalfA - 1.2f, v);
        }
        Hangings(f, (int)(f.HalfD / 3), Drapes);
    }
}
