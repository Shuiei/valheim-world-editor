using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedKindChooser;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's fit: the widest chosen kind at its largest size, the hint that says when kinds overlap, the
/// slider, and the Cut and fill action.
/// </summary>
[Collection("World files")]
public class PlaceFitTests
{
    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task CutAndFillFollowsTheGroundAndTheAction()
    {
        using var r = new SharedPlaceWindow.PanelRun(ToolMode.Area);
        var a = r.W.View.Area;
        a.Clear();
        a.Points.Add(new Vector2(12, 12));
        a.Points.Add(new Vector2(22, 22));
        a.Notify();
        var p = r.W.AreaPanel;
        p.Choose(AreaPanel.Act.Flatten);
        Assert.EndsWith("Ground inside: 0 m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
        Assert.StartsWith("Flatten to ", p.VolumeText.Text);
        // Another action: its own first line at once.
        p.Choose(AreaPanel.Act.Raise);
        p.AmountBox.Value = 2;
        Assert.StartsWith("Raise 2 m: fill ", p.VolumeText.Text);
        p.Choose(AreaPanel.Act.Lower);
        Assert.StartsWith("Lower 2 m: dig ", p.VolumeText.Text);
        // Raised: the ground inside counts it, after the change itself (not only when the panel changes).
        p.Choose(AreaPanel.Act.Raise);
        await p.Apply();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("Ground inside: 0 m³ raised", p.VolumeText.Text);
        Assert.Contains(" m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
        // Undone: back to nothing raised.
        r.S.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.EndsWith("Ground inside: 0 m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
    }

    /// <summary>
    /// A Place tool with these kinds chosen, each with a model box this wide in metres, at 100 % size.
    /// </summary>
    private static PlaceTool FitTool(params (string Name, float Width)[] kinds)
    {
        var t = new PlaceTool { ModelBox = n => kinds.FirstOrDefault(k => k.Name == n) is { Name: not null } k ? (new Vector3(-k.Width / 2, 0, -1), new Vector3(k.Width / 2, 3, 1)) : null };
        t.Chosen.Clear();
        t.Chosen.AddRange(kinds.Select(k => k.Name));
        t.SizeMin = 100;
        t.SizeMax = 100;
        return t;
    }

    [Fact]
    public void TheWidestChosenKindAtItsLargestSizeIsFound()
    {
        var t = FitTool(("Small", 2), ("Big", 6));
        Assert.Equal((6f, "Big"), t.Widest());
        // The largest size counts (Size max 150 %).
        t.SizeMax = 150;
        Assert.Equal(9, t.Widest().Width, 3);
        // The depth counts when it is the larger side.
        var deep = new PlaceTool { ModelBox = _ => (new Vector3(-1, 0, -4), new Vector3(1, 1, 4)) };
        deep.Chosen.Clear();
        deep.Chosen.Add("Deep");
        deep.SizeMax = 100;
        Assert.Equal(8, deep.Widest().Width, 3);
        // No model known: nothing.
        var none = new PlaceTool();
        Assert.Equal((0f, null), none.Widest());
        Assert.Null(none.FitHint());
        Assert.False(none.Fit());
    }

    [Fact]
    public void TheHintSaysWhenTheWidestKindOverlapsAndFitSpacesThem()
    {
        var t = FitTool(("Big", 6.3f));
        t.Mode = PlaceTool.Modes.Brush;
        t.Spacing = 4;
        Assert.Equal("Big is about 6.3 m wide: 4 m apart they overlap.", t.FitHint());
        Assert.True(t.Fit());
        Assert.Equal(6.5f, t.Spacing);
        Assert.Null(t.FitHint());
        // A quarter metre of slack: no hint.
        t.Spacing = 6.1f;
        Assert.Null(t.FitHint());
        // Grid: the cell; Line: the distance along it; End to end: none (the pieces' length).
        t.Mode = PlaceTool.Modes.Grid;
        t.Cell = 2;
        Assert.Equal(2, t.SpacingNow);
        Assert.Contains("2 m apart", t.FitHint());
        t.Fit();
        Assert.Equal(6.5f, t.Cell);
        t.Mode = PlaceTool.Modes.Line;
        t.Every = 1.5f;
        Assert.Contains("1.5 m apart", t.FitHint());
        t.Fit();
        Assert.Equal(6.5f, t.Every);
        t.EndToEnd = true;
        Assert.Null(t.SpacingNow);
        Assert.Null(t.FitHint());
        Assert.False(t.Fit());
        t.Mode = PlaceTool.Modes.Zone;
        Assert.Equal(t.Spacing, t.SpacingNow);
    }

    [Fact]
    public void FitStopsAtTheSettingsLargestValue()
    {
        var t = FitTool(("Huge", 50));
        t.Mode = PlaceTool.Modes.Brush;
        t.Fit();
        Assert.Equal(PlaceTool.MaxSpacing, t.Spacing);
        t.Mode = PlaceTool.Modes.Grid;
        t.Fit();
        Assert.Equal(PlaceTool.MaxCell, t.Cell);
        t.Mode = PlaceTool.Modes.Line;
        t.Fit();
        Assert.Equal(PlaceTool.MaxEvery, t.Every);
    }

    [AvaloniaFact]
    public void ThePanelShowsTheHintAndFitMovesTheSlider()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        r.T.ModelBox = n => n == "Beech1" ? (new Vector3(-4, 0, -4), new Vector3(4, 10, 4)) : null;
        r.T.SizeMax = 100;
        r.T.Mode = PlaceTool.Modes.Brush;
        Choose(r, "Beech1");
        r.P.SpacingSlider.Value = 3;
        r.T.Notify();
        r.W.PlaceInput.Refresh();
        Assert.True(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
        Assert.Equal("Beech1 is about 8.0 m wide: 3 m apart they overlap.", r.P.FitText.Text);
        Click(r.P.FitButton);
        Assert.Equal(8, r.P.SpacingSlider.Value);
        Assert.Equal(8, r.T.Spacing);
        Assert.False(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
        // Grid: the cell slider.
        r.T.Mode = PlaceTool.Modes.Grid;
        r.P.CellSlider.Value = 2;
        r.W.PlaceInput.Refresh();
        Click(r.P.FitButton);
        Assert.Equal(8, r.P.CellSlider.Value);
        // A kind narrower than the spacing: no hint.
        r.T.ModelBox = _ => (new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 1, 0.5f));
        r.W.PlaceInput.Refresh();
        Assert.False(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
    }
}
