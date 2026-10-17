
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A shrine: a statue or altar in a ring of braziers and skull piles; or a dolmen on a dais; or a statue on a
    /// plinth with offerings heaped before it.
    /// </summary>
    private void Shrine(Frame f)
    {
        switch (_rnd.Next(_mat.Name is "Stone" or "Grausten" or "Marble" ? 4 : 3))
        {
            case 3:
                // Sunken: ledges round a lower floor, the idol down in the middle in a ring of candles or skulls.
                Ledges(f);
                Put(f, One("StatueSeed", "StatueEvil", "StatueDeer", "MountainGraveStone01"), 0, 0, 180, 1.2f, force: true);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3;
                    Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", 1.7f * MathF.Cos(a), 1.7f * MathF.Sin(a), R(0, 360), 0.2f);
                }
                break;
            case 0:
                Put(f, f.S.Tall ? "StatueEvil" : One("StatueSeed", "blackmarble_altar_crystal", "StatueDeer"), 0, 0, 180, 1.3f, y: 0);
                float r = Math.Min(f.HalfA, f.HalfD) - 1.6f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3;
                    if (i % 2 == 0)
                    {
                        Brazier(f, r * MathF.Cos(a), r * MathF.Sin(a));
                    }
                    else
                    {
                        Put(f, "skull_pile", r * MathF.Cos(a), r * MathF.Sin(a), R(0, 360), 0.6f);
                    }
                }
                break;
            case 1 when _b.Name == "Mistlands":
                // A wisp fountain in the middle, black cores and candles round it.
                Put(f, "piece_wisplure", 0, 0, 0, 1.4f, force: true);
                Cores(f, 2);
                BackBraziers(f);
                break;
            case 1 when _b.Name is "Deep North" or "Mountain":
                // A memorial stone on a plinth, snow lanterns before it.
                Plinth(f, 0, 0.5f, 2, 2);
                string stone = One("MemorialStone_Medium", "MemorialStone_Large");
                _o.Add(stone, f.W(0, 0.5f, f.F + 1 + Lift * 2 + DungeonProps.Foot(stone)), f.Yaw(180));
                Steps(f, 0, -1.5f, 180, 2);
                Put(f, "piece_snowlantern", -1.6f, -1.6f, 0, 0.3f);
                Put(f, "piece_snowlantern", 1.6f, -1.6f, 0, 0.3f);
                Pilasters(f, 4);
                break;
            case 1 when f.HalfA >= 3.4f:
                // A standing stone: a wide one lying across the middle, or a high one where the roof allows.
                Put(f, f.S.Tall ? One("highstone", "highstone_2") : One("widestone", "widestone_2"), 0, 0, R(0, 360), 2.2f, force: true);
                BackBraziers(f);
                Pilasters(f, 4);
                break;
            case 1:
                Plinth(f, 0, 0, 4, 4);
                string dolmen = One("RockDolmen_1", "RockDolmen_2", "RockDolmen_3");
                _o.Add(dolmen, f.W(0, 0, f.F + 1 + Lift * 2 + DungeonProps.Foot(dolmen)), f.Yaw(R(0, 360)));
                Steps(f, 0, -3, 180, 2);
                BackBraziers(f);
                Pilasters(f, 4);
                break;
            default:
                Plinth(f, 0, f.HalfD - 2, 2, 2);
                _o.Add(One("StatueThor", "StatueFreya", "StatueEvil"), f.W(0, f.HalfD - 2, f.F + 1 + Lift * 2), f.Yaw(180));
                for (int i = 0, times = Count(2 + _s.Loot * 2); i < times; i++)
                {
                    PutNear(f, "treasure_stack", R(-1.5f, 1.5f), f.HalfD - 4.2f, R(0, 360), 0.3f, 0.8f);
                }
                Brazier(f, -2, f.HalfD - 2.4f);
                Brazier(f, 2, f.HalfD - 2.4f);
                Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, f.HalfD - 4.6f, 0, 0, force: true);
                Chains(f, 1);
                break;
        }
        Pickables(f, 0.6f);
        Clusters(f, 1, "altar");
        Hangings(f, 2);
        Foes(f, 0.9f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>
    /// A tomb: a sarcophagus and grave stone among four columns; or on a dais between two statues, chains above;
    /// treasure, an elite guard.
    /// </summary>
    private void Tomb(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, "crypt_skeleton_chest", 0, 0.5f, 90, 1.6f);
            Put(f, "MountainGraveStone01", 0, 2.6f, 180, 0.6f);
            foreach (var (u, v) in new[] { (-2.3f, -1.6f), (2.3f, -1.6f), (-2.3f, 2.6f), (2.3f, 2.6f) })
            {
                Column(f, u, v);
            }
        }
        else
        {
            Plinth(f, 0, 1, 4, 4);
            _o.Add("crypt_skeleton_chest", f.W(0, 1, f.F + 1 + Lift * 2), f.Yaw(90));
            Steps(f, 0, -2, 180, 2);
            string[] statues = Statues();
            Put(f, statues[0], -(f.HalfA - 1), f.HalfD - 1, 180, 0.6f);
            Put(f, statues[1], f.HalfA - 1, f.HalfD - 1, 180, 0.6f);
            Chains(f, 2);
        }
        Brazier(f, -f.HalfA + 1, -f.HalfD + 1);
        Brazier(f, f.HalfA - 1, -f.HalfD + 1);
        for (int i = 0, times = Count(1 + _s.Loot); i < times; i++)
        {
            PutNear(f, "treasure_stack", R(-1, 1), -1.2f, R(0, 360), 0.3f, 0.8f);
        }
        Hangings(f, 2);
        Elite(f, 0, -0.8f, 180);
        Clutter(f, 0.015f);
        Decay();
    }

    /// <summary>
    /// A throne room: the dais with colonnades, armour stands and banners; or a war room with a long table and weapon
    /// stands before the dais; or the dais among fallen columns and broken statues.
    /// </summary>
    private void Throne(Frame f)
    {
        Dais(f, f.HalfD - 2);
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.5))
                {
                    Loft(f);
                }
                if (f.HalfA >= 5)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                    {
                        Pillar(f, -(f.HalfA - 2.4f), v);
                        Pillar(f, f.HalfA - 2.4f, v);
                    }
                }
                Runner(f, -f.HalfD + 1.2f, f.HalfD - 6.2f);
                for (float v = -f.HalfD + 4; v <= f.HalfD - 7; v += 4)
                {
                    Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), v, 90, 0.6f);
                    Put(f, "ArmorStand_Male", f.HalfA - 0.8f, v, -90, 0.6f);
                }
                break;
            case 1:
                // The long table, where it has the room: not across the dais' steps or a doorway's floor.
                float[] along = [-3f, -1.5f, 0f, 1.5f, 3f];
                if (along.All(dv => f.Free(0, -1 + dv, 0.8f)))
                {
                    _o.Add("piece_table_oak", f.W(0, -1, f.Top + Lift), f.Yaw(90));
                    foreach (float dv in along)
                    {
                        f.Take(0, -1 + dv, 0.8f);
                    }
                    Put(f, "piece_cartographytable", 0, -1, 0, 0, y: 0.82f, force: true);
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (float dv = -2.4f; dv <= 2.41f; dv += 1.6f)
                        {
                            Put(f, "piece_chair03", side * 1.4f, -1 + dv, side < 0 ? 90 : -90, 0.4f);
                        }
                    }
                }
                foreach (var sp in f.WallSpots(4, 3).Where(x => x.Yaw is 90 or -90).OrderBy(_ => _rnd.Next()).Take(4))
                {
                    var (u, v) = In(sp, 0.7f);
                    Put(f, One("Morkhalla_WeaponStand", "ArmorStand_Male"), u, v, sp.Yaw, 0.7f);
                }
                Pilasters(f, 8);
                break;
            default:
                for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Chance(0.5))
                        {
                            Column(f, side * (f.HalfA - 2.4f), v);
                        }
                        else if (_mat.Column[0].Prefab == "stone_pillar")
                        {
                            FallenPillar(f, "stone_pillar", side * (f.HalfA - 3.4f), v, R(60, 120), 1.4f);
                        }
                    }
                }
                Put(f, "StatueThor_broken_bottom", -3, f.HalfD - 7, R(0, 360), 1f);
                Put(f, "StatueFreya_broken_right", 3, -f.HalfD + 3, R(0, 360), 1f);
                Clutter(f, 0.02f, "Morkhalla_Rubble2", "Morkhalla_Rubble1", "stone_wall_1x1_ruin");
                Webs(f, 0.6f);
                break;
        }
        Hangings(f, 4, "piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner09", "piece_banner10");
        Trophies(f, 2);
        WallGems(f, 2);
        Clusters(f, 1, "armoury");
        Lights(important: true);
        Elite(f, -1.8f, f.HalfD - 6.5f, 180);
        Elite(f, 1.8f, f.HalfD - 6.5f, 180);
        Foes(f, 0.6f);
        Decay();
    }

    /// <summary>n trophies on item stands on the walls.</summary>
    private void Trophies(Frame f, int n)
    {
        string[] options = [ "prop_itemstand_TrophyDraugrElite", "prop_itemstand_TrophyGoblinBrute", "prop_itemstand_TrophyGoblinShaman", "prop_itemstand_TrophyGreydwarf",
            "prop_itemstand_TrophyGreydwarfBrute", "prop_itemstand_TrophySeekerBrute" ];
        foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(n))
        {
            var (u, v) = In(s, 0.06f);
            _o.Add(One(options), f.W(u, v, f.Top + 2.1f), f.Yaw(s.Yaw));
        }
    }
}
