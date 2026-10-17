using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Erode brush and the drop speed: a drop slows going uphill and speeds up going downhill, and one Erode stroke, and its key and area action.
/// </summary>
public class ErosionBrushTests
{
    [Fact]
    public void ADropSlowsGoingUphillAndSpeedsUpGoingDownhill()
    {
        Assert.True(Erosion.DropSpeed(2, 0.5f) < 2);
        Assert.True(Erosion.DropSpeed(2, -0.5f) > 2);
        Assert.Equal(0, Erosion.DropSpeed(1, 10));
    }

    [Fact]
    public void TheErodeBrushIsOneStroke()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("d < 4 ? 6 : 0", ["d"]), 4, 0, "tower");
        float top = s.Scene.Heights[64 * s.Scene.W + 64];
        s.Brush.Radius = 10;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Erode, 64, 64);
        for (int i = 0; i < 30; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        Assert.True(s.Scene.Heights[64 * s.Scene.W + 64] < top - 0.5f);
        Assert.True(s.Scene.Heights[64 * s.Scene.W + 69] > 30.1f);
        Assert.Equal("Erode", s.UndoLabel);
    }

    [AvaloniaFact]
    public async Task TheKeyAndTheAreaAction()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("d < 4 ? 6 : 0", ["d"]), 4, 0, "tower");
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.KeyPress(Key.O, RawInputModifiers.None, PhysicalKey.O, "o");
        Assert.Equal(BrushTool.Erode, w.Tools.Tool);
        w.Tools.WaterButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.True(w.Tools.Brush.ErodeWater);
        Assert.False(w.Tools.RestSlider.IsEffectivelyVisible);
        w.Tools.ChooseMode(ToolMode.Area);
        w.View.Area.Points.Add(new Vector2(50, 50));
        w.View.Area.Points.Add(new Vector2(78, 78));
        w.AreaPanel.Choose(AreaPanel.Act.Erode);
        await w.AreaPanel.Apply();
        Assert.True(s.Scene.Heights[64 * s.Scene.W + 64] < 36);
        Assert.Equal("Area: Erode", s.UndoLabel);
    }
}
