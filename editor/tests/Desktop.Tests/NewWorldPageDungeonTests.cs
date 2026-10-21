using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

using static TerrainEditor.Desktop.Tests.NewWorldPageTests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The dungeons the New world page finds when their kind is switched on: found, listed and dotted on the map, waited
/// for when they were switched on beforehand or while the search runs, and seen seeds kept.
/// </summary>
public class NewWorldPageDungeonTests
{
    [AvaloniaFact]
    public async Task ADungeonKindSwitchedOnIsFoundListedAndDotted()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        // Looked for at once, out of sight; the list says so until they are found.
        await TestWindows.Until(() => r.P.DungeonMarks != null, 120000);
        Assert.Empty(r.P.Map.Dots);
        // Kinds the fixtures' rules have (they have no burial chambers, troll caves or frost caves).
        var sunken = r.P.ShowDungeons["Sunken crypts"];
        sunken.IsChecked = true;
        int crypts = r.P.DungeonMarks!.Count(m => m.Name == "Sunken crypts");
        Assert.True(crypts > 0);
        Assert.Equal(crypts, r.P.Map.Dots.Count);
        string list = Texts(r.P.Info);
        Assert.Contains("Dungeons (the nearest)", list);
        Assert.Contains($"{crypts} in the world", list);
        sunken.IsChecked = false;
        Assert.Empty(r.P.Map.Dots);
        // A dungeon's row switches its kind on and shows the nearest.
        ClickRow(r, r.P.Info, "Infested mines");
        Assert.True(r.P.ShowDungeons["Infested mines"].IsChecked);
        Assert.Equal(4, r.P.Map.MetersPerPixel);
        Assert.NotEmpty(r.P.Map.Dots);
        Assert.All(r.P.Map.Dots, d => Assert.Equal(((ISolidColorBrush)d.Fill).Color, Color.FromRgb(104, 120, 255)));
    }

    [AvaloniaFact]
    public async Task ADungeonKindOnBeforehandWaitsForTheNextSeedsAndSeenSeedsAreKept()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        await TestWindows.Until(() => r.P.DungeonMarks != null, 120000);
        var first = r.P.DungeonMarks;
        r.P.ShowDungeons["Frost caves"].IsChecked = true;
        // Another seed: the loading sign says the dungeons are being found, until they are. Seen when the sign says so
        // (the dungeons can be found before the next look at the page).
        (bool NoMarks, bool Sign, string Info)? waiting = null;
        r.P.LoadingText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty && r.P.LoadingText.Text == "Finding abc's dungeons…")
            {
                waiting = (r.P.DungeonMarks == null, r.P.Loading.IsVisible, Texts(r.P.Info));
            }
        };
        OnUi(() => r.P.ShowSeed("abc"));
        await TestWindows.Until(() => r.P.DungeonMarks != null && r.P.Shown?.Seed == "abc" && !r.P.Loading.IsVisible, 120000);
        Assert.NotNull(waiting);
        Assert.True(waiting.Value.NoMarks);
        Assert.True(waiting.Value.Sign);
        Assert.Contains("Finding the dungeons (a few seconds)…", waiting.Value.Info);
        Assert.Equal(r.P.DungeonMarks!.Count(m => m.Name == "Frost caves"), r.P.Map.Dots.Count);
        // Back to the first seed: its dungeons are known at once.
        OnUi(() => r.P.ShowSeed(Seed));
        await TestWindows.Until(() => r.P.Shown?.Seed == Seed, SeedWait);
        Assert.Same(first, r.P.DungeonMarks);
    }

    [AvaloniaFact]
    public async Task ADungeonKindSwitchedOnWhileTheyAreBeingFoundWaitsForThem()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        var sunken = r.P.ShowDungeons["Sunken crypts"];
        // Switched on while the seed's dungeons are still being looked for: the loading sign until they are found.
        bool early = r.P.DungeonMarks == null;
        sunken.IsChecked = true;
        if (early)
        {
            Assert.True(r.P.Loading.IsVisible);
            Assert.Equal($"Finding {Seed}'s dungeons…", r.P.LoadingText.Text);
        }
        await TestWindows.Until(() => r.P.DungeonMarks != null && !r.P.Loading.IsVisible, 120000);
        Assert.Equal(r.P.DungeonMarks!.Count(m => m.Name == "Sunken crypts"), r.P.Map.Dots.Count);
    }
}
