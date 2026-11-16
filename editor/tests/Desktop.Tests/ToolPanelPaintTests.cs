using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Paint brush: one rail button for the four paints, last of the Sculpt section. Keys 6 to 9 pick it with their
/// paint, its options show the paints as swatches (the chosen one lit, Clear named Clear paint), and the rail button
/// keeps the paint last chosen, in its colour.
/// </summary>
public class ToolPanelPaintTests
{
    /// <summary>
    /// Clicks the button.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// The texts a control shows, in order.
    /// </summary>
    private static List<string?> Texts(Control c) => [.. c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)];

    /// <summary>
    /// The colour of the Paint button's picture on the rail.
    /// </summary>
    private static IBrush? RailSwatch(ToolPanel t) =>
        t.ButtonOf(BrushTool.PaintDirt).GetLogicalDescendants().OfType<Border>().Single().Background;

    [AvaloniaFact]
    public void OneRailButtonStandsForTheFourPaints()
    {
        var t = new ToolPanel();
        var button = t.ButtonOf(BrushTool.PaintDirt);
        Assert.All(ToolPanel.Paints, p => Assert.Same(button, t.ButtonOf(p)));
        Assert.NotSame(button, t.ButtonOf(BrushTool.Restore));
        Assert.Equal(["Paint", "6-9"], Texts(button));
        Assert.StartsWith("Paint (6 to 9)", ToolTip.GetTip(button) as string);
    }

    [AvaloniaFact]
    public void TheKeysSixToNineChooseThePaintBrushWithTheirPaint()
    {
        var t = new ToolPanel();
        foreach (var (key, paint) in new[] { ("6", BrushTool.PaintDirt), ("7", BrushTool.PaintCultivated), ("8", BrushTool.PaintPaved), ("9", BrushTool.PaintClear) })
        {
            Assert.True(t.Key(key));
            Assert.Equal((ToolMode.Brush, (BrushTool?)paint, paint), (t.Mode, t.Tool, t.Paint));
            Assert.Contains("on", t.ButtonOf(paint).Classes);
            Assert.All(ToolPanel.Paints, p => Assert.Equal(p == paint, t.PaintButtons[p].Classes.Contains("on")));
            Assert.Equal(key, ToolPanel.KeyOf(paint));
        }
    }

    [AvaloniaFact]
    public void TheOptionsShowThePaintsAsSwatches()
    {
        var t = new ToolPanel();
        t.Choose(BrushTool.PaintDirt);
        Assert.Equal(["Dirt", "6", "Cultivate", "7", "Paved", "8", "Clear paint", "9"], ToolPanel.Paints.SelectMany(p => Texts(t.PaintButtons[p])).ToList());
        Assert.Equal("Paint", Texts(t.Options)[0]);
        Assert.StartsWith("Paint bare dirt, like a path.", Texts(t.Options)[1]);
        Assert.StartsWith("Paved (8)", ToolTip.GetTip(t.PaintButtons[BrushTool.PaintPaved]) as string);

        // A swatch lays its paint: the card keeps its title, its help line follows the paint.
        Click(t.PaintButtons[BrushTool.PaintClear]);
        Assert.Equal((BrushTool?)BrushTool.PaintClear, t.Tool);
        Assert.Equal("Paint", Texts(t.Options)[0]);
        Assert.StartsWith("Remove paint: back to the biome's own ground.", Texts(t.Options)[1]);

        // The swatches are the Paint brush's own: a sculpt brush's options hide them.
        var swatches = (Control)t.PaintButtons[BrushTool.PaintDirt].Parent!;
        Assert.True(swatches.IsVisible);
        t.Choose(BrushTool.Raise);
        Assert.False(swatches.IsVisible);
        Assert.Equal("Raise", Texts(t.Options)[0]);
        Assert.DoesNotContain("on", t.ButtonOf(BrushTool.PaintClear).Classes);
    }

    [AvaloniaFact]
    public void TheRailButtonKeepsThePaintLastChosen()
    {
        var t = new ToolPanel();
        // Dirt at first.
        Assert.Equal(((ISolidColorBrush)ToolPanel.PaintColour(BrushTool.PaintDirt)).Color, ((ISolidColorBrush)RailSwatch(t)!).Color);
        Click(t.ButtonOf(BrushTool.PaintDirt));
        Assert.Equal((BrushTool?)BrushTool.PaintDirt, t.Tool);

        Assert.True(t.Key("8"));
        Assert.Equal(((ISolidColorBrush)ToolPanel.PaintColour(BrushTool.PaintPaved)).Color, ((ISolidColorBrush)RailSwatch(t)!).Color);
        // Another tool in between: the button comes back with Paved, the brush's settings as they were.
        t.SizeSlider.Value = 11;
        t.ChooseSelect();
        Assert.Null(t.Tool);
        Assert.Equal(BrushTool.PaintPaved, t.Paint);
        Click(t.ButtonOf(BrushTool.PaintDirt));
        Assert.Equal((BrushTool?)BrushTool.PaintPaved, t.Tool);
        Assert.Equal(11f, t.Brush.Radius);

        // Clear paint: half grass, half bare ground.
        Assert.True(t.Key("9"));
        Assert.IsType<LinearGradientBrush>(RailSwatch(t));
    }

    [AvaloniaFact]
    public void TheWorkshopHasNoPaintBrush()
    {
        var t = new ToolPanel();
        t.Key("7");
        t.SetWorkshop(true);
        Assert.Null(t.Tool);
        Assert.False(t.ButtonOf(BrushTool.PaintCultivated).IsVisible);
        Assert.True(t.Key("7"));
        Assert.Null(t.Tool);
        t.SetWorkshop(false);
        Click(t.ButtonOf(BrushTool.PaintDirt));
        Assert.Equal((BrushTool?)BrushTool.PaintCultivated, t.Tool);
    }
}
