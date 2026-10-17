
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The arena, one of several: a ring of columns and braziers round a great rug with the boss upon it; the boss
    /// enthroned on a dais at the far end over heaps of skulls, braziers in rows before it; a crypt arena of columns
    /// on a grid, sarcophagi between them, chains above. Its followers about it.
    /// </summary>
    private void Boss(Frame f)
    {
        float r = Math.Min(f.HalfA, f.HalfD) - 3.5f;
        int variant = _rnd.Next(_mat.Name is "Stone" or "Grausten" or "Marble" ? 4 : 3);
        switch (variant)
        {
            case 3:
                // The pit: ledges round a sunken floor, columns at its corners, the master down in the middle. The columns
                // first: each ledge keeps 1.3 m round its tile, which took the columns' spots beside them.
                foreach (var (cu, cv) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    if (!f.NearDoor(cu * (f.Across - 3), cv * (f.Deep - 3), 2.6f))
                    {
                        Pillar(f, cu * (f.Across - 3), cv * (f.Deep - 3));
                    }
                }
                Ledges(f);
                if (f.S.Tall)
                {
                    _o.Add("StatueEvil", f.W(0, f.HalfD - 1.1f, f.F + 1 + Lift * 2 + DungeonProps.Foot("StatueEvil")), f.Yaw(180));
                }
                break;
            case 0:
                // A ring of columns, braziers between, the master in the middle.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4 + MathF.PI / 8;
                    float u = r * MathF.Cos(a), v = r * MathF.Sin(a);
                    if (!f.NearDoor(u, v, 3.5f))
                    {
                        Column(f, u, v);
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    Brazier(f, (r - 2.2f) * MathF.Cos(a), (r - 2.2f) * MathF.Sin(a));
                }
                break;
            case 1:
                // The dais at the far end, the boss's seat on it, rows of braziers and skulls before it.
                Plinth(f, 0, f.HalfD - 3, 8, 4);
                Steps(f, 0, f.HalfD - 6, 180, 4);
                string throne = PickThrone();
                _o.Add(throne, f.W(0, f.HalfD - 2.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(throne)));
                _o.Add("skull_pile", f.W(-2.6f, f.HalfD - 2.6f, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                _o.Add("skull_pile", f.W(2.6f, f.HalfD - 2.6f, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                {
                    Brazier(f, -(f.HalfA - 1.6f), v);
                    Brazier(f, f.HalfA - 1.6f, v);
                    Put(f, "skull_pile", -(f.HalfA - 3.2f), v + 2, R(0, 360), 0.5f);
                    Put(f, "skull_pile", f.HalfA - 3.2f, v + 2, R(0, 360), 0.5f);
                }
                Pilasters(f, 4);
                break;
            default:
                // Columns on a grid, sarcophagi between them, chains and cobwebs.
                for (float u = -f.HalfA + 3; u <= f.HalfA - 3; u += 4)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                    {
                        if (Math.Abs(u) < 2.5f && Math.Abs(v) < 2.5f || f.NearDoor(u, v, 3))
                        {
                            continue;
                        }
                        if (MathF.Abs(u) >= f.HalfA - 3.5f || MathF.Abs(v) >= f.HalfD - 3.5f)
                        {
                            Column(f, u, v);
                        }
                        else
                        {
                            Put(f, "crypt_skeleton_chest", u, v, R(0, 360), 1.2f);
                        }
                    }
                }
                Chains(f, 4);
                Webs(f, 0.6f);
                break;
        }
        if (f.S.Tall)
        {
            if (variant is not (1 or 3))
            {
                Put(f, "StatueEvil", 0, f.HalfD - 1.6f, 180, 1.6f);
            }
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3.4f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
            _o.Add("Morkhalla_StatueSword_hanging", f.W(0, f.HalfD - 5, f.Top + f.H - 2.7f), f.Yaw(0));
        }
        else if (variant != 1)
        {
            Put(f, One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, f.HalfD - 1.6f, 180, 1.5f);
        }
        // A great rug in the middle, the boss upon it; a giant's sword before the altar; banners.
        foreach (var (cu, cv, yaw) in new[] { (-2.18f, -2.18f, 0f), (2.18f, -2.18f, 90f), (2.18f, 2.18f, 180f), (-2.18f, 2.18f, 270f) })
        {
            _o.Add("Morkhalla_Rug_corner", f.W(cu, cv, f.Top + 0.06f), f.Yaw(yaw));
        }
        if (f.HalfA >= 10 && variant != 1)
        {
            _o.Add("Morkhalla_StatueSword", f.W(0, f.HalfD - 4.4f, f.Top + 0.3f), f.Yaw(R(-8, 8)));
            f.Take(-3, f.HalfD - 4.4f, 1.2f);
            f.Take(3, f.HalfD - 4.4f, 1.2f);
        }
        Hangings(f, 4, Drapes);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2;
            PutNear(f, "skull_pile", (r + 1.4f) * MathF.Cos(a), (r + 1.4f) * MathF.Sin(a), R(0, 360), 0.6f, 1f);
        }
        f.Take(0, 0, 1.5f);
        if (_boss != null)
        {
            Spawn(f, _boss, 0, 0.5f, 180, Math.Clamp(1 + (int)MathF.Round(_s.Monsters), 1, 3), once: true, force: true);
        }
        if (_s.Monsters > 0)
        {
            for (int i = 0, times = Count(2 * _s.Monsters); i < times; i++)
            {
                if (Spot(f, 0.7f, 2f) is var (u, v))
                {
                    Spawn(f, One(_foes), u, v, 180, 1, once: true);
                }
            }
        }
        Clutter(f, 0.012f, "Skull1", "LargeBone", "lox_ribs", "skull_pile");
        Lights(important: true);
    }
}
