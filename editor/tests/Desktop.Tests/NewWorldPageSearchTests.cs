using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

using static TerrainEditor.Desktop.Tests.NewWorldPageTests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's seed search: a search stopped part way shows the best so far and its result is looked at, a
/// search started again drops the first and its pictures, and leaving during a search is no failure.
/// </summary>
public class NewWorldPageSearchTests
{
    [AvaloniaFact]
    public async Task ASearchStoppedShowsTheBestSoFarAndAResultIsLookedAt()
    {
        using var r = new NewWorldPageRun();
        await r.Look("abc");
        r.P.Count.SelectedIndex = 0;
        Click(r.P.Find);
        Assert.False(r.P.Find.IsEnabled);
        Assert.True(r.P.Stop.IsVisible && r.P.Progress.IsVisible);
        await TestWindows.Until(() => r.P.Progress.Value > 0, 120000);
        Click(r.P.Stop);
        await TestWindows.Until(() => r.P.Find.IsEnabled, 120000);
        Assert.False(r.P.Stop.IsVisible || r.P.Progress.IsVisible);
        Assert.NotEmpty(r.P.Results);
        var buttons = r.P.Found.Children.OfType<Button>().ToList();
        Assert.Equal(r.P.Results.Count, buttons.Count);
        var (best, score) = r.P.Results[0];
        Assert.Equal(best.Seed, buttons[0].Tag);
        Assert.Contains($"score {score:0.0}", Texts(buttons[0]));
        Assert.Equal(NewWorldPage.Describe(best.Stats), ToolTip.GetTip(buttons[0]));
        // A result clicked: its seed in the box, looked at at once, its picture framed.
        Click(buttons[0]);
        Assert.Equal(best.Seed, r.P.SeedBox.Text);
        await TestWindows.Until(() => r.P.Shown?.Seed == best.Seed && !r.P.Loading.IsVisible, SeedWait);
        Assert.Equal(new Thickness(2), buttons[0].BorderThickness);
        // The others as thick, unseen: the chosen one no wider than they are.
        Assert.All(buttons.Skip(1), b => Assert.Equal((new Thickness(2), Brushes.Transparent), (b.BorderThickness, b.BorderBrush)));
    }

    // Leaving the page while a search reads the location rules: its source was disposed under it, and the search
    // failed with ObjectDisposedException on a page no longer shown.
    [AvaloniaFact]
    public async Task LeavingDuringASearchIsNoFailure()
    {
        using var r = new NewWorldPageRun();
        OnUi(() =>
        {
            r.P.StartSearch();
            r.P.Dispose();
        });
        await TestWindows.Until(() => r.P.Pending.IsCompleted, SeedWait);
        Settle();
        Assert.DoesNotContain("The search failed", Texts(r.P.Found));
    }

    [AvaloniaFact]
    public async Task ASearchStartedAgainDropsTheFirstAndItsPictures()
    {
        using var r = new NewWorldPageRun();
        await r.Look("abc");
        r.P.Count.SelectedIndex = 0;
        Click(r.P.Find);
        await TestWindows.Until(() => r.P.Progress.Value > 0, 120000);
        Click(r.P.Stop);
        await TestWindows.Until(() => r.P.Find.IsEnabled, 120000);
        Assert.NotEmpty(r.P.Found.Children.OfType<Button>());
        // The longest search, started twice (the second stops the first) and stopped at once: the pictures go.
        r.P.Count.SelectedIndex = 2;
        OnUi(() =>
        {
            r.P.StartSearch();
            r.P.StartSearch();
        });
        Assert.Empty(r.P.Found.Children.OfType<Button>());
        Click(r.P.Stop);
        await TestWindows.Until(() => r.P.Pending.IsCompleted, 120000);
        Settle();
        Assert.True(r.P.Find.IsEnabled);
        Assert.False(r.P.Stop.IsVisible);
    }
}
