using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using Xunit;

using static TerrainEditor.Desktop.Tests.NewWorldPageTests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's typed seeds: a typed seed looked at on Generate and a bad one said so, a seed typed back to the
/// one shown needs no Generate, a seed whose look was dropped can be generated again, and the wishes are the boxes.
/// </summary>
public class NewWorldPageSeedTests
{
    [AvaloniaFact]
    public async Task ATypedSeedIsLookedAtOnGenerateAndABadOneIsSaid()
    {
        using var r = new NewWorldPageRun();
        await TestWindows.Until(() => r.P.Shown != null && !r.P.Loading.IsVisible, SeedWait);
        // The seed shown: nothing to generate.
        Assert.False(r.P.Generate.IsEnabled);
        // Typed: not looked at yet, Generate lit.
        OnUi(() => r.P.SeedBox.Text = "abc");
        Assert.True(r.P.Generate.IsEnabled);
        Assert.False(r.P.Loading.IsVisible);
        Assert.NotEqual("abc", r.P.Shown?.Seed);
        // Generate: looked at, the button greyed again.
        Click(r.P.Generate);
        Assert.True(r.P.Loading.IsVisible);
        Assert.Equal("Looking at abc's world…", r.P.LoadingText.Text);
        Assert.False(r.P.Generate.IsEnabled);
        await TestWindows.Until(() => r.P.Shown?.Seed == "abc" && !r.P.Loading.IsVisible, SeedWait);
        // Enter in the box does the same.
        OnUi(() => r.P.SeedBox.Text = "abcd");
        OnUi(() => r.P.SeedBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }));
        Assert.False(r.P.Generate.IsEnabled);
        await TestWindows.Until(() => r.P.Shown?.Seed == "abcd" && !r.P.Loading.IsVisible, SeedWait);
        // Roll: another random seed of ten characters, looked at at once.
        Click(r.P.Roll);
        Assert.NotEqual("abcd", r.P.SeedBox.Text);
        Assert.Equal(10, r.P.SeedBox.Text!.Length);
        Settle();
        Assert.False(r.P.Generate.IsEnabled);
        await TestWindows.Until(() => r.P.Shown?.Seed == r.P.SeedBox.Text && !r.P.Loading.IsVisible, SeedWait);
        // No seed, or one too long: said, nothing shown.
        foreach (string bad in new[] { "", "12345678901" })
        {
            r.P.ShowSeed(bad);
            Assert.Null(r.P.Shown);
            Assert.False(r.P.Loading.IsVisible);
            Assert.Empty(r.P.Marks.Children);
            // The last seed's dungeon search stopped with it.
            Assert.False(r.P.FindingDungeons);
            Assert.Equal("The seed is 1 to 10 characters, as in the game.", Texts(r.P.Info));
        }
    }

    [AvaloniaFact]
    public void TheWishesAreTheBoxes()
    {
        using var r = new NewWorldPageRun();
        r.P.BigLand.IsChecked = false;
        r.P.LandAround.IsChecked = true;
        r.P.OnFoot.IsChecked = true;
        r.P.Swamp.Value = 1000;
        r.P.Mountain.Value = 0;
        r.P.Plains.Value = 3000;
        r.P.Haldor.Value = 2500;
        r.P.BogWitch.Value = 4000;
        r.P.Hildir.Value = 5000;
        r.P.Bosses.Value = 6000;
        Assert.Equal(new SeedPreview.Wishes(BigStartLand: false, LandAroundStart: true, MaxSwampDistance: 1000, MaxMountainDistance: 0, MaxPlainsDistance: 3000,
            BiomesOnStartLand: true, MaxHaldorDistance: 2500, MaxBogWitchDistance: 4000, MaxHildirDistance: 5000, MaxBossDistance: 6000), r.P.Wishes());
    }

    [AvaloniaFact]
    public async Task ASeedTypedBackToTheOneShownNeedsNoGenerate()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        OnUi(() => r.P.SeedBox.Text = Seed);
        Assert.False(r.P.Generate.IsEnabled);
        var shown = r.P.Shown;
        // Another seed typed: Generate lit, nothing looked at; back to the one shown: greyed again.
        OnUi(() => r.P.SeedBox.Text = "abc");
        Assert.True(r.P.Generate.IsEnabled);
        OnUi(() => r.P.SeedBox.Text = Seed);
        Assert.False(r.P.Generate.IsEnabled);
        Assert.False(r.P.Loading.IsVisible);
        Assert.Same(shown, r.P.Shown);
    }

    [AvaloniaFact]
    public async Task ASeedWhoseLookWasDroppedCanBeGeneratedAgain()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        // Another seed asked for, then its look dropped before it was shown (as when leaving the page).
        OnUi(() =>
        {
            r.P.SeedBox.Text = "abc";
            r.P.ShowSeed("abc");
            r.P.StopAll();
        });
        await TestWindows.Until(() => r.P.Pending.IsCompleted, SeedWait);
        Settle();
        // Its world is not the one on the map: Generate lit, and it is looked at.
        Assert.Equal(Seed, r.P.Shown?.Seed);
        Assert.True(r.P.Generate.IsEnabled);
        Click(r.P.Generate);
        await TestWindows.Until(() => r.P.Shown?.Seed == "abc" && !r.P.Loading.IsVisible, SeedWait);
        Assert.False(r.P.Generate.IsEnabled);
    }
}
