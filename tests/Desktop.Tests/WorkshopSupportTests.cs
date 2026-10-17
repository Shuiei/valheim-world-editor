using Avalonia.Headless.XUnit;
using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The support check's colours are the game's, and the cut hides the building above a height.
/// </summary>
public class WorkshopSupportTests
{
    // The support check's colours are the game's (WearNTear.Highlight): light blue on the ground, red to
    // green as support grows, red when it breaks; and the cut hides the building above a height.
    [AvaloniaFact]
    public async Task SupportColoursAreTheGamesAndTheCutHidesAboveIt()
    {
        Assert.Equal(new Vector3(0.6f, 0.8f, 1f), GlView.SupportTint(-1));
        var red = GlView.SupportTint(0);
        Assert.True(red.X > 1 && red.Y < 0.01f && red.Z < 0.01f, red.ToString());
        Assert.Equal(red, GlView.SupportTint(-2));
        var green = GlView.SupportTint(1);
        Assert.True(green.Y > green.X && green.Y > green.Z && green.X > 0.4f, green.ToString());
        var middle = GlView.SupportTint(0.5f);
        Assert.True(middle.X > 0.5f && middle.Y > 0.5f && middle.Z < middle.X, middle.ToString());
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        await w.OpenWorkshop(null);
        Assert.True(w.CutBox.IsVisible);
        Assert.Null(w.View.CutY);
        w.CutSlider.Value = 6;
        Assert.Equal(Workshop.Ground + 6, w.View.CutY);
        Assert.Equal(Workshop.Ground + 6, w.PlaceTool.CutY);
        Assert.Equal("Cut at 6 m", w.CutText.Text);
        w.CutSlider.Value = 0;
        Assert.Equal(Workshop.Ground, w.PlaceTool.CutY);
        w.CutSlider.Value = MainWindow.CutOff;
        Assert.Null(w.PlaceTool.CutY);
        Assert.Equal("Cut: off", w.CutText.Text);
        w.CutSlider.Value = 3;
        await w.LeaveWorkshop();
        Assert.False(w.CutBox.IsVisible);
        Assert.Null(w.View.CutY);
    }
}
