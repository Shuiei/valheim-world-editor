using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A number box in the inspector outlined in orange when its value is bad (a slot outside its container), and left with
/// its own border when the value is fine.
/// </summary>
[Collection("World files")]
public class InspectorNumberBorderTests
{
    [AvaloniaFact]
    public void ABadValueGetsAnOrangeBorder()
    {
        var box = new NumericUpDown();
        Assert.Same(box, InspectorPanel.WithBorder(box, bad: true));
        Assert.Equal(Brushes.Orange, box.BorderBrush);
    }

    [AvaloniaFact]
    public void AGoodValueKeepsItsBorder()
    {
        var box = new NumericUpDown { BorderBrush = Brushes.Blue };
        InspectorPanel.WithBorder(box, bad: false);
        Assert.Equal(Brushes.Blue, box.BorderBrush);
    }
}
