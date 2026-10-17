using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tool rail's buttons and keys, its Forget stamp with no loaded picture, and the Path panel's natural-look sliders
/// (bumps and their size).
/// </summary>
public class ToolPanelTests
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area.
    /// </summary>
    private static MainWindow Open()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return w;
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheRailsButtonsAndKeysChooseTools()
    {
        var w = Open();
        Click(w.Tools.ButtonOf(BrushTool.Smooth));
        Assert.Equal(BrushTool.Smooth, w.Tools.Tool);
        Click(w.Tools.SelectButton);
        Assert.True(w.Tools.SelectMode);
        Assert.Null(w.Tools.Tool);
        // A key that is not on the rail is left to others.
        Assert.False(w.Tools.Key("Z"));
        Assert.True(w.Tools.Key("O"));
        Assert.Equal(BrushTool.Erode, w.Tools.Tool);
    }

    [AvaloniaFact]
    public void TheRailIsInSections()
    {
        var w = Open();
        // Each section's caption, then its buttons' names, in order.
        var rail = w.Tools.Rail.GetLogicalDescendants().OfType<StackPanel>().First(p => p.Children.FirstOrDefault() is TextBlock);
        var names = rail.Children.Select(c => c is TextBlock t ? t.Text : ((TextBlock)((StackPanel)((Grid)((Button)c).Content!).Children[0]).Children[1]).Text).ToList();
        Assert.Equal(
            [
                "SCULPT", "Raise", "Lower", "Flatten", "Smooth", "Natural", "Erode", "Restore",
                "EDIT", "Area", "Path", "Place",
                "PAINT", "Dirt", "Cultivate", "Paved", "Clear",
                "CREATE", "Shape", "Mountain", "Cave", "Dungeon", "Script", "Blueprints",
                "TOOLS", "Select", "Measure", "View",
            ],
            names);
    }

    [AvaloniaFact]
    public void ForgetStampNeedsALoadedPicture()
    {
        var w = Open();
        w.Tools.Choose(BrushTool.Raise);
        string? said = null;
        w.Tools.Message += t => said = t.Shown;
        // A built-in stamp chosen: it stays.
        w.Tools.ShapeBox.SelectedIndex = 4;
        int stamps = w.Tools.StampList.Count;
        Click(w.Tools.ForgetStampButton);
        Assert.Equal("Choose a loaded picture as Shape first (built-in stamps stay).", said);
        Assert.Equal(stamps, w.Tools.StampList.Count);
    }

    [AvaloniaFact]
    public void ThePathsNaturalLookHasBumpSliders()
    {
        var w = Open();
        w.Tools.ChooseMode(ToolMode.Path);
        w.PathPanel.NaturalBox.IsChecked = true;
        var brush = w.Session!.Brush;
        var sliders = w.PathPanel.Card.GetLogicalDescendants().OfType<Slider>().Where(s => s.Maximum is 4 or 60).ToList();
        Assert.Equal(2, sliders.Count);
        sliders.Single(s => s.Maximum == 4).Value = 2.5;
        sliders.Single(s => s.Maximum == 60).Value = 30;
        Assert.Equal(2.5f, brush.NoiseAmp, 3);
        Assert.Equal(30f, brush.NoiseSize, 3);
        Assert.Contains("2.5 m", string.Join(" ", w.PathPanel.Card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
    }
}
