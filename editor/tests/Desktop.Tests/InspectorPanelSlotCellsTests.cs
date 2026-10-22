using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The names the slots show: a break the eye does not see between the words of a name written as one, so a narrow slot
/// wraps there and not in the middle of a word.
/// </summary>
public class InspectorPanelSlotCellsTests
{
    [Theory]
    [InlineData("IronScrap", "Iron​Scrap")]
    [InlineData("TrophyGreydwarfBrute", "Trophy​Greydwarf​Brute")]
    [InlineData("Wood", "Wood")]
    [InlineData("SledgeIron2", "Sledge​Iron​2")]
    [InlineData("piece_chest", "piece_​chest")]
    [InlineData("BBQ", "BBQ")]
    public void ANameBreaksBetweenItsWords(string name, string shown)
    {
        Assert.Equal(shown, InspectorPanel.SlotName(name));
        Assert.Equal(name, InspectorPanel.SlotName(name).Replace("​", ""));
    }
}
