using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The wall hangings that suit the style: banners in keeps and temples, the biome's own elsewhere.</summary>
    private string[] Drapes => _style.Name switch
    {
        "Fortress" or "Temple" => ["piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner07", "cloth_hanging_door"],
        _ => _b.Hanging,
    };

    /// <summary>
    /// A corridor: torches on alternate sides, or sconces; a few things lying about; now and then pilasters with an
    /// arch every 8 m, a niche's statue, rubble, cobwebs, a foe.
    /// </summary>
    private void Corridor(Frame f)
    {
        if (f.S.DeadEnd)
        {
            Cache(f);
        }
        // Pilasters every 8 m along a long straight corridor, a beam of wall between them like an arch.
        _bands.Clear();
        if (f.S.Shape == DungeonPlan.Shape.Rect && f.HalfD >= 6 && Chance(0.45))
        {
            for (float v = -f.HalfD + 4; v <= f.HalfD - 4; v += 8)
            {
                if (f.NearDoor(f.HalfA, v, 3) || f.NearDoor(-f.HalfA, v, 3))
                {
                    continue;
                }
                Column(f, -(f.HalfA - 0.45f), v);
                Column(f, f.HalfA - 0.45f, v);
                InnerWall(f, -f.HalfA, v, f.HalfA, v, 3, f.H);
                _bands.Add(v);
            }
        }
        CorridorKind(f);
        // A statue in a bay, a few bones, rubble, cobwebs.
        if (Chance(0.25) && Spot(f, 0.8f, 0.6f) is var (su, sv))
        {
            Put(f, Statue("StatueThor", "StatueFreya", "StatueDeer"), su, sv, R(0, 360), 0.9f);
        }
        Clutter(f, 0.02f);
        Webs(f, 0.1f + _decay * 0.4f);
        Foes(f, 0.25f);
        // Its lights and a heap of rubble come last, in Finish, each from a stream of the corridor's own.
    }

    /// <summary>
    /// A corridor's lights: torches along the walls (where there are walls: not where another corridor opens in),
    /// every other spot, closer the brighter; sconces instead, now and then, on stone.
    /// </summary>
    private void CorridorLights(Frame f, Random light)
    {
        // Catacombs and prisons keep some corridors dark between their lit rooms (the contrast is the point), unless Bright.
        if (_style.Name is "Catacombs" or "Prison" && _s.Light < 2 && light.NextDouble() < 0.4)
        {
            return;
        }
        float step = _s.Light >= 2 ? 6 : _s.Light == 1 ? 9 : 16;
        bool sconces = _mat.Name is "Stone" or "Marble" or "Grausten" && light.NextDouble() < 0.4;
        string lamp = Lamp(light);
        int k = light.Next(2);
        foreach (var sp in f.WallSpots(step / 2, 2.5f).Where(sp => sp.Yaw is 90 or -90))
        {
            if (k++ % 2 != 0)
            {
                continue;
            }
            if (sconces)
            {
                var (wu, wv) = In(sp, 0.25f);
                _o.Add("piece_walltorch", f.W(wu, wv, f.Top + 2.1f), f.Yaw(sp.Yaw));
            }
            else
            {
                var (tu, tv) = In(sp, 0.5f);
                Put(f, lamp, tu, tv, sp.Yaw, 0.35f, take: false);
            }
        }
    }

    /// <summary>The top or the foot of the stairs: the stairs' side kept free, torches on the other.</summary>
    private void Stairwell(Frame f)
    {
        // The stairs keep the side at u -3.5..-1.5; torches on the other.
        f.Take(-2.5f, 0, 2f);
        f.Take(-2.5f, f.HalfD - 4, 2f);
        f.Take(-2.5f, -f.HalfD + 4, 2f);
        if (f.S.Room == Room.StairTop)
        {
            // Only its first cell has a floor.
            Put(f, _b.Torch, f.HalfA - 0.6f, -f.HalfD + 1, -90, 0.4f);
            Put(f, _b.Torch, -f.HalfA + 0.6f, -f.HalfD + 1, 90, 0.4f);
        }
        else
        {
            Put(f, _b.Torch, f.HalfA - 0.6f, f.HalfD - 1, -90, 0.4f);
            Put(f, _b.Torch, f.HalfA - 0.6f, -f.HalfD + 6, -90, 0.4f);
            Clutter(f, 0.02f);
        }
    }

    /// <summary>
    /// The entrance hall: room for the portal, the name sign, a runner, braziers, benches; or a guard post, with
    /// weapon stands, armour and banners by the way in.
    /// </summary>
    private void Entrance(Frame f)
    {
        // The portal stands before the back wall; the dungeon's name on a sign beside it.
        f.Take(0, -f.HalfD + 1.2f, 1.9f);
        _o.Add("sign", f.W(2.6f, -f.HalfD + 0.04f, f.Top + 2.1f), f.Yaw(DungeonProps.Turn("sign")),
            [("strings", "text", _name)]);
        if (Chance(0.5))
        {
            Runner(f, -f.HalfD + 2.5f, f.HalfD - 1);
            Brazier(f, -2.8f, f.HalfD - 1.3f);
            Brazier(f, 2.8f, f.HalfD - 1.3f);
            foreach (int side in new[] { -1, 1 })
            {
                Put(f, _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : "piece_bench01", side * (f.HalfA - 0.6f), 0, side < 0 ? 90 : -90, 1.3f);
            }
        }
        else
        {
            Brazier(f, -2.2f, -f.HalfD + 3.2f);
            Brazier(f, 2.2f, -f.HalfD + 3.2f);
            Put(f, "Morkhalla_WeaponStand", -(f.HalfA - 0.8f), 1, 90, 0.8f);
            Put(f, "ArmorStand_Male", f.HalfA - 0.8f, 1, -90, 0.6f);
            Put(f, "ArmorStand_Female", f.HalfA - 0.8f, 2.4f, -90, 0.6f);
            Put(f, "piece_logbench01", -(f.HalfA - 0.7f), f.HalfD - 2, 90, 0.6f);
            Put(f, One("rug_wolf", "rug_deer", "jute_carpet"), 0, 1, 0, 0, force: true);
            Pilasters(f, 4);
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, StyleClusters);
        Lights(important: true);
        Clutter(f, 0.01f);
    }
}
