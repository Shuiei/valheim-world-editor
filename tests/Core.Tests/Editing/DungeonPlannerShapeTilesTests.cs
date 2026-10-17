using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tiles of a dungeon room of a given shape: the whole box for a rectangle, and the corners the shape cuts away
/// (two tiles deep in a big room, one in a small one).
/// </summary>
public class DungeonPlannerShapeTilesTests
{
    [Fact]
    public void ARectangleRoomKeepsEveryTileOfItsBox()
    {
        var tiles = DungeonPlanner.ShapeTiles((0, 0, 4, 4), (0, 1), DungeonPlan.Shape.Rect);

        Assert.Equal(16, tiles.Count);
    }

    [Fact]
    public void AnLRoomLeavesOutTheTwoTilesOfItsCutCorner()
    {
        var tiles = DungeonPlanner.ShapeTiles((0, 0, 4, 4), (0, 1), DungeonPlan.Shape.L);

        Assert.Equal(14, tiles.Count);
    }

    [Fact]
    public void ACrossRoomInASmallBoxCutsOneTileFromEachCorner()
    {
        var tiles = DungeonPlanner.ShapeTiles((0, 0, 4, 4), (0, 1), DungeonPlan.Shape.Cross);

        Assert.Equal(12, tiles.Count);
    }

    [Fact]
    public void ACrossRoomInABigBoxCutsTwoTilesDeepFromEachCorner()
    {
        var tiles = DungeonPlanner.ShapeTiles((0, 0, 10, 10), (0, 1), DungeonPlan.Shape.Cross);

        Assert.Equal(84, tiles.Count);
    }
}
