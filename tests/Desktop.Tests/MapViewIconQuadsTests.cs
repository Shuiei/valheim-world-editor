using ValheimGen;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The locations' icons as quads: centred on the location, sized on screen or in metres, the picture upright.</summary>
public class MapViewIconQuadsTests
{
    /// <summary>One icon of 40 × 20 pixels at the left of the icons' texture.</summary>
    private static readonly Dictionary<string, MapView.IconSlot> Slots = new() { ["Hut"] = new MapView.IconSlot(0f, 0f, 0.5f, 1f, 40, 20) };

    [Fact]
    public void AnIconIsSizedOnScreenUnderFiveAndInMetresFromFive()
    {
        // 2 m a pixel: 40 pixels tall at size 1 is 80 m, twice as wide (its picture's shape).
        float[] q = MapView.IconQuads([new LocationIcons.Icon(100, 200, "Hut", 1f)], Slots, 2f);
        Assert.Equal(24, q.Length);
        Assert.Equal((20f, 240f, 0f, 0f), (q[0], q[1], q[2], q[3]));
        Assert.Equal((180f, 160f, 0.5f, 1f), (q[8], q[9], q[10], q[11]));
        // 50: 50 m wide whatever the zoom.
        q = MapView.IconQuads([new LocationIcons.Icon(0, 0, "Hut", 50f)], Slots, 7f);
        Assert.Equal((-25f, 12.5f), (q[0], q[1]));
    }

    [Fact]
    public void AnIconTheMinimapLacksIsLeftOut() =>
        Assert.Empty(MapView.IconQuads([new LocationIcons.Icon(0, 0, "Nowhere", 1f)], Slots, 2f));
}
