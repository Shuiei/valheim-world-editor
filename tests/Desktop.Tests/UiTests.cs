using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The app's shared look (Ui): a border made a card gets the cards' look and keeps its own place and child; a tool
/// card's row puts its label, input and value in three columns; a slider shows its value and passes each change on.
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

    [AvaloniaFact]
    public void ARowHasItsLabelInputAndValueInColumns()
    {
        var input = new Slider();
        var value = new TextBlock();
        var g = Ui.Row("Size", input, value, "72,*,Auto");
        Assert.Equal(72, g.ColumnDefinitions[0].Width.Value);
        Assert.Equal("Size", Assert.IsType<TextBlock>(g.Children[0]).Text);
        Assert.Equal((1, 2), (Grid.GetColumn(input), Grid.GetColumn(value)));
        Assert.Equal(2, Ui.Row("Seed", new NumericUpDown()).Children.Count);
    }

    [AvaloniaFact]
    public void ASliderShowsItsValueAndPassesChangesOn()
    {
        var shown = new TextBlock();
        double got = -1;
        var s = Ui.Slide(0, 10, 1, 2, shown, v => $"{v:0} m", v => got = v);
        Assert.Equal(("2 m", 12.0), (shown.Text, shown.FontSize));
        s.Value = 4;
        Assert.Equal(("4 m", 4.0), (shown.Text, got));
    }
}
