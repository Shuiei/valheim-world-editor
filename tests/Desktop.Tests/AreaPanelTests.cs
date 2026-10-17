using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's ground actions on a copy of the test world (zone 0, 0): each action chosen in its list and applied, the No limit switch, Paint, Restore and Erode, what it says with nothing to work on, Regrow's limits, the height average and the volume shown.
/// </summary>
[Collection("World files")]
public class AreaPanelTests
{
    [AvaloniaTheory]
    [InlineData(AreaPanel.Act.Raise, "Raise applied to")]
    [InlineData(AreaPanel.Act.Lower, "Lower applied to")]
    [InlineData(AreaPanel.Act.Smooth, "Smooth applied to")]
    [InlineData(AreaPanel.Act.Natural, "Naturalize applied to")]
    [InlineData(AreaPanel.Act.Flatten, "Flatten applied to")]
    public async Task EachGroundActionIsAppliedFromThePanel(AreaPanel.Act act, string said)
    {
        using var r = new SharedAreaPanelRun();
        float before = r.H(32, 32);
        r.P.Choose(act);
        r.P.HeightBox.Value = (decimal)(before + 3);
        r.P.AmountBox.Value = 1.5m;
        await r.P.Apply();
        Assert.StartsWith(said, r.Said);
        Assert.True(r.S.CanUndo);
        Assert.StartsWith("Area: ", r.S.UndoLabel);
        if (act == AreaPanel.Act.Raise)
        {
            Assert.Equal(before + 1.5f, r.H(32, 32), 2);
        }
        if (act == AreaPanel.Act.Lower)
        {
            Assert.Equal(before - 1.5f, r.H(32, 32), 2);
        }
        if (act == AreaPanel.Act.Flatten)
        {
            Assert.Equal(before + 3, r.H(32, 32), 2);
        }
    }

    [AvaloniaFact]
    public void NoLimitShowsUnderFlattenRaiseAndLowerAndIsTheBrushSwitch()
    {
        using var r = new SharedAreaPanelRun();
        foreach (var act in new[] { AreaPanel.Act.Flatten, AreaPanel.Act.Raise, AreaPanel.Act.Lower })
        {
            r.P.Choose(act);
            Assert.True(r.P.NoLimitBox.IsVisible);
        }
        r.P.Choose(AreaPanel.Act.Smooth);
        Assert.False(r.P.NoLimitBox.IsVisible);
        // Flatten far above the ground: the limit leaves most of it out of reach, until No limit is on.
        r.P.Choose(AreaPanel.Act.Flatten);
        r.P.HeightBox.Value = (decimal)(r.H(32, 32) + 100);
        Assert.Contains("out of reach", r.P.VolumeText.Text);
        r.P.NoLimitBox.IsChecked = true;
        Assert.True(r.W.Tools.NoLimitBox.IsChecked);
        Assert.True(r.S.Ground.NoLimit);
        Assert.StartsWith("No limit", r.Said);
        Assert.DoesNotContain("out of reach", r.P.VolumeText.Text);
        // The brush options' switch moves this one too.
        r.W.Tools.NoLimitBox.IsChecked = false;
        Assert.False(r.P.NoLimitBox.IsChecked);
        Assert.False(r.S.Ground.NoLimit);
        Assert.Contains("out of reach", r.P.VolumeText.Text);
    }

    [AvaloniaFact]
    public async Task PaintIsAppliedWithTheKindChosenInItsList()
    {
        using var r = new SharedAreaPanelRun();
        r.P.Choose(AreaPanel.Act.Paint);
        int paved = Array.FindIndex(((IEnumerable<string>)r.P.PaintBox.ItemsSource!).ToArray(), l => l.Contains("aved"));
        r.P.PaintBox.SelectedIndex = paved;
        await r.P.Apply();
        Assert.StartsWith("Paint paved applied to", r.Said);
        int p = 32 * r.S.Ground.W + 32;
        Assert.Equal(Brush.PaintOf(BrushTool.PaintPaved), r.S.Ground.Paint[(p * 4)..(p * 4 + 4)]);
    }

    [AvaloniaFact]
    public async Task RestoreAndErodeAreGroundActionsToo()
    {
        using var r = new SharedAreaPanelRun();
        r.P.Choose(AreaPanel.Act.Raise);
        await r.P.Apply();
        r.P.Choose(AreaPanel.Act.Restore);
        await r.P.Apply();
        Assert.StartsWith("Restore", r.Said);
        r.P.Choose(AreaPanel.Act.Erode);
        await r.P.Apply();
        Assert.Contains("applied to", r.Said);
    }

    [AvaloniaFact]
    public async Task WithoutASelectionOrSessionItSaysWhatToDo()
    {
        using var r = new SharedAreaPanelRun(select: false);
        r.P.Choose(AreaPanel.Act.Raise);
        await r.P.Apply();
        Assert.Equal("Select an area first.", r.Said);
        Assert.False(r.S.CanUndo);
        // No area open at all: nothing happens.
        var lone = new MainWindow(load: false) { Width = 800, Height = 600 };
        lone.Show();
        await lone.AreaPanel.Apply();
        Assert.Equal("", lone.MessageText.Text ?? "");
    }

    [AvaloniaFact]
    public async Task RemoveWithNoneOfTheChosenKindsInsideSaysSo()
    {
        using var r = new SharedAreaPanelRun();
        r.ChooseNoKinds();
        r.P.Choose(AreaPanel.Act.Remove);
        await r.P.Apply();
        Assert.Equal("No objects of the chosen kinds inside the selection.", r.Said);
    }

    [AvaloniaFact]
    public void TheAverageButtonPutsTheMeanHeightInTheBox()
    {
        using var r = new SharedAreaPanelRun();
        Click(r.P.AverageButton);
        float avg = r.W.View.Area.Average(r.S.Ground)!.Value;
        Assert.Equal((decimal)MathF.Round(avg, 1), r.P.HeightBox.Value);
    }

    [AvaloniaFact]
    public async Task RegrowNeedsTheGeneratorAndASmallEnoughArea()
    {
        // An area with no world generator behind it.
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseMode(ToolMode.Area);
        w.View.Area.Points.Add(new Vector2(2, 2));
        w.View.Area.Points.Add(new Vector2(60, 60));
        w.AreaPanel.Choose(AreaPanel.Act.Regrow);
        await w.AreaPanel.Apply();
        Assert.Equal("Regrow needs the world's generator (not available here).", w.MessageText.Text);
        // More than 16 zones.
        using var r = new SharedAreaPanelRun(select: false, size: 5);
        r.Select(1, 1, 319, 319);
        r.P.Choose(AreaPanel.Act.Regrow);
        await r.P.Apply();
        Assert.StartsWith("Choose a smaller area", r.Said);
    }

    [AvaloniaFact]
    public void TheVolumeIsWorkedOutOnlyForTheActionsShowingIt()
    {
        using var r = new SharedAreaPanelRun();
        r.P.Choose(AreaPanel.Act.Remove);
        Assert.Equal("", r.P.VolumeText.Text);
        foreach (var act in (AreaPanel.Act[])[AreaPanel.Act.Flatten, AreaPanel.Act.Raise, AreaPanel.Act.Lower])
        {
            r.P.Choose(act);
            Assert.Contains("Ground inside", r.P.VolumeText.Text, StringComparison.Ordinal);
            r.P.Choose(AreaPanel.Act.Smooth);
            Assert.Equal("", r.P.VolumeText.Text);
        }
    }
}
