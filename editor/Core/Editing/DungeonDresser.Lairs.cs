using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The antechamber: braziers either side of the boss's gate, a warning sign at the key gate, elite guards.</summary>
    private void Antechamber(Frame f)
    {
        // The way to the boss: braziers either side of its gate, guards, a warning.
        foreach (var m in f.Mouths.Where(m => m.Door.Kind is Door.Key or Door.Grand))
        {
            var (u, v) = f.Local(m.X, m.Z);
            var (sideU, sideV) = (m.In.X * f.A.X + m.In.Z * f.A.Z, m.In.X * f.D.X + m.In.Z * f.D.Z);
            var (alongU, alongV) = (sideV, -sideU);
            Brazier(f, u + sideU * 1.2f + alongU * 3.2f, v + sideV * 1.2f + alongV * 3.2f);
            Brazier(f, u + sideU * 1.2f - alongU * 3.2f, v + sideV * 1.2f - alongV * 3.2f);
            if (m.Door.Kind == Door.Key)
            {
                // The doorway's middle is on the wall's centre line: the sign goes on its inner face, a touch out.
                float yaw = MathF.Atan2(sideU, sideV) * 180 / MathF.PI, inset = _mat.Thick / 2 + 0.04f;
                _o.Add("sign", f.W(u + alongU * 2.6f + sideU * inset, v + alongV * 2.6f + sideV * inset, f.Top + 2.1f), f.Yaw(yaw + DungeonProps.Turn("sign")),
                    [("strings", "text", "Sealed. The key lies within these halls.")]);
            }
        }
        Hangings(f, 2, Drapes);
        Clutter(f, 0.02f, "Skull1", "skull_pile", "LargeBone");
        Elite(f, -1.6f, 0, 180);
        Elite(f, 1.6f, 0, 180);
        Lights(important: true);
    }

    /// <summary>A goblin den: straw and beds by the walls round a trash pile and a totem; or a fire in the middle with the straw round it.</summary>
    private void Den(Frame f)
    {
        if (Chance(0.5))
        {
            foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(4))
            {
                var (u, v) = In(s, 1.1f);
                Put(f, Chance(0.5) ? "goblin_strawpile" : "goblin_bed", u, v, s.Yaw, 1.1f);
            }
            Put(f, "goblin_trashpile", R(-1, 1), R(-1, 1), R(0, 360), 1.6f);
            Brazier(f, R(-2, 2), -1.5f);
        }
        else
        {
            Put(f, "prop_bonfire", 0, 0, R(0, 360), 1.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathF.PI / 3 + MathF.PI / 6;
                PutNear(f, One("goblin_strawpile", "goblin_bed", "rug_straw"), 2.9f * MathF.Cos(a), 2.9f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.9f, 0.5f);
            }
            Put(f, "goblin_trashpile", -(f.HalfA - 1.4f), f.HalfD - 1.4f, R(0, 360), 1.4f);
            Put(f, "lox_ribs", f.HalfA - 1.4f, -(f.HalfD - 1.4f), R(0, 360), 0.8f);
        }
        if (!(Chance(0.4) && PutHero(f, 0, f.HalfD - 2, 180, 1.8f)))
        {
            Put(f, "goblin_totempole", 0, f.HalfD - 0.6f, 180, 0.6f);
        }
        Put(f, "goblin_banner", -(f.HalfA - 0.6f), f.HalfD - 1.2f, 90, 0.6f);
        Clusters(f, 2, "camp", "bones");
        Lights();
        Foes(f, 1.5f);
        Clutter(f, 0.025f);
        Decay();
    }

    /// <summary>A totem hall: a brazier ringed by totem poles; or a row of totems along the far wall over a bone-strewn floor; skull piles, shamans if there are any.</summary>
    private void Totem(Frame f)
    {
        if (Chance(0.5))
        {
            Brazier(f, 0, 0);
            for (int i = 0; i < 5; i++)
            {
                float a = i * MathF.PI * 2 / 5;
                Put(f, "goblin_totempole", 3.2f * MathF.Cos(a), 3.2f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.6f);
            }
        }
        else
        {
            for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 2)
            {
                Put(f, "goblin_totempole", u, f.HalfD - 0.8f, 180, 0.6f);
            }
            Brazier(f, -2, f.HalfD - 3);
            Brazier(f, 2, f.HalfD - 3);
            if (!PutHero(f, 0, f.HalfD - 3.4f, 180, 1.6f))
            {
                Put(f, "Goblin_Gem", 0, f.HalfD - 3.4f, 180, 1.2f);
            }
            Clutter(f, 0.03f, "Skull1", "LargeBone", "lox_ribs");
        }
        foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(3))
        {
            var (u, v) = In(s, 0.5f);
            Put(f, "skull_pile", u, v, R(0, 360), 0.6f);
        }
        Clusters(f, 1, "bones", "altar");
        Hangings(f, 2);
        Foes(f, 1f, [.. _foes.Where(x => x.Creature.Contains("Shaman", StringComparison.Ordinal)).DefaultIfEmpty(_foes[0])]);
        Foes(f, 0.6f);
        Clutter(f, 0.02f);
        Decay();
    }

    /// <summary>A mine gallery: timber frames every 6 m, crates and tools by the walls, loot lying about.</summary>
    private void Mine(Frame f)
    {
        // Timber frames across the gallery, every 6 m.
        for (float v = -f.HalfD + 3; v <= f.HalfD - 2; v += 6)
        {
            _o.Add("dvergrtown_wood_support", f.W(0, v, f.F), f.Yaw(0));
            f.Take(-(f.HalfA - 0.4f), v, 0.6f);
            f.Take(f.HalfA - 0.4f, v, 0.6f);
        }
        foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(6))
        {
            var (u, v) = In(s, 0.7f);
            Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe", "dvergrprops_crate_long"), u, v, s.Yaw + R(-20, 20), 0.6f);
        }
        Pickables(f, 1.5f);
        Clusters(f, 1, "stores");
        Lights();
        Foes(f, 0.8f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>A workshop: work tables with stools and a lantern, workbench pieces, shelves.</summary>
    private void Workshop(Frame f)
    {
        if (Chance(0.5))
        {
            // The crafter's own stations: a workbench with its tools, the artisan's table, a spinning wheel, a mage's table in the mists.
            Put(f, "piece_workbench", 0, f.HalfD - 0.8f, 180, 1.3f);
            Put(f, "prop_piece_workbench_ext1", -2.0f, f.HalfD - 0.5f, 180, 0.6f);
            Put(f, "prop_piece_workbench_ext3", 2.0f, f.HalfD - 0.5f, 180, 0.6f);
            Put(f, _b.Name == "Mistlands" ? "piece_magetable" : "piece_artisanstation", -(f.HalfA - 1.4f), -0.4f, 90, 1.4f);
            Put(f, "piece_spinningwheel", f.HalfA - 1.2f, -0.6f, -90, 1.1f);
            Put(f, "UpgradeStation", 0, -(f.HalfD - 1.0f), 0, 0.8f);
            Clutter(f, 0.02f, "dvergrprops_crate", "barrell", "prop_wood_stack", "piece_chest_barrel");
            Clusters(f, 1, "stores");
            Lights();
            Foes(f, 0.6f);
            Decay();
            return;
        }
        Put(f, "dvergrprops_table", -1.4f, 0, 90, 1.1f);
        Put(f, "dvergrprops_table", 1.4f, 0, 90, 1.1f);
        Put(f, "dvergrprops_stool", -1.4f, -1.2f, 0, 0.3f);
        Put(f, "dvergrprops_stool", 1.4f, 1.2f, 180, 0.3f);
        Put(f, "dvergrprops_lantern_standing", -1.4f, 0.3f, 0, 0, y: 0.78f, force: true);
        Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 2, 90, 0.7f);
        Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
        Shelves(f, 2.2f);
        Clutter(f, 0.02f, "dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe");
        Clusters(f, 1, "workshop", "stores");
        Lights();
        Foes(f, 0.6f);
        Decay();
    }
}
