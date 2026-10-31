using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A dragged row of building pieces (PlaceTool.Row): laid one piece's length apart along whichever of the piece's sides
/// is nearer the drag, either way, at its height, turned as it is, and no longer than MaxRow.
/// </summary>
public class PlaceToolRowTests
{
    /// <summary>A wooden wall placed at this point, turned this much (degrees).</summary>
    private static PlaceTool.Placement Wall(float yaw = 0) => new("woodwall", new Vector3(10, 35, 20), new Vector3(0, yaw, 0), 0, new Vector2(42, 52));

    [Fact]
    public void AWallsRowFollowsItsLength()
    {
        float length = PlaceTool.Length("woodwall", Vector3.UnitX);
        Assert.True(length > 1);
        var first = Wall();

        var row = PlaceTool.Row(first, first.Position + new Vector3(length * 2.2f, 4, 0.3f));

        Assert.Equal(3, row.Count);
        Assert.Equal(first, row[0]);
        for (int i = 0; i < row.Count; i++)
        {
            Assert.Equal(first.Position.X + length * i, row[i].Position.X, 3);
            Assert.Equal(first.Position.Y, row[i].Position.Y);
            Assert.Equal(first.Position.Z, row[i].Position.Z, 3);
            Assert.Equal(first.G.X + length * i, row[i].G.X, 3);
            Assert.Equal(first.Rotation, row[i].Rotation);
        }
    }

    [Fact]
    public void AFloorsRowGoesAlongTheNearerSideEitherWay()
    {
        var first = new PlaceTool.Placement("wood_floor", new Vector3(10, 35, 20), Vector3.Zero, 0, new Vector2(42, 52));
        float wide = PlaceTool.Length("wood_floor", Vector3.UnitX), deep = PlaceTool.Length("wood_floor", Vector3.UnitZ);
        Assert.True(wide > 1 && deep > 1);

        // Mostly backwards across: the row goes west.
        var west = PlaceTool.Row(first, first.Position + new Vector3(-wide, 0, 0.2f));
        Assert.Equal(2, west.Count);
        Assert.Equal(first.Position.X - wide, west[1].Position.X, 3);
        // Mostly through it: along its other side, by that length.
        var deepRow = PlaceTool.Row(first, first.Position + new Vector3(0.1f, 0, -deep * 3));
        Assert.Equal(4, deepRow.Count);
        Assert.Equal(first.Position.Z - deep * 3, deepRow[3].Position.Z, 3);
        Assert.Equal(first.Position.X, deepRow[3].Position.X, 3);
    }

    // A wall has no length through it: dragged that way it is not laid face to face, it stays one wall; a drag mostly
    // through it that reaches along it still rows it along its length.
    [Fact]
    public void AWallIsOnlyRowedAlongItsLength()
    {
        var first = Wall();
        float length = PlaceTool.Length("woodwall", Vector3.UnitX);
        Assert.Equal(1, PlaceTool.Length("woodwall", Vector3.UnitZ));

        Assert.Single(PlaceTool.Row(first, first.Position + new Vector3(0.2f, 0, 6)));
        var row = PlaceTool.Row(first, first.Position + new Vector3(length * 2, 0, 9));
        Assert.Equal(3, row.Count);
        Assert.Equal(first.Position.Z, row[2].Position.Z, 3);
    }

    [Fact]
    public void ATurnedPieceRowsAlongItsOwnSides()
    {
        var first = Wall(90);
        float length = PlaceTool.Length("woodwall", Vector3.UnitX);
        var across = Vector3.Transform(Vector3.UnitX, TerrainEditor.App.BlueprintFormats.FromEuler(first.Rotation));

        var row = PlaceTool.Row(first, first.Position + across * length);

        Assert.Equal(2, row.Count);
        Assert.Equal(first.Position.X + across.X * length, row[1].Position.X, 3);
        Assert.Equal(first.Position.Z + across.Z * length, row[1].Position.Z, 3);
    }

    [Fact]
    public void AShortDragIsTheOnePieceAndALongOneStopsAtMaxRow()
    {
        var first = Wall();
        Assert.Single(PlaceTool.Row(first, first.Position + new Vector3(0.2f, 0, 0)));
        Assert.Equal(PlaceTool.MaxRow, PlaceTool.Row(first, first.Position + new Vector3(10_000, 0, 0)).Count);
    }

    [Fact]
    public void APieceWithoutSnapPointsIsAMetreLong()
    {
        Assert.Equal(1, PlaceTool.Length("no_such_piece_for_row_test", Vector3.UnitX));
        Assert.Null(Hammer.Get("no_such_piece_for_row_test"));
        var first = new PlaceTool.Placement("no_such_piece_for_row_test", Vector3.Zero, Vector3.Zero, 0, Vector2.Zero);
        Assert.Equal(4, PlaceTool.Row(first, new Vector3(3, 0, 0)).Count);
    }
}
