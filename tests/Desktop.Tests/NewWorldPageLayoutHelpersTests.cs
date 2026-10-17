using TerrainEditor.App;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The new world page's small layout helpers: a labelled row, a labelled distance box with its unit, and a zoom button.
/// </summary>
public class NewWorldPageLayoutHelpersTests
{
    [AvaloniaFact]
    public void ARowHasItsLabelThenItsControl()
    {
        var box = new TextBox();
        var row = NewWorldPage.Row("Name", box);
        Assert.Equal("Name", ((TextBlock)row.Children[0]).Text);
        Assert.Same(box, row.Children[1]);
    }

    [AvaloniaFact]
    public void ADistanceRowHasItsLabelThenTheBoxWithTheMetresUnit()
    {
        var box = new NumericUpDown();
        var row = NewWorldPage.Metres("Within", box);
        Assert.Equal("Within", ((TextBlock)row.Children[0]).Text);
        var unit = Assert.IsType<StackPanel>(row.Children[1]);
        Assert.Same(box, unit.Children[0]);
        Assert.Equal(Lang.T("common.metres"), ((TextBlock)unit.Children[1]).Text);
    }

    [AvaloniaFact]
    public void AZoomButtonShowsItsSymbolAtAFixedSize()
    {
        var button = NewWorldPage.ZoomButton("+", "newWorld.zoomIn");
        Assert.Equal("+", button.Content);
        Assert.Equal(30, button.Width);
        Assert.Equal(30, button.Height);
    }
}
