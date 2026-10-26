using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Shape panel's layout: the formula box named by its label, for screen readers and the driver.
/// </summary>
public class ShapePanelLayoutTests
{
    [AvaloniaFact]
    public async Task TheFormulaBoxIsNamedAndSetByItsLabel()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.Tools.ChooseMode(ToolMode.Shape);
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
        Assert.Equal(Lang.T("shape.label.formula"), AutomationProperties.GetName(w.ShapePanel.FormulaBox));
        await Driver.Run(w, "set " + Lang.T("shape.label.formula") + "|d * 2");
        Assert.Equal("d * 2", w.ShapePanel.FormulaBox.Text);
    }
}
