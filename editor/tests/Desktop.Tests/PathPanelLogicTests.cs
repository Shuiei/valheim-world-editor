using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path panel's logic: the rows follow the action, and the refresh
/// with no line says there is none.
/// </summary>
public class PathPanelLogicTests
{
    [AvaloniaFact]
    public void ShowRowsShowsOnlyTheRowsOfTheAction()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var panel = w.PathPanel;
        w.View.Path.Act = PathTool.Action.Ramp;
        panel.ShowRows();
        Assert.True(((Control)panel.StartBox.Parent!).IsVisible);
        Assert.False(((Control)panel.DepthBox.Parent!).IsVisible);
    }

    [AvaloniaFact]
    public void RefreshWithNoLineSaysThereIsNone()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.PathPanel.Refresh();
        Assert.Equal(Lang.T("path.hint.noLine"), w.PathPanel.Info.Text);
    }
}
