using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tool rail on its own: Alt + click's flatten height, a new pattern and the Cave button with nobody listening, and
/// the Workshop's rail sending a brush back to View.
/// </summary>
public class ToolPanelBranchesTests
{
    [AvaloniaFact]
    public void FlattenToLevelsToTheHeightGiven()
    {
        var t = new ToolPanel();
        t.FlattenTo(33.27f);
        Assert.Equal(BrushTool.Flatten, t.Tool);
        Assert.False(t.TargetFromClickBox.IsChecked);
        Assert.Equal(33.3f, t.Brush.Target, 3);
        Assert.Equal(33.3m, t.TargetBox.Value);
    }

    [AvaloniaFact]
    public void ANewPatternOrTheCaveWithNobodyListeningStillWork()
    {
        var t = new ToolPanel();
        var before = t.Brush.Noise;
        t.NewPattern(5);
        Assert.NotSame(before, t.Brush.Noise);
        var cave = t.Rail.GetLogicalDescendants().OfType<Button>().First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(x => x.Text == "Cave"));
        cave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ToolMode.Path, t.Mode);
    }

    [AvaloniaFact]
    public void TheWorkshopsRailSendsABrushBackToView()
    {
        var t = new ToolPanel();
        t.Choose(BrushTool.Raise);
        t.SetWorkshop(true);
        Assert.Null(t.Tool);
        Assert.Equal(ToolMode.View, t.Mode);
        // Already on a tool the Workshop has: kept.
        t.SetWorkshop(false);
        t.ChooseMode(ToolMode.Select);
        t.SetWorkshop(true);
        Assert.Equal(ToolMode.Select, t.Mode);
    }
}
