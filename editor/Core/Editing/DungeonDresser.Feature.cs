namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The set piece at the far end of a hall: an altar, a throne, a statue, a table.</summary>
    private void Feature(Frame f, float v)
    {
        switch (_style.Name)
        {
            case "Fortress" or "Goblin warren":
                Dais(f, v);
                break;
            case "Dvergr hold":
                Put(f, "dvergrprops_table", 0, v - 0.6f, 0, 1.2f);
                Put(f, "dvergrprops_chair", 0, v + 0.3f, 180, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.5f, v - 0.6f, 0, 0, y: 0.78f, force: true);
                break;
            default:
                if (!(Chance(0.4) && PutHero(f, 0, v, 180)))
                {
                    Put(f, f.S.Tall ? "StatueEvil" : One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, v, 180, 1.6f);
                }
                Brazier(f, -2.2f, v - 0.6f);
                Brazier(f, 2.2f, v - 0.6f);
                break;
        }
    }

    /// <summary>A throne that suits the material (a bone throne in a stone crypt); some are drawn, so call it once a seat.</summary>
    private string PickThrone() => _mat.Name switch
    {
        "Marble" or "Grausten" => "piece_blackmarble_throne",
        "Goblin" => "piece_bone_throne",
        "Stave" => "piece_moose_throne",
        _ => _style.Name == "Crypt" ? "piece_bone_throne" : One("piece_throne01", "piece_throne02"),
    };

    /// <summary>
    /// A dais of stone 1 m high across the far end, steps in front, the throne on it. It stands on the
    /// marble, through any wooden floor (stone on wood would not hold).
    /// </summary>
    private void Dais(Frame f, float v)
    {
        float back = Math.Min(f.HalfD - 0.6f, v + 1.2f);
        for (float u = -2; u <= 2.01f; u += 2)
        {
            for (float dv = 0; dv < 4; dv += 2)
            {
                _o.Add("stone_floor_2x2", f.W(u, back - 1 - dv, f.F + 0.5f + Lift), f.Yaw(0));
            }
        }
        for (float u = -2; u <= 2.01f; u += 2)
        {
            _o.Add("stone_stair", f.W(u, back - 5, f.F + Lift), f.Yaw(180));
        }
        f.Take(0, back - 2.5f, 3.6f);
        string throne = PickThrone();
        _o.Add(throne, f.W(0, back - 1.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(throne)));
        Brazier(f, -3.6f, back - 1.5f);
        Brazier(f, 3.6f, back - 1.5f);
    }
}
