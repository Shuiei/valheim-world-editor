
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
