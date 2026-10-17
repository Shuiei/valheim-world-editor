namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A storeroom: barrels and crates along the walls with shelves; or stacked in a corner block with a cart; or
    /// shelves in rows forming aisles. Loot lying about, maybe a chest.
    /// </summary>
    private void Store(Frame f)
    {
        string[] stock = _mat.Name == "Dvergr" ? ["dvergrprops_barrel", "dvergrprops_crate", "dvergrprops_crate_long"] : ["barrell", "CargoCrate", "barrell", "prop_wood_stack"];
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(1.2f, 2.6f))
                {
                    var (u, v) = In(s, 0.6f);
                    if (Chance(0.75))
                    {
                        Put(f, One(stock), u, v, s.Yaw + R(-15, 15), 0.55f);
                    }
                }
                foreach (var s in f.WallSpots(2.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(2))
                {
                    var (u, v) = In(s, 0.12f);
                    Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.5f);
                }
                break;
            case 1:
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()).Take(2))
                {
                    for (float du = 0.6f; du <= 2.5f; du += 1.1f)
                    {
                        for (float dv = 0.6f; dv <= 2.5f; dv += 1.1f)
                        {
                            var (cu, cv) = (u + du * MathF.Sign(MathF.Sin(yaw * MathF.PI / 180) + 0.001f), v + dv * MathF.Sign(MathF.Cos(yaw * MathF.PI / 180) + 0.001f));
                            Put(f, One(stock), cu, cv, R(0, 360), 0.5f);
                        }
                    }
                }
                Put(f, "Cart", R(-1, 1), R(-1, 1), R(0, 360), 1.8f);
                break;
            default:
                for (float u = -f.HalfA + 1.6f; u <= f.HalfA - 1.6f; u += 3.2f)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
                    {
                        Put(f, Chance(0.7) ? "dvergrprops_shelf" : One(stock), u, v, 90, 0.5f);
                    }
                }
                break;
        }
        Pickables(f, 1);
        if (Chance(0.5))
        {
            ChestAgainstWall(f);
        }
        Webs(f, 0.3f);
        Lights();
        Foes(f, 0.3f);
        Decay();
    }
}
