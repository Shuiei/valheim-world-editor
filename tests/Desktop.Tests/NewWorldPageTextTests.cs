using Avalonia;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The texts the New world page shows for people: distances and biomes, a result's tooltip and its small preview.
/// </summary>
public class NewWorldPageTextTests
{
    [Fact]
    public void DistancesAndBiomesAreSaidForPeople()
    {
        Assert.Equal("none", NewWorldPage.Km(float.PositiveInfinity));
        Assert.Equal("400 m", NewWorldPage.Km(430));
        Assert.Equal("1.2 km", NewWorldPage.Km(1234));
        // Both ends: under 50 m said so (it read "000 m"), 950 m and more in km (it read "1000 m").
        Assert.Equal("< 100 m", NewWorldPage.Km(30));
        Assert.Equal("100 m", NewWorldPage.Km(60));
        Assert.Equal("1.0 km", NewWorldPage.Km(970));
        Assert.Equal("Black Forest", NewWorldPage.Name(Heightmap.Biome.BlackForest));
        Assert.Equal("Ashlands", NewWorldPage.Name(Heightmap.Biome.AshLands));
        Assert.Equal("Deep North", NewWorldPage.Name(Heightmap.Biome.DeepNorth));
        Assert.Equal("Swamp", NewWorldPage.Name(Heightmap.Biome.Swamp));
    }

    [Fact]
    public void AResultsTooltipDescribesItsWorld()
    {
        var shares = SeedPreview.Biomes.ToDictionary(b => b, _ => 0f);
        shares[Heightmap.Biome.Meadows] = 0.6f;
        shares[Heightmap.Biome.Swamp] = 0.4f;
        var distance = SeedPreview.Biomes.ToDictionary(b => b, _ => 800f);
        distance[Heightmap.Biome.Mountain] = float.PositiveInfinity;
        var same = SeedPreview.Biomes.ToDictionary(b => b, _ => true);
        same[Heightmap.Biome.Swamp] = false;
        var s = new SeedPreview.Stats(0.3f, shares, 0.5f, (120, -40), 0.7f, 0.9f, distance, same);
        string text = NewWorldPage.Describe(s);
        Assert.Contains("Land: 30 % of the world · the start's landmass: 90 % of the land (largest: 50 %) · land around the start: 70 %", text);
        Assert.Contains("Biomes: Meadows 60 %, Swamp 40 %", text);
        Assert.Contains("Swamp 800 m (by sea)", text);
        Assert.Contains("Mountain none", text);
        Assert.DoesNotContain("Ashlands", text.Split('\n')[2]);
        Assert.Contains("The start (red dot) is likely near 120, -40", text);
        Assert.DoesNotContain("Traders", text);
        // With the temple's start and the landmarks' distances.
        var nearest = new Dictionary<string, float> { ["Haldor"] = 2500, ["Eikthyr"] = 300 };
        text = NewWorldPage.Describe(s with { TempleStart = true, Nearest = nearest });
        Assert.Contains("where the game will put its start temple", text);
        Assert.Contains("Haldor 2.5 km, the Bog Witch none, Hildir none", text);
        Assert.Contains("Eikthyr 300 m", text);
    }

    [AvaloniaFact]
    public void APreviewIsDrawnSmallWithItsStart()
    {
        var p = SeedPreview.Make("abc", 64, cancel: TestContext.Current.CancellationToken);
        using var bmp = NewWorldPage.Draw(p, 40);
        Assert.Equal(new PixelSize(40, 40), bmp.PixelSize);
        using var plain = NewWorldPage.Draw(p, 16, startDot: false);
        Assert.Equal(new PixelSize(16, 16), plain.PixelSize);
    }
}
