using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The building bar on its own: the parts it shows for Build, Select and Paste, its steps (Shift always finer), and its
/// settings kept between runs.
/// </summary>
public class BuildingBarTests
{
    /// <summary>The bar's snapping button of a mode.</summary>
    private static Button ModeButton(BuildingBar bar, PlaceTool.SnapModes mode) => bar.SnapModes.Children.OfType<Button>().Single(b => (PlaceTool.SnapModes)b.Tag! == mode);

    [AvaloniaFact]
    public void EachToolShowsThePartsItUses()
    {
        var bar = new BuildingBar();
        Assert.False(bar.Card.IsVisible);
        Assert.Equal(BuildingBar.Uses.None, bar.Use);

        // Build: every part, Built by in a world.
        bar.Show(BuildingBar.Uses.Build, builder: true);
        Assert.True(bar.Card.IsVisible);
        Assert.True(((Control)bar.SnapModes.Parent!).IsVisible);
        Assert.True(ModeButton(bar, PlaceTool.SnapModes.Manual).IsVisible);
        Assert.True(((Control)bar.LiftReset.Parent!).IsVisible);
        Assert.True(((Control)bar.BuilderBox.Parent!).IsVisible);

        // Select: snapping on or off (no snap point by hand), no standing lift.
        bar.ShowSnapPoint("Point 1 of 4 · Q / E");
        bar.Show(BuildingBar.Uses.Select, builder: true);
        Assert.True(((Control)bar.SnapModes.Parent!).IsVisible);
        Assert.False(ModeButton(bar, PlaceTool.SnapModes.Manual).IsVisible);
        Assert.True(ModeButton(bar, PlaceTool.SnapModes.Free).IsVisible);
        Assert.False(bar.SnapPointText.IsVisible);
        Assert.False(((Control)bar.LiftReset.Parent!).IsVisible);
        Assert.True(bar.LiftBox.IsVisible);

        // Paste: no snapping, its height shown; on the Workshop's plot no builder.
        bar.Show(BuildingBar.Uses.Paste, builder: false);
        Assert.False(((Control)bar.SnapModes.Parent!).IsVisible);
        Assert.True(((Control)bar.LiftReset.Parent!).IsVisible);
        Assert.False(((Control)bar.BuilderBox.Parent!).IsVisible);
        bar.ShowLift(1.5f);
        Assert.Equal("+1.5 m", bar.LiftText.Text);
        Assert.True(bar.LiftReset.IsEnabled);
        bar.ShowLift(0);
        Assert.Equal("none", bar.LiftText.Text);
        Assert.False(bar.LiftReset.IsEnabled);

        bar.Show(BuildingBar.Uses.None, builder: true);
        Assert.False(bar.Card.IsVisible);
    }

    [AvaloniaFact]
    public void ShiftIsAlwaysFiner()
    {
        var bar = new BuildingBar();
        // As the game's hammer by default: 22.5° a turn, half a metre a lift.
        Assert.Equal((22.5f, 0.5f, 0f), (bar.TurnStep, bar.LiftStep, bar.GridStep));
        Assert.True(bar.Snaps);
        Assert.Equal(22.5f, bar.TurnBy(shift: false));
        Assert.Equal(1f, bar.TurnBy(shift: true));
        Assert.Equal(0.5f, bar.LiftBy(shift: false));
        Assert.Equal(0.1f, bar.LiftBy(shift: true), 3);
        // The finest turn step is not made coarser by Shift.
        bar.TurnBox.SelectedIndex = 0;
        Assert.Equal(1f, bar.TurnBy(shift: false));
        Assert.Equal(1f, bar.TurnBy(shift: true));
        bar.LiftBox.SelectedIndex = 3;
        Assert.Equal(1f, bar.LiftBy(shift: false));
        Assert.Equal(0.2f, bar.LiftBy(shift: true), 3);
        bar.LiftBox.SelectedIndex = 0;
        Assert.Equal(0.02f, bar.LiftBy(shift: true), 3);
    }

    [AvaloniaFact]
    public void TheSettingsAreKeptAndEveryChangeIsTold()
    {
        var prefs = Prefs.InMemory();
        var bar = new BuildingBar();
        bar.Remember(prefs);
        int changes = 0, resets = 0;
        bar.Changed += () => changes++;
        bar.LiftResetAsked += () => resets++;

        ModeButton(bar, PlaceTool.SnapModes.Free).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bar.GridBox.SelectedIndex = 3;
        bar.TurnBox.SelectedIndex = 4;
        bar.LiftBox.SelectedIndex = 1;
        Assert.Equal(4, changes);
        Assert.False(bar.Snaps);
        Assert.Contains("on", ModeButton(bar, PlaceTool.SnapModes.Free).Classes);
        Assert.DoesNotContain("on", ModeButton(bar, PlaceTool.SnapModes.Auto).Classes);
        bar.LiftReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, resets);

        var again = new BuildingBar();
        again.Remember(prefs);
        Assert.Equal(PlaceTool.SnapModes.Free, again.SnapMode);
        Assert.Equal((2f, 90f, 0.25f), (again.GridStep, again.TurnStep, again.LiftStep));
        // A step no longer offered: the list as it is.
        prefs.Set("build.turn", 33.0);
        var other = new BuildingBar();
        other.Remember(prefs);
        Assert.Equal(22.5f, other.TurnStep);
    }

    [AvaloniaFact]
    public void TheBarNeverGetsNarrowerThanItsWidestPart()
    {
        var bar = new BuildingBar();
        var parts = (Panel)bar.Card.Child!;
        bar.Fit(900);
        Assert.Equal(900 - bar.Card.Padding.Left - bar.Card.Padding.Right, parts.MaxWidth);
        bar.Fit(40);
        Assert.Equal(BuildingBar.MinWidth - bar.Card.Padding.Left - bar.Card.Padding.Right, parts.MaxWidth);
    }
}
