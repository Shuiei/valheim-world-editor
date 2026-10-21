
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The pit arena: ledges round a sunken floor, columns at its corners, the master down in the middle.</summary>
    /// <param name="f">The room's frame.</param>
    private void Pit(Frame f)
    {
        // The columns first: each ledge keeps 1.3 m round its tile, which took the columns' spots beside them.
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
    }

    /// <summary>The ring arena: a ring of columns, braziers between, the master in the middle.</summary>
    /// <param name="f">The room's frame.</param>
    /// <param name="r">The ring's radius (metres).</param>
    private void Ring(Frame f, float r)
    {
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
    }

    /// <summary>The dais arena: the dais at the far end, the boss's seat on it, rows of braziers and skulls before it.</summary>
    /// <param name="f">The room's frame.</param>
    private void Dais(Frame f)
    {
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
    }
}
