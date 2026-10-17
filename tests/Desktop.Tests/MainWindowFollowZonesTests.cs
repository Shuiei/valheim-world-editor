using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The zones the live following asks the game for: every zone of the area, once, from its first corner.
/// </summary>
public sealed class MainWindowFollowZonesTests
{
    [Fact]
    public void ItListsEveryZoneOfTheAreaOnce()
    {
        var scene = TestScenes.Flat(2).Scene;
        var zones = MainWindow.ZonesOf(scene);
        Assert.Equal(scene.ZonesX * scene.ZonesZ, zones.Count);
        Assert.Equal(zones.Distinct().Count(), zones.Count);
    }

    [Fact]
    public void TheFirstAndLastZonesAreTheAreasCorners()
    {
        var scene = TestScenes.Flat(2).Scene;
        var zones = MainWindow.ZonesOf(scene);
        Assert.Equal((scene.X0, scene.Z0), zones[0]);
        Assert.Equal((scene.X0 + scene.ZonesX - 1, scene.Z0 + scene.ZonesZ - 1), zones[^1]);
    }
}
