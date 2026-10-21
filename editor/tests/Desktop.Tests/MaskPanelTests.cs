using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mask's panel keeps its boxes and the mask in step.
/// </summary>
public class MaskPanelTests
{
    [AvaloniaFact]
    public void AMaskChangedElsewhereKeepsEverySetting()
    {
        var m = new Mask();
        var panel = new MaskPanel(m);
        m.On = true;
        m.Biomes.Add(8);
        m.SlopeMin = 10;
        m.SlopeMax = 30;
        m.Paint = Mask.PaintRule.Paved;
        m.Notify();
        Assert.Contains(8, m.Biomes);
        Assert.Equal(10, m.SlopeMin);
        Assert.Equal(30, m.SlopeMax);
        Assert.Equal(Mask.PaintRule.Paved, m.Paint);
        Assert.True(panel.BiomeButtons[8].IsChecked);
        Assert.Equal(30, (double)panel.SlopeMax.Value!, 2);
        Assert.Equal((int)Mask.PaintRule.Paved, panel.PaintBox.SelectedIndex);
    }
}
