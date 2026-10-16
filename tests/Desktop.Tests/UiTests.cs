using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The app's shared look (Ui): a border made a card gets the cards' look and keeps its own place and child.
/// </summary>
public class UiTests
{
    [AvaloniaFact]
    public void ACardKeepsItsOwnPlaceAndChild()
    {
        var child = new TextBlock();
        var b = Ui.AsCard(new Border { VerticalAlignment = VerticalAlignment.Top, Width = 200, Child = child });
        Assert.Equal((Ui.Panel, Ui.Line, Ui.Pad, 10.0), (b.Background, b.BorderBrush, b.Padding, b.CornerRadius.TopLeft));
        Assert.Equal(1, b.BoxShadow.Count);
        Assert.Equal((VerticalAlignment.Top, 200.0), (b.VerticalAlignment, b.Width));
        Assert.Same(child, b.Child);
    }
}
