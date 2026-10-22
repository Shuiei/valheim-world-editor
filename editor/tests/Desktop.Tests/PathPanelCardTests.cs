using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path panel's card laid out: what each row shows fits.
/// </summary>
public class PathPanelCardTests
{
    // The cave preset's Randomize button is shown whole beside the list (it was cut to "Rand" in the value column).
    [AvaloniaFact]
    public void TheCavePresetsRandomizeButtonIsShownWhole()
    {
        double natural = LaidOut.NaturalWidth(new Button { Content = Lang.T("common.randomize"), FontSize = 12 });
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.Tools.ChooseMode(ToolMode.Path);
        w.View.Path.Act = PathTool.Action.Cave;
        w.PathPanel.ShowRows();
        w.UpdateLayout();
        var button = w.PathPanel.CaveRandomButton;
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.Bounds.Width >= natural, $"{button.Bounds.Width} < {natural}");
    }
}
