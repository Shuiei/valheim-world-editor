namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A chapel: an altar between two statues at the far end with pews facing it; or a round shrine, the altar in the
    /// middle in a ring of braziers; or a bare nave of rugs and candles under long hangings.
    /// </summary>
    private void Chapel(Frame f)
    {
        float far = f.HalfD - 1.6f;
        string altar = One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom");
        string[] statues = Statues();
        string bench = _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : _mat.Name == "Stave" ? "prop_piece_bench_runed" : "piece_bench01";
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.4))
                {
                    Loft(f);
                }
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, statues[0], -2.6f, far + 0.3f, 180, 0.6f);
                Put(f, statues[1], 2.6f, far + 0.3f, 180, 0.6f);
                Brazier(f, -1.6f, far - 1.8f);
                Brazier(f, 1.6f, far - 1.8f);
                for (float v = -f.HalfD + 3; v <= far - 3.5f; v += 1.7f)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (float u = 2.6f; u <= f.HalfA - 1.2f; u += 2.6f)
                        {
                            Put(f, bench, side * u, v, 0, 0.7f);
                        }
                    }
                }
                Runner(f, -f.HalfD + 1.2f, far - 1.4f);
                break;
            case 1:
                Plinth(f, 0, 0.5f, 4, 4);
                _o.Add(altar, f.W(0, 0.5f, f.F + 1 + Lift * 2), f.Yaw(180));
                Steps(f, 0, -2.5f, 180, 2);
                float r = Math.Min(f.HalfA, f.HalfD) - 1.4f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3 + MathF.PI / 6;
                    Brazier(f, r * MathF.Cos(a), 0.5f + r * MathF.Sin(a));
                }
                Put(f, statues[0], -(f.HalfA - 1), far + 0.3f, 180, 0.6f);
                Put(f, statues[1], f.HalfA - 1, far + 0.3f, 180, 0.6f);
                Pilasters(f, 4);
                break;
            default:
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, "StatueEvil", 0, far - 2.4f, 180, 1f);
                for (float v = -f.HalfD + 2.5f; v <= far - 4; v += 3)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Put(f, One("jute_carpet", "jute_carpet_blue", "rug_fur"), side * 2.2f, v, 0, 0, force: true);
                    }
                }
                if (!_s.Respawn)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (Spot(f, 0.2f, 0.5f) is var (cu, cv))
                        {
                            Put(f, "Candle_resin", cu, cv, R(0, 360), 0.2f);
                        }
                    }
                }
                Chains(f, 2);
                break;
        }
        Pickables(f, 0.5f);
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add("cloth_hanging_long", f.W(side * 2.2f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, "altar");
        Lights(important: true);
        Foes(f, 0.7f);
        Clutter(f, 0.008f);
        Decay();
    }
}
