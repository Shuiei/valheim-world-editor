using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The neighbour index's search: an item counts when it lies in the point's own cell or one of the eight cells round it,
/// and only the items the test accepts count.
/// </summary>
public class CellNeighbourIndexAnyNearTests
{
    [Fact]
    public void AnItemInANeighbouringCellIsFound()
    {
        // Cell width 10: (5, 5) is in cell (0, 0), and (14, 5) is in cell (1, 0), next to it.
        var index = new CellNeighbourIndex<string>(10f);
        index.Add(5, 5, "a");
        Assert.True(index.AnyNear(14, 5, s => s == "a"));
    }

    [Fact]
    public void AnItemTwoCellsAwayIsNotFound()
    {
        // (35, 5) is in cell (3, 0): cell (0, 0) is not among its neighbours.
        var index = new CellNeighbourIndex<string>(10f);
        index.Add(5, 5, "a");
        Assert.False(index.AnyNear(35, 5, s => s == "a"));
    }

    [Fact]
    public void AnItemTheTestRejectsIsNotFound()
    {
        var index = new CellNeighbourIndex<string>(10f);
        index.Add(5, 5, "a");
        Assert.False(index.AnyNear(5, 5, s => s == "z"));
    }
}
