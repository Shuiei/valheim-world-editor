namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A crossing: a statue, a ring of braziers, a column or an altar in the middle; columns round it if large.</summary>
    private void Hub(Frame f)
    {
        switch (_rnd.Next(4))
        {
            case 0:
                if (!(Chance(0.35) && PutHero(f, 0, 0, 180, 1.8f)))
                {
                    Put(f, Statue("StatueThor", "StatueFreya", "StatueDeer", "StatueSeed"), 0, 0, 180, 1.4f);
                }
                break;
            case 1:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    Brazier(f, 1.6f * MathF.Cos(a), 1.6f * MathF.Sin(a));
                }
                Put(f, "Morkhalla_giant_railing_deco", 0, 0, 0, 0.8f, y: 0.5f);
                break;
            case 2:
                Column(f, 0, 0);
                break;
            default:
                Put(f, "blackmarble_altar_crystal", 0, 0, 0, 1f, y: 0.9f);
                break;
        }
        if (f.S.Shape == DungeonPlan.Shape.Cross)
        {
            // Pilasters where the notches meet the walls.
            foreach (var (u, v, yaw, convex) in f.Corners().Where(c => !c.Convex))
            {
                var (cu, cv) = In((u, v, yaw), 0.5f);
                Column(f, cu, cv);
            }
        }
        else if (f.HalfA >= 5 && f.HalfD >= 5)
        {
            foreach (var (u, v) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
            {
                if (!f.NearDoor(u, v, 3))
                {
                    Column(f, u, v);
                }
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, StyleClusters);
        Lights();
        Foes(f, 0.6f);
        Clutter(f, 0.015f);
        Decay();
    }
}
