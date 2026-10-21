using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Each overlay layer's colour and whether the ground hides it.
/// </summary>
public class OverlaysStyleTests
{
    [Theory]
    [InlineData(Overlays.Layer.Borders, 0.25f, true)]
    [InlineData(Overlays.Layer.Markers, 1f, true)]
    [InlineData(Overlays.Layer.Wards, 0.9f, false)]
    [InlineData(Overlays.Layer.Stations, 0.9f, false)]
    [InlineData(Overlays.Layer.Flatten, 0.95f, false)]
    [InlineData(Overlays.Layer.FlattenEdge, 0.35f, false)]
    public void EachLayerHasItsStyle(Overlays.Layer layer, float opacity, bool hidden)
    {
        var (color, isHidden) = Overlays.Style(layer);
        Assert.Equal(opacity, color.W, 3);
        Assert.Equal(hidden, isHidden);
    }
}
