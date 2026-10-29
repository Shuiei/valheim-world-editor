using ValheimGen;
using Xunit;
using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Where the game lays out a seed's locations (SeedLocations, with the game's rules from
/// editor/tests/fixtures/locations.json), against a world the game made: seed 5DCcdIcuYJ's start temple, Eikthyr's altars, the
/// Bog Witch's ten spots, Fader's arenas and the spot that became Haldor's camp, as Valheim 0.221 saved them.
/// </summary>
public class SeedLocationsPlacementTests
{
    [Fact]
    public void DungeonsAreFoundByKind()
    {
        Fixtures.UseLocations();
        // The tests' copy holds the dungeons laid out first (sunken crypts, infested mines, Hildir's,
        // the charred fortresses): the same spots as when every kind before them is laid out.
        var dungeons = TerrainEditor.App.SeedPreview.FindDungeons(Seed(), parallel: true, cancel: TestContext.Current.CancellationToken);
        var placed = SeedLocations.Place(Seed(), SeedLocations.Which.Prioritized, parallel: true, cancel: TestContext.Current.CancellationToken);
        var crypts = placed.Where(p => p.Rule.Prefab == "SunkenCrypt4").Select(p => (p.X, p.Z)).ToHashSet();
        Assert.NotEmpty(crypts);
        Assert.Equal(crypts, [.. dungeons.Where(d => d.Name == "Sunken crypts").Select(d => (d.X, d.Z))]);
        Assert.All(dungeons, d => Assert.Equal("dungeon", d.Kind));
        Assert.Contains(dungeons, d => d.Name == "Infested mines");
        Assert.DoesNotContain(dungeons, d => d.Name == "Haldor");
    }

    [Fact]
    public void TheStartIsWhereTheGamePutsItsTemple()
    {
        Fixtures.UseLocations();
        var start = Assert.Single(SeedLocations.Place(Seed(), SeedLocations.Which.Start, cancel: TestContext.Current.CancellationToken));
        Assert.Equal("StartTemple", start.Rule.Prefab);
        Assert.Equal(0.444078f, start.X, 0.01f);
        Assert.Equal(-6.853543f, start.Z, 0.01f);
    }

    [Fact]
    public void BossesAndTradersAreWhereTheGameLaysThemOut()
    {
        Fixtures.UseLocations();
        var placed = SeedLocations.Place(Seed(), SeedLocations.Which.Prioritized, parallel: true, cancel: TestContext.Current.CancellationToken);
        Has(placed, "StartTemple", 0.444078f, -6.853543f);
        Has(placed, "Eikthyrnir", 332.4125f, 64.03294f);
        Has(placed, "Eikthyrnir", 110.40247f, 205.81421f);
        Has(placed, "Eikthyrnir", 64.40399f, 332.57587f);
        Has(placed, "GDKing", -191.07407f, 2172.5815f);
        Has(placed, "Vendor_BlackForest", -253.49864f, 2352.9058f);
        Has(placed, "FaderLocation", 2816f, -9024f);
        Has(placed, "FaderLocation", -4864f, -7936f);
        Has(placed, "FaderLocation", 640f, -9152f);
        foreach (var (x, z) in new[] { (-3718.105f, -1156.0703f), (-3326.012f, 1471.598f), (4546.0337f, -3206.04f), (3460.6895f, 3705.4165f), (4672.6562f, 1978.0132f), (-711.52435f, -5496.555f), (-1343.1382f, 4990.305f), (-3260.2358f, -3843.965f), (-5123.88f, 517.98596f), (-3322.991f, 4220.4844f) })
        {
            Has(placed, "BogWitch_Camp", x, z);
        }
        Assert.Equal(10, placed.Count(p => p.Rule.Prefab == "BogWitch_Camp"));
        Assert.Equal(10, placed.Count(p => p.Rule.Prefab == "Vendor_BlackForest"));
        // Only the kinds laid out first, none an alt biome decides.
        Assert.All(placed, p => Assert.True(p.Rule.Prioritized != 0, p.Rule.Prefab));
        // Unity's Random is put back as it was.
        ValheimGen.UnityEngine.Random.InitState(42); // nosemgrep
        float before = ValheimGen.UnityEngine.Random.Value;
        ValheimGen.UnityEngine.Random.InitState(42);
        SeedLocations.Place(Seed(), SeedLocations.Which.Start, cancel: TestContext.Current.CancellationToken);
        Assert.Equal(before, ValheimGen.UnityEngine.Random.Value);
    }

    [Fact]
    public void EveryKindOrTheKindsAskedForAreLaidOut()
    {
        Fixtures.UseLocations();
        var cancel = TestContext.Current.CancellationToken;
        var all = new SeedLocations.Layout(Seed()).Place(SeedLocations.Which.All, cancel);
        Assert.True(all.Count >= SeedLocations.Place(Seed(), SeedLocations.Which.Prioritized, cancel: cancel).Count);
        Assert.Contains(SeedLocations.PlaceKinds(Seed(), ["StartTemple"], cancel: cancel), p => p.Rule.Prefab == "StartTemple");
    }

    [Fact]
    public void AStoppedLayoutKeepsNothingOfItAndStartsAgain()
    {
        Fixtures.UseLocations();
        var layout = new SeedLocations.Layout(Seed());
        using var stopped = new CancellationTokenSource();
        stopped.Cancel();
        Assert.Throws<OperationCanceledException>(() => layout.Place(SeedLocations.Which.Prioritized, stopped.Token));
        var start = Assert.Single(layout.Place(SeedLocations.Which.Start, TestContext.Current.CancellationToken));
        Assert.Equal("StartTemple", start.Rule.Prefab);
    }
}
