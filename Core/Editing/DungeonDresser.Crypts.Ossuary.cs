namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// An ossuary: bones along every wall, or a great heap in the middle with skulls in the corners, or stacked in rows
    /// forming aisles; with monsters, a bone pile that raises the dead.
    /// </summary>
    private void Ossuary(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(1.25f, 2.6f))
                {
                    var (u, v) = In(s, 0.55f);
                    Put(f, Chance(0.5) ? "skull_pile" : "bone_stack", u, v, s.Yaw + R(-20, 20), 0.5f);
                }
                break;
            case 1:
                for (int i = 0; i < 7; i++)
                {
                    PutNear(f, One("bone_stack", "skull_pile", "LargeBone"), R(-1.5f, 1.5f), R(-1.5f, 1.5f), R(0, 360), 0.35f, 1f);
                }
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex))
                {
                    var (cu, cv) = In((u, v, yaw), 0.9f);
                    Put(f, "skull_pile", cu, cv, R(0, 360), 0.5f);
                }
                Webs(f, 0.7f);
                break;
            default:
                for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 3)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 1.3f)
                    {
                        Put(f, Chance(0.8) ? "bone_stack" : "skull_pile", u, v, 90, 0.5f);
                    }
                }
                break;
        }
        if (_s.Monsters > 0 && _s.Respawn && _s.Foes is not { Count: > 0 })
        {
            // A heap of bones that keeps raising the dead until broken (the biome's: none when foes are chosen, nor when
            // nothing respawns).
            Put(f, _b.Name == "Swamp" ? "Spawner_DraugrPile" : _b.Name is "Black Forest" or "Meadows" ? "BonePileSpawner" : "BonePileSpawner_swamp", 0, R(-1, 1), R(0, 360), 1.3f);
        }
        Clutter(f, 0.04f, "Skull1", "LargeBone", "LargeBone_half01", "Skull1");
        Clusters(f, 1, "bones");
        Lights();
        Foes(f, 0.4f);
        Decay();
    }
}
