using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The planner's geometry helpers: the squared distance between two spaces' middles, whether a tile lies in a cell a tall
/// room of the level below rises through, and the corridor made over a set of tiles.
/// </summary>
public class DungeonPlannerGeometryTests
{
    /// <summary>
    /// A space whose box starts at these tiles and spans this many.
    /// </summary>
    private static DungeonPlan.Space Box(int x0, int z0, int w, int h) => new() { X0 = x0, Z0 = z0, W = w, H = h };

    [Fact]
    public void TheDistanceBetweenTwoSpacesMiddlesIsSquaredOverBothAxes()
    {
        // Middles at x 2 and 8 (dx 6) and at z 2 and 10 (dz 8): 36 + 64.
        var a = Box(0, 0, 2, 2);
        var b = Box(3, 4, 2, 2);

        Assert.Equal(100f, (float)PrivateCall.Static(typeof(DungeonPlanner), "Dist2", a, b)!);
    }

    [Fact]
    public void ATileInACellATallRoomAboveRisesThroughIsOverTall()
    {
        var (p, level0, _) = DungeonPlannerProbe.Entered();
        var level1 = p.AddLevel();
        // Cell (0, 0) is tiles 0 to 3 along each axis.
        level1.TallCells.Add((0, 0));

        Assert.True((bool)p.Call("OverTall", level0, (1, 2))!);
        Assert.False((bool)p.Call("OverTall", level0, (4, 2))!);
    }

    [Fact]
    public void TheTopLevelHasNothingAboveItToBeOverTall()
    {
        var (p, _, _) = DungeonPlannerProbe.Entered();
        var level1 = p.AddLevel();
        level1.TallCells.Add((0, 0));

        Assert.False((bool)p.Call("OverTall", level1, (1, 2))!);
    }

    [Fact]
    public void ACorridorTakesExactlyItsTilesInTheirBounds()
    {
        var (p, _, _) = DungeonPlannerProbe.Entered();
        var level1 = p.AddLevel();
        var tiles = new List<(int, int)> { (0, 0), (1, 0), (2, 0), (2, 1) };

        var corridor = (DungeonPlan.Space)p.Call("NewCorridor", level1, tiles, (1, 0))!;

        Assert.True(corridor.Corridor);
        Assert.Equal("Corridor", corridor.Name);
        Assert.Equal((0, 0, 3, 2), (corridor.X0, corridor.Z0, corridor.W, corridor.H));
        Assert.True(corridor.Tiles.SetEquals(tiles));
        Assert.All(tiles, t => Assert.Same(corridor, level1.Owner[t]));
    }
}
