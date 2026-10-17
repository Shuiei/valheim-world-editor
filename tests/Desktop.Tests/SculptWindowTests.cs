using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.StrokeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Sculpting in the window: the number keys pick the brushes, Escape goes back to view, the sliders set the brush, and
/// undo, redo and save work from the window.
/// </summary>
[Collection("World files")]
public class SculptWindowTests
{
    [AvaloniaFact]
    public void NumberKeysPickTheBrushesAndEscapeGoesBackToView()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.KeyPress(Key.D3, RawInputModifiers.None, PhysicalKey.Digit3, "3");
        Assert.Equal(BrushTool.Flatten, w.Tools.Tool);
        Assert.Equal(BrushTool.Flatten, w.View.Tool);
        w.KeyPress(Key.D0, RawInputModifiers.None, PhysicalKey.Digit0, "0");
        Assert.Equal(BrushTool.Natural, w.Tools.Tool);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(w.Tools.Tool);
        Assert.Null(w.View.Tool);
    }

    [AvaloniaFact]
    public void TheSlidersSetTheBrush()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.Tools.SizeSlider.Value = 12.5;
        w.Tools.StrengthSlider.Value = 0.8;
        w.Tools.ShapeBox.SelectedIndex = 1;
        w.Tools.FalloffBox.SelectedIndex = 5;
        Assert.Equal(12.5f, w.Tools.Brush.Radius);
        Assert.Equal(0.8f, w.Tools.Brush.Strength, 4);
        Assert.Equal(BrushShape.Square, w.Tools.Brush.Shape);
        Assert.Equal(Falloff.Sharp, w.Tools.Brush.Falloff);
    }

    [AvaloniaFact]
    public async Task UndoRedoAndSaveFromTheWindow()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        var s = TestScenes.Flat();
        w.Edit(s);
        Assert.Equal(s.Brush, w.Tools.Brush);
        Assert.False(w.SaveButton.IsEnabled);
        Stroke(s, BrushTool.Raise, 32, 32);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(w.SaveButton.IsEnabled);
        Assert.True(w.UndoButton.IsEnabled);
        Assert.StartsWith("Unsaved: 1 zone", w.PendingText.Text);
        w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
        Assert.Equal(30, At(s, 32, 32), 4);
        Assert.Equal("All saved", w.PendingText.Text);
        Assert.True(w.RedoButton.IsEnabled);
        w.KeyPress(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.Z, "Z");
        Assert.True(At(s, 32, 32) > 30);
        // Saving asks first; saying no writes nothing.
        string? asked = null;
        w.ConfirmSave = q => { asked = q; return Task.FromResult(false); };
        w.Tell = _ => Task.CompletedTask;
        await w.Save();
        // Worded as the map's save asks it, not cut out of the status line's text.
        Assert.Contains("Write 1 zone(s) of ground into the world files?", asked);
        Assert.True(s.Pending.Zones > 0);
    }
}
