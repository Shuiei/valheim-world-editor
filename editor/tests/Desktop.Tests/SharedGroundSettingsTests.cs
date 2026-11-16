using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brush settings more than one tool uses (Naturalize's Bumps, Bump size and pattern, Erode's Rest angle): each
/// panel shows them with the action that uses them, and every copy sets and shows the one brush's value.
/// </summary>
public sealed class SharedGroundSettingsTests
{
    /// <summary>A window editing a flat 2 × 2 zone area.</summary>
    private static MainWindow Open() => TestWindows.Editing(TestScenes.Flat(2));

    [AvaloniaFact]
    public void TwoCopiesOfASettingSetTheSameValueAndShowIt()
    {
        var b = new Brush();
        TextBlock shownOne = new(), shownTwo = new();
        Slider one = BrushRows.Bumps(b, shownOne), two = BrushRows.Bumps(b, shownTwo);

        one.Value = 2.5;
        Assert.Equal(2.5f, b.NoiseAmp, 3);
        Assert.Equal(2.5, two.Value, 3);
        Assert.Equal("2.5 m", shownTwo.Text);

        two.Value = 0.8;
        Assert.Equal(0.8f, b.NoiseAmp, 3);
        Assert.Equal(0.8, one.Value, 3);
        Assert.Equal(shownTwo.Text, shownOne.Text);
    }

    [AvaloniaFact]
    public void TheRestAngleAndTheBumpSizeFollowTheBrushToo()
    {
        var b = new Brush();
        Slider rest = BrushRows.RestAngle(b, new TextBlock()), size = BrushRows.BumpSize(b, new TextBlock());
        Assert.Equal(33, rest.Value, 3);
        Assert.Equal(16, size.Value, 3);

        // Set on the brush by something else (another panel's copy): told, the sliders show it.
        b.RestAngle = 45;
        b.NoiseSize = 30;
        b.NotifySettings();
        Assert.Equal(45, rest.Value, 3);
        Assert.Equal(30, size.Value, 3);
    }

    [AvaloniaFact]
    public void TheAreaPanelShowsNaturalizesBumpsAndErodesRestAngleWithTheirActions()
    {
        var w = Open();
        w.Tools.ChooseMode(ToolMode.Area);
        var p = w.AreaPanel;

        p.Choose(AreaPanel.Act.Smooth);
        Assert.False(p.NaturalRows.IsVisible);
        Assert.False(p.RestRow.IsVisible);
        p.Choose(AreaPanel.Act.Natural);
        Assert.True(p.NaturalRows.IsVisible);
        Assert.False(p.RestRow.IsVisible);
        p.Choose(AreaPanel.Act.Erode);
        Assert.False(p.NaturalRows.IsVisible);
        Assert.True(p.RestRow.IsVisible);

        // The same value as the Erode brush's and the Path tool's: set in one panel, shown in the others.
        p.RestSlider.Value = 48;
        Assert.Equal(48f, w.Tools.Brush.RestAngle, 3);
        Assert.Equal(48, w.Tools.RestSlider.Value, 3);
        Assert.Equal(48, w.PathPanel.RestSlider.Value, 3);
        w.Tools.RestSlider.Value = 20;
        Assert.Equal(20, p.RestSlider.Value, 3);
    }

    [AvaloniaFact]
    public void TheAreaPanelsNewPatternChangesTheBrushsPatternAndSaysSo()
    {
        var w = Open();
        w.Tools.ChooseMode(ToolMode.Area);
        var p = w.AreaPanel;
        p.Choose(AreaPanel.Act.Natural);
        var before = w.Tools.Brush.Noise;

        p.NewPatternButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotSame(before, w.Tools.Brush.Noise);
        Assert.StartsWith("New natural pattern.", w.LastMessageEn);
    }

    [AvaloniaFact]
    public void ThePathPanelShowsTheBumpsForNaturalizeAndTheRestAngleForErode()
    {
        var w = Open();
        w.Tools.ChooseMode(ToolMode.Path);
        var p = w.PathPanel;
        var actions = Enum.GetValues<PathTool.Action>();

        Assert.False(p.NaturalRows.IsVisible);
        Assert.False(p.RestRow.IsVisible);
        p.ActionBox.SelectedIndex = Array.IndexOf(actions, PathTool.Action.Naturalize);
        Assert.Equal(PathTool.Action.Naturalize, w.View.Path.Act);
        // Naturalize uses the bumps whatever the Natural look says.
        Assert.True(p.NaturalRows.IsVisible);
        Assert.False(p.RestRow.IsVisible);
        p.ActionBox.SelectedIndex = Array.IndexOf(actions, PathTool.Action.Erode);
        Assert.False(p.NaturalRows.IsVisible);
        Assert.True(p.RestRow.IsVisible);
        p.ActionBox.SelectedIndex = Array.IndexOf(actions, PathTool.Action.Flatten);
        Assert.False(p.RestRow.IsVisible);

        var before = w.Tools.Brush.Noise;
        p.NewPatternButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotSame(before, w.Tools.Brush.Noise);
        Assert.StartsWith("New natural pattern.", w.LastMessageEn);
    }

    [AvaloniaFact]
    public void ThePathToolRestoresTheGroundAlongItsLineAsOneStep()
    {
        var w = Open();
        var s = w.Session!;
        int p = 64 * s.Ground.W + 64;
        s.Shape(64, 64, Formula.Compile("3", []), 6, 0, "pad");
        Assert.Equal(33f, s.Ground.HeightOf(p), 2);

        w.Tools.ChooseMode(ToolMode.Path);
        var path = w.View.Path;
        path.Act = PathTool.Action.Restore;
        path.Width = 20;
        path.Points.Add(new Vector2(40, 64));
        path.Points.Add(new Vector2(88, 64));
        w.ApplyPath();

        Assert.Equal(30f, s.Ground.HeightOf(p), 2);
        Assert.Equal("Path: Restore", s.UndoLabel);
        Assert.StartsWith("Applied “Restore” along ", w.LastMessageEn);
    }
}
