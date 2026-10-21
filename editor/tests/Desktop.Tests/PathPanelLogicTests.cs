using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path panel's logic: a cave preset fills its values and says so, the rows follow the action, and the refresh
/// with no line says there is none.
/// </summary>
public class PathPanelLogicTests
{
    [AvaloniaFact]
    public void ACavePresetFillsItsValuesAndItsHelp()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var panel = w.PathPanel;
        var preset = PathTool.CavePresets()[panel.CavePresetBox.SelectedIndex];
        panel.CavePreset(false);
        Assert.Equal(preset.Width, panel.WidthSlider.Value, 3);
        Assert.Equal(preset.Soft, panel.SoftSlider.Value, 3);
        Assert.Equal(preset.Depth, (double)panel.CaveDepthBox.Value!, 3);
        Assert.Equal(preset.Headroom, (double)panel.HeadroomBox.Value!, 3);
        Assert.Equal(preset.Help, panel.CaveHelp.Text);
    }

    [AvaloniaFact]
    public void ARandomisedPresetSaysItIs()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var panel = w.PathPanel;
        var preset = PathTool.CavePresets()[panel.CavePresetBox.SelectedIndex];
        panel.CavePreset(true);
        Assert.Equal(preset.Help + " " + Lang.T("cave.hint.randomized"), panel.CaveHelp.Text);
    }

    [AvaloniaFact]
    public void ShowRowsShowsOnlyTheRowsOfTheAction()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var panel = w.PathPanel;
        w.View.Path.Act = PathTool.Action.Ramp;
        panel.ShowRows();
        Assert.True(((Control)panel.StartBox.Parent!).IsVisible);
        Assert.False(((Control)panel.CaveDepthBox.Parent!).IsVisible);
    }

    [AvaloniaFact]
    public void RefreshWithNoLineSaysThereIsNone()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.PathPanel.Refresh();
        Assert.Equal(Lang.T("path.hint.noLine"), w.PathPanel.Info.Text);
    }
}
