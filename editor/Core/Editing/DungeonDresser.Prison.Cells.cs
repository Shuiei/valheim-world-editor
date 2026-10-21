namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A block of cells either side of an aisle 4 m wide, each 4 m along it behind a grate.</summary>
    private void Cells(Frame f)
    {
        float aisle = 2;
        int n = (int)MathF.Floor((2 * f.HalfD) / 4);
        float v0 = -n * 2f, end = f.HalfD + _mat.Thick / 2;
        for (int i = 0; i < n; i++)
        {
            float a = v0 + 4 * i, b = a + 4;
            foreach (int side in new[] { -1, 1 })
            {
                float front = side * aisle, back = side * f.HalfA;
                if (i > 0)
                {
                    InnerWall(f, Math.Min(front, back), a, Math.Max(front, back), a, 0, 4);
                }
                // The first and last cells' fronts run on to the end walls' middle: the cells take 4 m each, and the
                // strip the block leaves at each end was open to the aisle.
                InnerWall(f, front, i == 0 ? -end : a, front, a + 1, 0, 4);
                InnerWall(f, front, b - 1, front, i == n - 1 ? end : b, 0, 4);
                InnerWall(f, front, a + 1, front, b - 1, 3, 4);
                _o.Add("iron_grate", f.W(front, a + 2, f.F + 1), f.Yaw(90));
                float mid = (front + back) / 2;
                Put(f, Chance(0.5) ? "goblin_strawpile" : "rug_straw", mid, (a + b) / 2, R(0, 360), 0, force: true);
                Hang(f, "Morkhalla_WallChain1", (back, (a + b) / 2, side < 0 ? 90 : -90));
                if (Chance(0.6))
                {
                    _o.Add(One("Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1"), f.W(mid + side * 0.8f, a + R(1, 3), f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.3) && _s.Loot > 0)
                {
                    _o.Add(_b.Pickable, f.W(mid, a + 2, f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.4) && _s.Monsters > 0)
                {
                    var p = f.W(mid, a + 2, f.Top + Lift);
                    var foe = One(_foes);
                    _o.Add(_s.Respawn ? foe.Spawner : foe.Creature, p, f.Yaw(side < 0 ? 90 : -90));
                }
            }
        }
        // The aisle's walls are the cells': only the aisle is free. Each row of cells is kept by circles a metre apart
        // along its middle, each just wide enough that together they cover the row up to its grates.
        float half = (f.HalfA - aisle) / 2, keep = MathF.Sqrt(half * half + 0.25f);
        foreach (int side in new[] { -1, 1 })
        {
            for (float v = v0 + 0.5f; v < -v0; v++)
            {
                f.Take(side * (aisle + half), v, keep);
            }
        }
        for (float v = v0 + 2; v < -v0; v += 8)
        {
            Put(f, _b.Torch, aisle - 0.4f, v, -90, 0.3f);
        }
        Foes(f, 0.3f, _elites.Length > 0 ? _elites : null);
    }
}
