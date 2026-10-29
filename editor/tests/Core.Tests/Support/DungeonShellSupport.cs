using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// The dungeon shell's test setup: a doorway of a given kind between two tiles, and the output the shell makes of it in stone.
/// </summary>
internal static class DungeonShellSupport
{
    /// <summary>A doorway of this kind, one metre wide, from the tile at the origin to the one beside it along x.</summary>
    public static DungeonPlan.Doorway DoorwayOf(DungeonKit.Door kind) =>
        new() { A = new(), B = new(), T1 = (0, 0), T2 = (0, 1), Dir = (1, 0), Kind = kind };

    /// <summary>What the shell builds in a stone doorway of this kind (Meadows biome, first level's floor).</summary>
    public static DungeonOut ShellOf(DungeonKit.Door kind)
    {
        var o = new DungeonOut();
        DungeonShell.Doorway(DoorwayOf(kind), 0, DungeonKit.MaterialOf("Stone"), o, "Meadows");
        return o;
    }
}
