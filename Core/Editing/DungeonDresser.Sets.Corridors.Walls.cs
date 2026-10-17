namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>Skulls and bones along both walls of a catacomb corridor, but not right by a doorway.</summary>
    private void CorridorBones(Frame f)
    {
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
    }

    /// <summary>Vines on the side walls of a corridor, some of the spots only.</summary>
    private void CorridorVines(Frame f)
    {
        foreach (var sp in f.WallSpots(3, 2.2f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
        {
            var (vu, vv) = In(sp, 0.1f);
            _o.Add("vines", f.W(vu, vv, f.Top + 2.3f + R(-0.3f, 0.5f)), f.Yaw(sp.Yaw + 180));
        }
    }

    /// <summary>Spikes along the side walls of an Ashlands corridor.</summary>
    private void CorridorSpikes(Frame f)
    {
        foreach (var sp in f.WallSpots(4, 2.6f).Where(sp => sp.Yaw is 90 or -90))
        {
            var (su2, sv2) = In(sp, 0.2f);
            _o.Add("Ashlands_Fortress_Wall_Spikes", f.W(su2, sv2, f.Top + 1.9f), f.Yaw(sp.Yaw + 90));
        }
    }

    /// <summary>Roots on the side walls of a woods corridor, some of the spots only.</summary>
    private void CorridorRoots(Frame f)
    {
        foreach (var sp in f.WallSpots(2.2f, 2.4f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
        {
            var (ru2, rv2) = In(sp, 0.7f);
            Put(f, One("root11", "root12", "root07"), ru2, rv2, R(0, 360), 0.6f, force: true);
        }
    }

    /// <summary>Sharpened stakes along both walls of a goblin warren corridor, but not right by a doorway.</summary>
    private void CorridorStakes(Frame f)
    {
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
    }
}
