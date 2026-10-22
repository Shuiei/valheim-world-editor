using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The zone filter's panel laid out in the side panel's width: nothing runs past its edge.
/// </summary>
public class MapPageZonesPanelTests
{
    // "No buildings in the zone or within [2] zone(s)" in the side panel's width: the words wrap, the box and "zone(s)"
    // stay inside (in a row, "zone(s)" was cut off at the panel's edge).
    [AvaloniaFact]
    public void TheNoBuildingsRowFitsTheSidePanel()
    {
        var m = new MapPage();
        var row = Assert.IsAssignableFrom<Panel>(m.NoBuildBox.Parent);
        ((Panel)row.Parent!).Children.Remove(row);
        LaidOut.In(row, 300, () =>
        {
            foreach (var child in row.Children)
            {
                Assert.True(child.Bounds.Right <= 300.5, $"{child} ends at {child.Bounds.Right}");
            }
        });
    }
}
