using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The shell's walls: the tile sides a doorway crosses are no wall, the wall between two spaces is one edge, and a room is
/// walled with its material's pieces.
/// </summary>
public class DungeonShellWallsTests
{
    /// <summary>A level of two spaces side by side along x: the tile (0, 0) and the tile (1, 0).</summary>
    private static (DungeonPlan Plan, DungeonPlan.Level Level) TwoSpaces()
    {
        var plan = new DungeonPlan();
        var level = new DungeonPlan.Level { Index = 0 };
        level.Owner[(0, 0)] = new DungeonPlan.Space();
        level.Owner[(1, 0)] = new DungeonPlan.Space();
        return (plan, level);
    }

    /// <summary>A doorway from the first space's tiles (0, 0) and (0, 1) through to the tile beside them along x.</summary>
    private static DungeonPlan.Doorway DoorwayAcross(int level) =>
        new() { A = new DungeonPlan.Space { Level = level }, B = new DungeonPlan.Space(), T1 = (0, 0), T2 = (0, 1), Dir = (1, 0) };

    [Fact]
    public void ADoorwaysTwoSidesAreBothNoWall()
    {
        var (plan, level) = TwoSpaces();
        plan.Doors.Add(DoorwayAcross(0));

        var sides = DungeonShell.DoorEdges(plan, level);

        Assert.Contains(((0, 0), (1, 0)), sides);
        Assert.Contains(((1, 0), (0, 0)), sides);
    }

    [Fact]
    public void ADoorwayOfAnotherLevelIsNoWallHere()
    {
        var (plan, level) = TwoSpaces();
        plan.Doors.Add(DoorwayAcross(1));

        Assert.Empty(DungeonShell.DoorEdges(plan, level));
    }

    [Fact]
    public void TheWallBetweenTwoSpacesIsOneEdgeAlongTheSharedLine()
    {
        var (plan, level) = TwoSpaces();

        var edges = DungeonShell.WallEdges(plan, level, 0);

        var shared = Assert.Single(edges, e => e.AlongZ && e.Line == 2 && e.Index == 0);
        Assert.Equal((0f, 4f), (shared.Y0, shared.Y1));
    }

    [Fact]
    public void ADoorwayInTheWallLeavesNoEdgeThere()
    {
        var (plan, level) = TwoSpaces();
        plan.Doors.Add(DoorwayAcross(0));

        var edges = DungeonShell.WallEdges(plan, level, 0);

        Assert.DoesNotContain(edges, e => e.AlongZ && e.Line == 2 && e.Index == 0);
    }

    [Fact]
    public void AWallRoundARoomIsMadeOfTheMaterialsPieces()
    {
        var plan = new DungeonPlan();
        var level = new DungeonPlan.Level { Index = 0 };
        var room = new DungeonPlan.Space();
        foreach (var t in new (int, int)[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            level.Owner[t] = room;
        }
        var mat = DungeonKit.MaterialOf("Stone");
        var o = new DungeonOut();

        DungeonShell.Walls(plan, level, 0, mat, o);

        var allowed = mat.Walls.Select(p => p.Prefab).Concat(mat.Post is { } post ? [post.Prefab] : []).ToHashSet();
        Assert.NotEmpty(o.Items);
        Assert.All(o.Items, i => Assert.Contains(i.Prefab, allowed));
    }
}
