namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A feast hall: long tables end to end down the middle with benches and the hearth at the end; or the high table
    /// on a dais at the far end with rows of benches before it and a fire in the middle.
    /// </summary>
    private void Feast(Frame f)
    {
        float v0 = -f.HalfD + 3, v1 = f.HalfD - 3.6f;
        if (Chance(0.5))
        {
            for (float v = v0 + 3.3f; v + 3.3f <= v1 + 0.01f; v += 6.8f)
            {
                _o.Add("piece_table_oak", f.W(0, v, f.Top + Lift), f.Yaw(90));
                for (float dv = -3f; dv <= 3.01f; dv += 1.5f)
                {
                    f.Take(0, v + dv, 0.8f);
                }
                foreach (float dv in new[] { -2f, 0f, 2f })
                {
                    _o.Add(Chance(0.7) ? FeastFood : "prop_Tankard", f.W(R(-0.3f, 0.3f), v + dv, f.Top + 0.82f + Lift), f.Yaw(R(0, 360)));
                }
                foreach (int side in new[] { -1, 1 })
                {
                    for (float dv = -2.6f; dv <= 2.61f; dv += 2.6f)
                    {
                        Put(f, "piece_bench01", side * 1.5f, v + dv, side < 0 ? 90 : -90, 0.45f);
                    }
                }
            }
            Put(f, "piece_chair03", 0, v1 + 0.6f, 180, 0.4f);
            Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.4f);
        }
        else
        {
            Plinth(f, 0, f.HalfD - 2, 8, 2);
            _o.Add("piece_table_oak", f.W(0, f.HalfD - 2, f.F + 1 + Lift * 2), f.Yaw(0));
            foreach (float du in new[] { -2f, 0f, 2f })
            {
                _o.Add(Chance(0.7) ? FeastFood : "prop_Tankard", f.W(du, f.HalfD - 2, f.F + 1.82f + Lift * 2), f.Yaw(R(0, 360)));
                _o.Add("piece_chair03", f.W(du, f.HalfD - 1.1f, f.F + 1 + Lift * 2), f.Yaw(180));
            }
            Steps(f, 0, f.HalfD - 4, 180, 2);
            Put(f, One("prop_bonfire", "Morkhalla_firepit"), 0, -1, R(0, 360), 1.6f);
            for (float v = -f.HalfD + 2.5f; v <= f.HalfD - 5.5f; v += 2)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "piece_bench01", side * 2.8f, v, 0, 0.45f);
                }
            }
            Trophies(f, 2);
        }
        Hangings(f, 3, Drapes);
        Lights();
        Foes(f, 1.2f);
        Clutter(f, 0.008f);
        Decay();
    }

    /// <summary>A kitchen: hearth and cauldron, preparation and butcher's tables, stores by the walls.</summary>
    private void Kitchen(Frame f)
    {
        if (Chance(0.5))
        {
            // The cook's own stations: a cauldron over its fire, an oven, the fermenter and the mead cauldron, a bucket.
            Put(f, "piece_oven", 0, f.HalfD - 1.1f, 180, 1.6f);
            Put(f, "piece_cauldron", -(f.HalfA - 1.3f), f.HalfD - 1.3f, 135, 0.9f);
            Put(f, "piece_preptable", f.HalfA - 0.5f, 0, -90, 1.3f);
            Put(f, "fermenter", -(f.HalfA - 1.1f), -1, 90, 1.0f);
            Put(f, "piece_MeadCauldron", -(f.HalfA - 0.8f), -2.8f, 90, 0.6f);
            Put(f, "bucket", f.HalfA - 0.6f, -2.4f, R(0, 360), 0.4f);
            Put(f, "coal_pile", 1.2f, f.HalfD - 2.8f, R(0, 360), 0.9f);
            foreach (var s in f.WallSpots(1.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(3))
            {
                var (su, sv) = In(s, 0.6f);
                Put(f, One("barrell", "piece_chest_barrel", "prop_wood_stack", "CastleKit_braided_box01"), su, sv, R(0, 360), 0.6f);
            }
            Lights();
            Foes(f, 0.5f);
            Clutter(f, 0.01f);
            Decay();
            return;
        }
        Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.6f);
        Put(f, "prop_piece_cauldron", 0, f.HalfD - 3.4f, 0, 0.6f);
        Put(f, "prop_preptable", -(f.HalfA - 0.5f), 0, 90, 1.3f);
        Put(f, "prop_cauldron_ext6_rollingpins", -(f.HalfA - 0.5f), 0.6f, 90, 0, y: 1.2f, force: true);
        Put(f, "prop_cauldron_ext3_butchertable", f.HalfA - 1.2f, 0, -90, 0.8f);
        Put(f, "prop_cauldron_ext5_mortarandpestle", f.HalfA - 0.6f, -2.4f, -90, 0.8f);
        Put(f, "prop_piece_cookingstation", -2.4f, f.HalfD - 1.4f, 180, 0.8f);
        foreach (var s in f.WallSpots(1.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(5))
        {
            var (u, v) = In(s, 0.6f);
            Put(f, One("barrell", "CargoCrate", "prop_wood_stack"), u, v, R(0, 360), 0.6f);
        }
        Lights();
        Foes(f, 0.5f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>A forge: forge and workbench pieces, wood, and metal bars when there is loot.</summary>
    private void Forge(Frame f)
    {
        if (Chance(0.5))
        {
            // The smith's own: the forge with its bellows, a smelter or the biome's furnace, coal and ore, bars.
            string furnace = _b.Name switch { "Ashlands" => "blackforge", "Mistlands" => "eitrrefinery", "Deep North" => "piece_FrostFoundry", _ => "smelter" };
            Put(f, "forge", 0, f.HalfD - 1.2f, 180, 1.2f);
            Put(f, "prop_forge_ext2", -1.4f, f.HalfD - 1.1f, 180, 0.5f);
            Put(f, furnace, -(f.HalfA - 1.8f), -(f.HalfD - 1.8f), 45, 1.8f);
            Put(f, "piece_stonecutter", f.HalfA - 1.0f, 0.4f, -90, 1.0f);
            Put(f, "coal_pile", f.HalfA - 1.2f, -(f.HalfD - 1.2f), R(0, 360), 0.9f);
            Put(f, One("stone_pile", "coal_pile", "prop_wood_stack"), -(f.HalfA - 1.0f), 1.6f, R(0, 360), 0.9f);
            if (_s.Loot > 0)
            {
                Put(f, _b.Bars, 1.6f, f.HalfD - 2.8f, R(-10, 10), 0.5f);
            }
            Put(f, "bucket", 1.2f, -0.4f, R(0, 360), 0.4f);
            Lights();
            Foes(f, 0.5f);
            Clutter(f, 0.01f);
            Decay();
            return;
        }
        Put(f, "prop_forge_ext2", 0, 0.6f, 0, 0.6f);
        Put(f, "prop_forge_ext5", 1.4f, 0.6f, 0, 0.6f);
        Brazier(f, -1.4f, 0.6f);
        Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
        Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 1.6f, 90, 0.7f);
        Put(f, "prop_piece_workbench_ext4", f.HalfA - 0.4f, 1.6f, -90, 0.7f);
        Put(f, "prop_wood_stack", -(f.HalfA - 1.4f), -(f.HalfD - 1.4f), R(0, 360), 1.2f);
        if (_s.Loot > 0)
        {
            Put(f, _b.Bars, f.HalfA - 1.2f, -(f.HalfD - 1.2f), R(0, 360), 0.5f);
        }
        Lights();
        Foes(f, 0.5f);
        Clutter(f, 0.01f);
        Decay();
    }
}
