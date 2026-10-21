using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Tidy slots lays the items out in rows: the grid's width when it is known, four when it is not.
/// </summary>
public sealed class InspectorPanelTidyTests
{
    [Fact]
    public void TheRowWidthIsTheGridsWhenKnown()
    {
        Assert.Equal(6, InspectorPanel.TidyWidth(6));
    }

    [Fact]
    public void TheRowWidthIsFourWhenTheGridIsUnknown()
    {
        Assert.Equal(4, InspectorPanel.TidyWidth(0));
    }

    [Fact]
    public void TheFirstRowFillsFromTheLeft()
    {
        Assert.Equal((0, 0), InspectorPanel.TidiedSlot(0, 4));
        Assert.Equal((3, 0), InspectorPanel.TidiedSlot(3, 4));
    }

    [Fact]
    public void ItemsWrapToTheNextRowAtTheWidth()
    {
        Assert.Equal((0, 1), InspectorPanel.TidiedSlot(4, 4));
        Assert.Equal((2, 2), InspectorPanel.TidiedSlot(10, 4));
    }
}
