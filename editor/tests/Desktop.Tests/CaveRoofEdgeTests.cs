using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A cave roof at the edges: no line, no roof; a cave running off the grid gets boulders only where they reach it,
/// the boulders the rock chosen gives, and the floor left open after the first boulders covered by more.
/// </summary>
public class CaveRoofEdgeTests
{
    /// <summary>
    /// A cave 6 m wide with no soft edge and 4 m of headroom, its seed fixed.
    /// </summary>
    private static PathTool Cave() => new() { Act = PathTool.Action.Cave, Width = 6, Soft = 0, Headroom = 4, Seed = 5 };

    [Fact]
    public void ALineOfOnePointHasNoRoof()
    {
        var plan = new PathTool.CavePlan([(new Vector2(10, 10), 0)], [0], [10], [20]);
        var roof = CaveRoof.Build(Cave(), plan, 65, 65, 0, 0, (_, _) => PathTool.CaveRock.Auto);
        Assert.Empty(roof.Rocks);
        Assert.Empty(roof.Seal);
        Assert.Equal(0, roof.Uncovered);
    }

    [Fact]
    public void ACaveRunningOffTheGridIsRoofedOnlyWhereTheBouldersReachIt()
    {
        // 42 m deep under the ground, all but its last 2 m west of the grid (x from -40 to 2, z 32).
        var plan = new PathTool.CavePlan([(new Vector2(-40, 32), 0), (new Vector2(2, 32), 0)], [0, 42], [10, 10], [20, 20]);
        var roof = CaveRoof.Build(Cave(), plan, 65, 65, 0, 0, (_, _) => PathTool.CaveRock.Coast);
        Assert.NotEmpty(roof.Rocks);
        Assert.All(roof.Rocks, r => Assert.Equal("rock4_coast", r.Prefab));
        // Boulders far off the grid cannot hold up anything there: none was put.
        Assert.All(roof.Rocks, r => Assert.True(r.Position.X > -20, $"a boulder at {r.Position}"));
    }

    [Fact]
    public void ANearFieldKeepsThePointsWithinReachOfTheLine()
    {
        var path = new PathTool { Headroom = 4.5f };
        var plan = new PathTool.CavePlan([(new Vector2(0, 0), 0), (new Vector2(10, 0), 0)], [0f, 10f], [5f, 5f], [0f, 0f]);
        var near = CaveRoof.NearField(path, plan, 12, 5, 2f);
        Assert.True(near.ContainsKey(2 * 12 + 5));
        Assert.False(near.ContainsKey(3 * 12 + 5));
    }

    [Fact]
    public void ANearestPointGivesItsDistanceCeilingAndDepthOnTheLine()
    {
        var path = new PathTool { Headroom = 4.5f };
        var plan = new PathTool.CavePlan([(new Vector2(0, 0), 0), (new Vector2(10, 0), 0)], [0f, 10f], [5f, 5f], [0f, 0f]);
        Assert.Equal((3f, 4.5f, 5f), CaveRoof.Nearest(path, plan, 5, 3));
    }
}
