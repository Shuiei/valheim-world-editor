using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Moving an item to another slot: the two items swap when both slots hold one, and the one that is there moves alone
/// into an empty slot.
/// </summary>
public sealed class InspectorPanelSwapTests
{
    [Fact]
    public void TwoItemsSwapSlots()
    {
        var a = new InspectorPanel.ItemRow { Name = "Coins", X = 0, Y = 0 };
        var b = new InspectorPanel.ItemRow { Name = "Wood", X = 1, Y = 0 };
        InspectorPanel.SwapSlots([a, b], (0, 0), (1, 0));
        Assert.Equal((1, 0), (a.X, a.Y));
        Assert.Equal((0, 0), (b.X, b.Y));
    }

    [Fact]
    public void AnItemMovesAloneIntoAnEmptySlot()
    {
        var a = new InspectorPanel.ItemRow { Name = "Coins", X = 2, Y = 3 };
        InspectorPanel.SwapSlots([a], (2, 3), (5, 1));
        Assert.Equal((5, 1), (a.X, a.Y));
    }

    [Fact]
    public void AnEmptySlotMovesNothing()
    {
        var a = new InspectorPanel.ItemRow { Name = "Coins", X = 2, Y = 3 };
        InspectorPanel.SwapSlots([a], (0, 0), (5, 1));
        Assert.Equal((2, 3), (a.X, a.Y));
    }
}
