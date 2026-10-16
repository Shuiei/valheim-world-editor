using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Measure tool: a tape between two points of the ground.
/// </summary>
public class MeasureToolTests
{
    [Fact]
    public void TheTapeMeasuresDistanceRiseAndSlope()
    {
        var m = new MeasureTool();
        m.Down(new Vector3(0, 30, 0));
        m.Move(new Vector3(30, 40, 40));
        Assert.False(m.Fixed);
        m.Down(new Vector3(30, 40, 40));
        Assert.True(m.Fixed);
        // A hill 50 m high halfway.
        var r = m.Measure((x, z) => x == 15 ? 80 : 30, water: 30)!;
        Assert.Equal(50, r.Distance, 3);
        Assert.Equal(MathF.Sqrt(2600), r.AlongSlope, 3);
        Assert.Equal(10, r.Rise, 3);
        Assert.Equal(20, r.SlopePercent!.Value, 3);
        Assert.Equal(MathF.Atan(0.2f) * 180 / MathF.PI, r.SlopeDegrees, 3);
        Assert.Equal(80, r.Highest, 3);
        Assert.Null(r.WaterDepth);
        // A third click starts again.
        m.Down(new Vector3(5, 20, 5));
        Assert.Null(m.B);
        Assert.Equal(new Vector3(5, 20, 5), m.A);
    }

    [Fact]
    public void TheClickedHeightsCountForLowestAndHighest()
    {
        var m = new MeasureTool();
        m.Down(new Vector3(0, 40, 0));
        m.Down(new Vector3(10, 20, 0));
        var r = m.Measure((_, _) => 30, water: 0)!;
        Assert.Equal(20, r.Lowest, 3);
        Assert.Equal(40, r.Highest, 3);
    }

    [Fact]
    public void UnderTheSeaTheDepthIsShown()
    {
        var m = new MeasureTool();
        m.Down(new Vector3(0, 25, 0));
        m.Down(new Vector3(0, 26, 0));
        var (_, rows) = m.Describe((_, _) => 25, 30);
        Assert.Contains(("Water depth", "5.0 m"), rows);
        Assert.Contains(("Slope", "– · 90.0°"), rows);
    }

    [AvaloniaFact]
    public void ClickingTwicePutsTheValuesInThePanel()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.KeyPress(Key.M, RawInputModifiers.None, PhysicalKey.M, "m");
        Assert.Equal(ToolMode.Measure, w.View.Mode);
        Assert.True(w.MeasurePanel.Card.IsVisible);
        w.View.SetCamera(new Vector3(0, 100, 1), Vector3.Zero, 1);
        var size = w.View.Bounds.Size;
        Avalonia.Point Screen(float vx, float vz)
        {
            var q = Vector4.Transform(new Vector4(vx, 30, vz, 1), w.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
        }
        w.MouseDown(Screen(-10, 0), MouseButton.Left);
        w.MouseUp(Screen(-10, 0), MouseButton.Left);
        w.MouseMove(Screen(10, 0));
        w.MouseDown(Screen(10, 0), MouseButton.Left);
        w.MouseUp(Screen(10, 0), MouseButton.Left);
        Assert.True(w.View.Tape.Fixed);
        Assert.Equal("20.0 m", w.MeasurePanel.Shown["Distance"]);
        Assert.Equal("30.0 → 30.0 m (+0.0)", w.MeasurePanel.Shown["Height A → B"]);
        // An edit under the tape: the panel follows.
        s.Shape(64, 64, Formula.Compile("d < 3 ? 6 : 0", ["d"]), 3, 0, "step");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("30.0 / 36.0 m", w.MeasurePanel.Shown["Lowest / highest"]);
        // Shown again (the tape changes at the mouse's pace): the same rows, their text set again.
        // The window puts the card's content in a scroll viewer.
        var content = ((Border)w.MeasurePanel.Card).Child;
        var rows = (Grid)((StackPanel)(content is ScrollViewer sv ? sv.Content! : content!)).Children[2];
        var made = rows.Children.ToList();
        w.MeasurePanel.Refresh();
        Assert.Equal(made, rows.Children);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(w.View.Tape.A);
        Assert.Empty(w.MeasurePanel.Shown);
        // Still measuring: Esc only cleared the tape.
        Assert.Equal(ToolMode.Measure, w.View.Mode);
    }
}
