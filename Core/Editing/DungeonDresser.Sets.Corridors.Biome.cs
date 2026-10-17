namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A seeker creep in a mines corridor: webbed walls with eggs, and a loose egg on the floor.</summary>
    private void CorridorCreep(Frame f)
    {
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
    }

    /// <summary>Icicles hanging from the ceiling of a cold corridor, and ice shards at the walls.</summary>
    private void CorridorIcicles(Frame f)
    {
        for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += R(1.5f, 3f))
        {
            _o.Add("caverock_ice_stalagtite", f.W(R(-1, 1), v, f.Top + f.H - 3.4f), f.Yaw(R(0, 360)));
        }
        foreach (int side in new[] { -1, 1 })
        {
            Put(f, "BlackIceShard_01", side * (f.HalfA - 0.5f), R(-f.HalfD + 2, f.HalfD - 2), R(0, 360), 0.3f);
        }
    }
}
