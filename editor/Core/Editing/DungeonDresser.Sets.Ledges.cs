
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Ledges round a pit.
    /// <summary>
    /// Ledges 1 m high round the room's edge, leaving the middle a sunken floor and the doorways' tiles level with
    /// it: an arena's stands, a sunken shrine. The ledges keep their floor; braziers stand on their corners.
    /// </summary>
    private void Ledges(Frame f)
    {
        var ledges = new List<(float U, float V)>();
        for (int a = f.A0; a <= f.A1; a++)
        {
            for (int b = f.B0; b <= f.B1; b++)
            {
                if (!f.Has(a, b) || f.Has(a - 1, b) && f.Has(a + 1, b) && f.Has(a, b - 1) && f.Has(a, b + 1))
                {
                    continue;
                }
                float u = 2 * a + 1 - f.Across, v = 2 * b + 1 - f.Deep;
                if (f.NearDoor(u, v, 2.6f))
                {
                    continue;
                }
                _o.Add(Deck, f.W(u, v, f.F + 0.5f + Lift), f.Yaw(0));
                f.Take(u, v, 1.3f);
                ledges.Add((u, v));
            }
        }
        // Braziers on the ledges' corners (the tiles with two free sides), skulls on some others.
        foreach (var (u, v) in ledges)
        {
            bool corner = MathF.Abs(MathF.Abs(u) - (f.Across - 1)) < 0.1f && MathF.Abs(MathF.Abs(v) - (f.Deep - 1)) < 0.1f;
            if (corner)
            {
                _o.Add(_b.Brazier, f.W(u, v, f.F + 1 + Lift * 2 + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
            }
            else if (Chance(0.25))
            {
                _o.Add(One("skull_pile", "bone_stack", "Skull1"), f.W(u, v, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
            }
        }
    }

    // ---- A vault within the room.
    /// <summary>
    /// An inner chamber against the far wall: walls of the material 3 m high, w metres across and d deep, with a 2 m
    /// opening in the middle of its front under a lintel; candles either side of the opening. What it holds goes in
    /// after (its middle is (0, HalfD - d / 2)). The floor along its walls is kept.
    /// </summary>
    private void Vault(Frame f, float w, float d)
    {
        float front = f.HalfD - d, half = w / 2;
        InnerWall(f, -half, front, -half, f.HalfD, 0, 3);
        InnerWall(f, half, front, half, f.HalfD, 0, 3);
        InnerWall(f, -half, front, -1, front, 0, 3);
        InnerWall(f, 1, front, half, front, 0, 3);
        InnerWall(f, -1, front, 1, front, 2, 3);
        for (float t = 0; t <= d; t += 1)
        {
            f.Take(-half, front + t, 0.5f);
            f.Take(half, front + t, 0.5f);
        }
        for (float t = -half; t <= half; t += 1)
        {
            if (MathF.Abs(t) > 1.2f)
            {
                f.Take(t, front, 0.5f);
            }
        }
        if (!_s.Respawn)
        {
            _o.Add("Candle_resin", f.W(-1.3f, front - 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
            _o.Add("Candle_resin", f.W(1.3f, front - 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
        }
        else
        {
            Put(f, "skull_pile", -1.4f, front - 0.6f, R(0, 360), 0.3f);
            Put(f, "skull_pile", 1.4f, front - 0.6f, R(0, 360), 0.3f);
        }
    }

    // ---- Clusters.
    /// <summary>The style's dining table, bench and seat.</summary>
    private (string Table, string Seat, string Bench) Furniture => _mat.Name switch
    {
        "Dvergr" => ("dvergrprops_table", "dvergrprops_stool", "dvergrprops_table"),
        "Grausten" => ("Morkhalla_Table", "Morkhalla_Stool", "Morkhalla_Bench"),
        "Marble" => ("piece_table_oak", "piece_chair03", "piece_blackmarble_bench"),
        "Stave" => ("piece_table_runed", "piece_chair_runed", "piece_bench_runed"),
        _ => ("piece_table", "piece_chair", "piece_bench01"),
    };
}
