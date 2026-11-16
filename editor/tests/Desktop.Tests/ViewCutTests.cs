using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The view's one cut (the status bar's slider) in a world: off at first, counted from the ground in the middle of the
/// view, which it follows, under the camera when walking; and the levels it counts from keep their own cut.
/// </summary>
[Collection("World files")]
public class ViewCutTests
{
    [AvaloniaFact]
    public void AWorldsCutIsOffAtFirstAndCountsFromTheGroundInTheMiddleOfTheView()
    {
        var (w, s) = TestWindows.EditingFlat();
        try
        {
            var cut = w.ViewCut;
            Assert.Equal(ViewCut.Level.Ground, cut.From);
            Assert.False(cut.On);
            Assert.Equal("Cut: off", cut.Text.Text);
            Assert.Null(w.View.CutY);
            Assert.Null(w.PlaceTool.CutY);
            // 5 m above the flat ground (30 m) the view turns on.
            w.View.Orbit(32, 32, 0, 55, 40);
            cut.Slider.Value = 5;
            Assert.True(cut.On);
            Assert.Equal("Cut at 5 m", cut.Text.Text);
            Assert.Equal(35f, w.View.CutY);
            Assert.Equal(35f, w.PlaceTool.CutY);
            // The view moved onto higher ground (the east side, raised to 42 m): the cut follows, 5 m above it.
            var scene = s.Scene;
            for (int z = 0; z < scene.H; z++)
            {
                for (int x = 90; x < scene.W; x++)
                {
                    scene.Heights[z * scene.W + x] = 42;
                }
            }
            Assert.Equal(35f, w.View.CutY);
            w.View.Orbit(-32 + 100, 32, 0, 55, 40);
            Assert.Equal(47f, w.View.CutY);
            // Walking: counted from the ground under the camera.
            w.View.CycleEye();
            Assert.Equal(GlView.EyeMode.Walk, w.View.Eye);
            Assert.Equal(47f, w.View.CutY);
            // The left end is the ground itself; the right end is off.
            cut.Slider.Value = 0;
            Assert.Equal(42f, w.View.CutY);
            cut.Off();
            Assert.Null(w.View.CutY);
            Assert.Null(w.View.CutAbove);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [AvaloniaFact]
    public void TheWorldAndADungeonEachKeepTheirCutAndTheWorkshopStartsWithNone()
    {
        var (w, _) = TestWindows.EditingFlat();
        try
        {
            var cut = w.ViewCut;
            w.View.Orbit(32, 32, 0, 55, 40);
            cut.Set(7);
            Assert.Equal(37f, w.View.CutY);
            // A dungeon's floor at 5030 m: its own cut (off at first), within its own range.
            cut.FromDungeon(5030);
            Assert.Equal(ViewCut.Level.Dungeon, cut.From);
            Assert.Equal(ViewCut.DungeonEnd, cut.Slider.Maximum);
            Assert.False(cut.On);
            Assert.Null(w.View.CutY);
            cut.Set(4);
            Assert.Equal(5034f, w.View.CutY);
            Assert.Equal(5034f, w.PlaceTool.CutY);
            // Another dungeon's floor: the same metres above it.
            cut.FromDungeon(5010);
            Assert.Equal(5014f, w.View.CutY);
            // Back in the world: its cut as it was, following the view again; then the dungeon's as it was.
            cut.FromGround();
            Assert.Equal(ViewCut.End, cut.Slider.Maximum);
            Assert.Equal(7, cut.Slider.Value);
            Assert.Equal(37f, w.View.CutY);
            cut.FromDungeon(5010);
            Assert.Equal(4, cut.Slider.Value);
            Assert.Equal(5014f, w.View.CutY);
            // The Workshop's plot: off at first, whatever it was last time; out of it, the world's cut is off too.
            cut.FromPlot(Workshop.Ground);
            Assert.Equal(ViewCut.Level.Plot, cut.From);
            Assert.False(cut.On);
            cut.Set(3);
            Assert.Equal(Workshop.Ground + 3, w.View.CutY);
            cut.FromPlot(Workshop.Ground);
            Assert.False(cut.On);
            cut.FromGround(forget: true);
            Assert.Equal(ViewCut.Level.Ground, cut.From);
            Assert.False(cut.On);
            Assert.Null(w.View.CutY);
            // Forgotten from the world itself too.
            cut.Set(9);
            cut.FromGround(forget: true);
            Assert.False(cut.On);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [AvaloniaFact]
    public void NoLimitShowsOnTheMaskCardForTheToolsItGoverns()
    {
        var (w, s) = TestWindows.EditingFlat();
        try
        {
            // The sculpt brushes, Landform, Path and Region keep to the game's ±8 m unless No limit is on.
            w.Tools.Choose(BrushTool.Raise);
            Assert.True(w.MaskPanel.Card.IsVisible);
            Assert.True(w.MaskPanel.NoLimitShown);
            foreach (var mode in new[] { ToolMode.Landform, ToolMode.Path, ToolMode.Area })
            {
                w.Tools.ChooseMode(mode);
                Assert.True(w.MaskPanel.Card.IsVisible);
                Assert.True(w.MaskPanel.NoLimitShown);
            }
            // The Mask holds for these too, but not No limit: paint and Place move no ground.
            w.Tools.Choose(BrushTool.PaintPaved);
            Assert.True(w.MaskPanel.Card.IsVisible);
            Assert.False(w.MaskPanel.NoLimitShown);
            w.Tools.ChooseMode(ToolMode.Place);
            Assert.True(w.MaskPanel.Card.IsVisible);
            Assert.False(w.MaskPanel.NoLimitShown);
            // Switched with one tool, it holds for the others: the session's ground has it.
            w.Tools.ChooseMode(ToolMode.Landform);
            w.MaskPanel.NoLimitBox.IsChecked = true;
            Assert.True(s.Ground.NoLimit);
            w.Tools.ChooseMode(ToolMode.Path);
            Assert.True(w.MaskPanel.NoLimitBox.IsChecked);
            Assert.True(s.Ground.NoLimit);
            w.MaskPanel.NoLimitBox.IsChecked = false;
            Assert.False(s.Ground.NoLimit);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [Fact]
    public void OnlyTheToolsThatKeepToTheLimitAreGovernedByNoLimit()
    {
        Assert.True(MainWindow.GovernedByNoLimit(ToolMode.Brush, BrushTool.Raise));
        Assert.True(MainWindow.GovernedByNoLimit(ToolMode.Brush, BrushTool.Flatten));
        Assert.True(MainWindow.GovernedByNoLimit(ToolMode.Landform, null));
        Assert.True(MainWindow.GovernedByNoLimit(ToolMode.Path, null));
        Assert.True(MainWindow.GovernedByNoLimit(ToolMode.Area, null));
        Assert.False(MainWindow.GovernedByNoLimit(ToolMode.Brush, BrushTool.PaintDirt));
        Assert.False(MainWindow.GovernedByNoLimit(ToolMode.Brush, null));
        Assert.False(MainWindow.GovernedByNoLimit(ToolMode.Place, null));
        Assert.False(MainWindow.GovernedByNoLimit(ToolMode.View, null));
        Assert.False(MainWindow.GovernedByNoLimit(ToolMode.Select, null));
    }
}
