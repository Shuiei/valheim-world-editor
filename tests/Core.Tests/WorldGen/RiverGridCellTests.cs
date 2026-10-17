using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The river grid: 64 m cells centred on multiples of 64, a point's cell, whether a point is near enough to a cell to
/// count for it, and the cells a river point is added to.
/// </summary>
public sealed class RiverGridCellTests
{
    /// <summary>The origin is the middle of cell (0, 0), which runs from -32 to 32 metres.</summary>
    [Fact]
    public void TheOriginIsInTheMiddleCell()
    {
        Assert.Equal(new Vector2i(0, 0), WorldGenerator.GetRiverGrid(0f, 0f));
        Assert.Equal(new Vector2i(0, 0), WorldGenerator.GetRiverGrid(-32f, 31.9f));
    }

    /// <summary>The next cell starts at 32 metres, so 32 is in cell 1 and 31.9 in cell 0.</summary>
    [Fact]
    public void ACellStartsHalfWayBetweenItsCentres()
    {
        Assert.Equal(1, WorldGenerator.GetRiverGrid(32f, 0f).x);
        Assert.Equal(0, WorldGenerator.GetRiverGrid(31.9f, 0f).x);
    }

    /// <summary>Negative coordinates go to the cell below, not towards zero.</summary>
    [Fact]
    public void NegativeCoordinatesRoundDownToTheCellBelow()
    {
        Assert.Equal(-1, WorldGenerator.GetRiverGrid(-33f, 0f).x);
        Assert.Equal(-1, WorldGenerator.GetRiverGrid(0f, -33f).y);
    }

    /// <summary>A point at the centre of its cell is inside it, with no width to add.</summary>
    [Fact]
    public void APointInsideItsCellIsInsideTheRiverGrid()
    {
        Assert.True(WorldGenerator.InsideRiverGrid(new Vector2i(0, 0), new Vector2(10f, -5f), 0f));
    }

    /// <summary>With no width the reach is 32 metres: a point exactly 32 metres from the cell's centre is outside.</summary>
    [Fact]
    public void APointExactlyAReachAwayFromTheCellCentreIsOutside()
    {
        Assert.False(WorldGenerator.InsideRiverGrid(new Vector2i(0, 0), new Vector2(32f, 0f), 0f));
        Assert.True(WorldGenerator.InsideRiverGrid(new Vector2i(0, 0), new Vector2(31f, 0f), 0f));
    }

    /// <summary>A river point is added to every cell its width reaches: a 64 m wide point reaches the eight cells round its own.</summary>
    [Fact]
    public void AWideRiverPointIsAddedToTheNineCellsAroundItsOwn()
    {
        var cells = new Dictionary<Vector2i, List<WorldGenerator.RiverPoint>>();

        PrivateMember.Call(typeof(WorldGenerator), "AddToCells", null, cells, new Vector2(0f, 0f), 64f);

        Assert.Equal(9, cells.Count);
        Assert.All(cells.Values, list => Assert.Single(list));
    }

    /// <summary>A narrow river point is added to its own cell only.</summary>
    [Fact]
    public void ANarrowRiverPointIsAddedToItsOwnCellOnly()
    {
        var cells = new Dictionary<Vector2i, List<WorldGenerator.RiverPoint>>();

        PrivateMember.Call(typeof(WorldGenerator), "AddToCells", null, cells, new Vector2(0f, 0f), 0f);

        Assert.Equal([new Vector2i(0, 0)], [.. cells.Keys]);
    }
}
