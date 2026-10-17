
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Corridor kinds.
    /// <summary>
    /// What makes this corridor its own: a gate midway (a wall across with a door of the style's), a collapse (rubble
    /// and broken wall narrowing the way, a fallen pillar), bones along both walls (catacombs), a runner down the
    /// middle (a keep, a temple), vines on the walls (a crypt, ruins), or nothing but the torches. A long straight
    /// corridor only, never the bent ones. Then the biome's own: a muddy scrap pile to dig through in the swamp, a stake
    /// wall barricade or seeker creep in the mines, icicles and ice in the cold, spikes in the Ashlands, roots in the
    /// woods, a trap in a keep or a warren.
    /// </summary>
    private void CorridorKind(Frame f)
    {
        if (f.S.Shape != DungeonPlan.Shape.Rect || f.HalfD < 3)
        {
            return;
        }
        var kinds = new List<(string Kind, float Weight)> { ("plain", 2) };
        if (f.HalfD >= 4 && _mat.Name is not "Goblin")
        {
            kinds.Add(("gate", 1.2f));
        }
        if (_mat.Name is "Stone" or "Grausten" or "Marble")
        {
            // Chosen whatever the decay (so that the setting moves nothing else); a kept corridor then stays plain.
            kinds.Add(("collapse", 1.5f));
        }
        if (_style.Name is "Crypt" or "Catacombs" or "Ruins")
        {
            kinds.Add(("bones", _style.Name == "Catacombs" ? 2 : 1));
            kinds.Add(("vines", 0.8f));
        }
        if (_style.Name is "Fortress" or "Temple" or "Dvergr hold")
        {
            kinds.Add(("runner", 1.2f));
        }
        if (_style.Name is "Goblin warren")
        {
            kinds.Add(("stakes", 1));
        }
        // The biome's own: mud in the swamp's crypts, a barricade or creep in the mines, icicles in the cold,
        // spikes in the Ashlands, roots in the woods, a trap in a keep or a warren.
        switch (_b.Name)
        {
            case "Swamp":
                kinds.Add(("mud", 1.5f));
                kinds.Add(("roots", 1));
                break;
            case "Mistlands":
                kinds.Add(("barricade", 1.2f));
                kinds.Add(("creep", 1.2f));
                break;
            case "Mountain" or "Deep North":
                kinds.Add(("icicles", 1.5f));
                break;
            case "Ashlands":
                kinds.Add(("spikes", 1));
                break;
            case "Meadows" or "Black Forest":
                kinds.Add(("roots", 1));
                break;
            default:
                break;
        }
        if (_style.Name is "Fortress" or "Prison" or "Goblin warren" && f.HalfD >= 4)
        {
            kinds.Add(("trap", 0.8f));
        }
        CorridorAs(f, Weighted.Pick(kinds, k => k.Weight, _rnd.NextDouble()).Kind);
    }

    /// <summary>
    /// Dresses a corridor as the kind picked (<see cref="CorridorKind"/>); a plain one, or a kind without dressing of its
    /// own, gets nothing.
    /// </summary>
    private void CorridorAs(Frame f, string kind)
    {
        switch (kind)
        {
            case "gate":
                CorridorGate(f);
                break;
            case "collapse":
                CorridorCollapse(f);
                break;
            case "bones":
                for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        float u = side * (f.HalfA - 0.55f);
                        // Against the walls, whatever else keeps the floor (a corridor is narrow: its doorways' clear
                        // space would refuse nearly every spot), only not right by a doorway.
                        if (!f.NearDoor(u, v, 2.2f))
                        {
                            Put(f, One("skull_pile", "bone_stack", "skull_pile", "LargeBone"), u, v, R(0, 360), 0.4f, force: true);
                        }
                    }
                }
                break;
            case "runner":
                Runner(f, -f.HalfD + 1, f.HalfD - 1);
                break;
            case "vines":
                foreach (var sp in f.WallSpots(3, 2.2f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (vu, vv) = In(sp, 0.1f);
                    _o.Add("vines", f.W(vu, vv, f.Top + 2.3f + R(-0.3f, 0.5f)), f.Yaw(sp.Yaw + 180));
                }
                break;
            case "mud":
                // A muddy scrap pile across the way, to be dug through, as in the sunken crypts.
                Put(f, "mudpile", 0, R(-1, 1), R(0, 360), 1.4f, force: true);
                Put(f, One("Pickable_Tar", "skull_pile", "LargeBone"), R(-0.8f, 0.8f), R(2.2f, 3.2f), R(0, 360), 0.3f);
                break;
            case "barricade":
                // The miners' stake wall across the way, to be hacked through.
                Put(f, "dvergrtown_wood_stakewall", 0, R(-1, 1), 90, 1.4f, force: true);
                break;
            case "creep":
                foreach (var sp in f.WallSpots(2.4f, 2.2f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (cu, cv) = In(sp, 0.15f);
                    _o.Add("CreepProp_wall01", f.W(cu, cv, f.Top + 1.6f), f.Yaw(sp.Yaw));
                }
                if (Spot(f, 0.4f, 1.2f) is var (eu, ev))
                {
                    _o.Add(One("CreepProp_egg_hanging01", "CreepProp_egg_hanging02"), f.W(eu, ev, f.Top + f.H - 2.8f), f.Yaw(R(0, 360)));
                }
                Put(f, "SeekerEgg", R(-0.8f, 0.8f), R(-f.HalfD + 2, f.HalfD - 2), R(0, 360), 0.3f);
                break;
            case "icicles":
                for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += R(1.5f, 3f))
                {
                    _o.Add("caverock_ice_stalagtite", f.W(R(-1, 1), v, f.Top + f.H - 3.4f), f.Yaw(R(0, 360)));
                }
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "BlackIceShard_01", side * (f.HalfA - 0.5f), R(-f.HalfD + 2, f.HalfD - 2), R(0, 360), 0.3f);
                }
                break;
            case "spikes":
                foreach (var sp in f.WallSpots(4, 2.6f).Where(sp => sp.Yaw is 90 or -90))
                {
                    var (su2, sv2) = In(sp, 0.2f);
                    _o.Add("Ashlands_Fortress_Wall_Spikes", f.W(su2, sv2, f.Top + 1.9f), f.Yaw(sp.Yaw + 90));
                }
                break;
            case "roots":
                foreach (var sp in f.WallSpots(2.2f, 2.4f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (ru2, rv2) = In(sp, 0.7f);
                    Put(f, One("root11", "root12", "root07"), ru2, rv2, R(0, 360), 0.6f, force: true);
                }
                break;
            case "trap":
                Put(f, _style.Name == "Goblin warren" ? "fuling_trap" : "piece_trap_troll", 0, R(-1.5f, 1.5f), 90, 0.9f, force: true);
                break;
            case "stakes":
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 2.5f; v <= f.HalfD - 2.5f; v += 3)
                    {
                        float u = side * (f.HalfA - 0.9f);
                        if (!f.NearDoor(u, v, 2.4f))
                        {
                            Put(f, "piece_sharpstakes", u, v, side < 0 ? 90 : -90, 0.6f, force: true);
                        }
                    }
                }
                break;
            default:
                break;
        }
    }
}
