namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
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
                CorridorBones(f);
                break;
            case "runner":
                Runner(f, -f.HalfD + 1, f.HalfD - 1);
                break;
            case "vines":
                CorridorVines(f);
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
                CorridorCreep(f);
                break;
            case "icicles":
                CorridorIcicles(f);
                break;
            case "spikes":
                CorridorSpikes(f);
                break;
            case "roots":
                CorridorRoots(f);
                break;
            case "trap":
                Put(f, _style.Name == "Goblin warren" ? "fuling_trap" : "piece_trap_troll", 0, R(-1.5f, 1.5f), 90, 0.9f, force: true);
                break;
            case "stakes":
                CorridorStakes(f);
                break;
            default:
                break;
        }
    }
}
