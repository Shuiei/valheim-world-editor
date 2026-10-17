using Xunit;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// ClaudeBuilder's layouts in stone and shingle: a stone floor, stone walls in 1 m rows with a door taking two of
/// them, a 26 degree shingle roof whose ridge runs along z, the materials and angles it does not know, and layouts of
/// more than MaxPieces refused before they are laid out.
/// </summary>
public class ClaudeBuilderTests
{
    [Fact]
    public void AStoneFloorIsTwoMetreTiles()
    {
        var floor = ClaudeBuilder.Floor(4, 4, 0, 0, 35, "Stone");
        Assert.Equal(4, floor.Count);
        Assert.All(floor, p => Assert.Equal("stone_floor_2x2", p.Prefab));
        Assert.Equal([1f, 3f], floor.Select(p => p.X).Distinct().Order());
        Assert.Single(floor.Select(p => p.Y).Distinct());
        Assert.Equal("A floor is wood or stone.", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Floor(0, 0, 4, 4, 35, "glass")).Message);
    }

    // 4 x 4 m, 3 m high: 2 spots a side, 3 rows each (24); at the west door, the door and the one row above it.
    [Fact]
    public void StoneWallsAreOneMetreRowsWithTheirDoors()
    {
        var walls = ClaudeBuilder.Walls(0, 0, 4, 4, 34, 3, "stone", [new ClaudeBuilder.Door("West", 3.2f)]);
        Assert.Equal(24 - 3 + 2, walls.Count);
        var door = Assert.Single(walls, p => p.Prefab == "wood_door");
        Assert.Equal((0f, 3f, 90f), (door.X, door.Z, door.Yaw));
        Assert.Single(walls, p => p.Prefab == "stone_wall_2x1" && p.X == 0 && p.Z == 3);
        Assert.Equal(3, walls.Where(p => p.Prefab == "stone_wall_2x1").Select(p => p.Y).Distinct().Count());
        Assert.All(walls.Where(p => p.X is 0 or 4 && p.Z is 1 or 3), p => Assert.Equal(90, p.Yaw));
        Assert.Equal("Walls are wood or stone.", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Walls(0, 0, 4, 4, 34, 3, "mud", [])).Message);
    }

    // 6 m across and 10 m along z: one row of slopes a side and a ridge at each of the five 2 m spots, turned to run
    // along z; each gable a half wall under the ridge and a sloped wall either side.
    [Fact]
    public void AShingleRoofAlongZ()
    {
        var roof = ClaudeBuilder.Roof(-3, -5, 3, 5, 38, 26, "Shingle", gables: true);
        Assert.Equal(5, roof.Count(p => p.Prefab == "darkwood_roof_top"));
        Assert.All(roof.Where(p => p.Prefab == "darkwood_roof_top"), p => Assert.Equal((0f, 90f), (p.X, p.Yaw)));
        Assert.Equal(10, roof.Count(p => p.Prefab == "darkwood_roof"));
        Assert.Equal([90f, 270f], roof.Where(p => p.Prefab == "darkwood_roof").Select(p => p.Yaw).Distinct().Order());
        Assert.Equal(2, roof.Count(p => p.Prefab == "wood_wall_half"));
        Assert.Equal(4, roof.Count(p => p.Prefab == "wood_wall_roof"));
        Assert.All(roof.Where(p => p.Prefab.StartsWith("wood_wall", StringComparison.Ordinal)), p => Assert.Equal(5, MathF.Abs(p.Z)));
        Assert.Equal(21, roof.Count);
        // Without gables: the slopes and ridges only.
        Assert.Equal(15, ClaudeBuilder.Roof(-3, -5, 3, 5, 38, 26, "shingle", gables: false).Count);
        Assert.Equal("A roof is thatch or shingle.", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Roof(-3, -5, 3, 5, 38, 26, "tin", true)).Message);
        Assert.Equal("A roof is 26 or 45 degrees.", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Roof(-3, -5, 3, 5, 38, 60, "thatch", true)).Message);
    }

    // More than 5000 pieces is refused before any is laid out (CheckFloor and CheckWalls need no height, so they are
    // asked before one is read from the ground): a huge floor, walls ten thousand rows high, a huge roof.
    [Fact]
    public void LayoutsOfMoreThanMaxPiecesAreRefused()
    {
        Assert.Equal(ClaudeBuilder.MaxPieces, ClaudeBuilder.Floor(0, 0, 200, 100, 35, "wood").Count);
        ClaudeBuilder.CheckFloor(0, 0, 200, 100);
        Assert.Contains("more than 5000", Assert.Throws<ArgumentException>(() => ClaudeBuilder.CheckFloor(0, 0, 202, 100)).Message);
        Assert.Contains("more than 5000", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Floor(0, 0, 5000, 5000, 35, "wood")).Message);
        ClaudeBuilder.CheckWalls(0, 0, 4, 4, 625, "stone");
        Assert.Contains("more than 5000", Assert.Throws<ArgumentException>(() => ClaudeBuilder.CheckWalls(0, 0, 4, 4, 626, "stone")).Message);
        Assert.Contains("more than 5000", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Walls(0, 0, 4, 4, 34, 1e9f, "stone", [])).Message);
        Assert.Contains("more than 5000", Assert.Throws<ArgumentException>(() => ClaudeBuilder.Roof(0, 0, 3e7f, 2e7f, 40, 45, "thatch", true)).Message);
    }

    [Fact]
    public void AThatchRoofsPiecesAreTheWoodRoofAtThatAngle()
    {
        Assert.Equal(("wood_roof", "wood_roof_top", "wood_wall_roof"), ClaudeBuilder.RoofNames("Thatch", 26));
    }

    [Fact]
    public void AShingleRoofAt45DegreesHasItsOwnPieces()
    {
        Assert.Equal(("darkwood_roof_45", "darkwood_roof_top_45", "wood_wall_roof_45"), ClaudeBuilder.RoofNames("shingle", 45));
    }

    [Fact]
    public void AnUnknownRoofMaterialIsRefused()
    {
        Assert.Equal("A roof is thatch or shingle.", Assert.Throws<ArgumentException>(() => ClaudeBuilder.RoofNames("tin", 26)).Message);
    }

    [Fact]
    public void ASlopeCourseGoesOnBothSidesOfTheRidgeAndThenTheRidgePiece()
    {
        var list = new List<ClaudeTools.PieceSpec>();
        ClaudeBuilder.Slopes(list, new ClaudeBuilder.Ridge(true, 0, 0, 4, 2), [0f], 1, 10f, 1f, 0f, "s", "r");
        Assert.Equal([("s", 2f), ("s", -2f), ("r", 0f)], list.Select(p => (p.Prefab, p.Z)));
    }

    [Fact]
    public void AWallOfMoreThanTwoMetresIsFullWallsAndAHalfWallForTheRest()
    {
        var list = new List<ClaudeTools.PieceSpec>();
        ClaudeBuilder.FillWall(list, 0, 0, 0, 5, 0, 0, 0);
        Assert.Equal([("woodwall", 0f), ("woodwall", 2f), ("wood_wall_half", 4f)], list.Select(p => (p.Prefab, p.Y!.Value)));
    }

    [Fact]
    public void AWallShorterThanAMetreIsNothing()
    {
        var list = new List<ClaudeTools.PieceSpec>();
        ClaudeBuilder.FillWall(list, 0, 0, 0, 0.5f, 0, 0, 0);
        Assert.Empty(list);
    }

    [Fact]
    public void EachGableEndOfAShallowRidgeIsTwoSlopedPieces()
    {
        var list = new List<ClaudeTools.PieceSpec>();
        ClaudeBuilder.Gables(list, new ClaudeBuilder.Ridge(true, 0, 0, 4, 2), 38f, 0.5f, "wood_wall_roof");
        Assert.Equal(4, list.Count);
        Assert.All(list, p => Assert.Equal("wood_wall_roof", p.Prefab));
    }

    [Fact]
    public void ASlopeWithItsSnapPointsGivesItsRiseRunPitchAndHighEdge()
    {
        var slope = new PieceCatalog.Info("s", 0, 0, 0, 0, 0, 0, 0, 0) { Snaps = [[0, 0, 2], [0, 1, -2]] };
        Assert.Equal((1f, 4f, 0.25f, 1f), ClaudeBuilder.SlopeOf(slope, "s"));
    }

    [Fact]
    public void ASlopeWithoutTwoSnapPointsCannotBeLaid()
    {
        var slope = new PieceCatalog.Info("s", 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Contains("has no snap points", Assert.Throws<ArgumentException>(() => ClaudeBuilder.SlopeOf(slope, "s")).Message);
    }
}
