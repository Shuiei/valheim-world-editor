using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The landmasses of a biome map (SeedLandmasses.Find): land cells joined side by side, numbered and counted.
/// </summary>
public class SeedLandmassesTests
{
    private const byte Land = 0;

    [Fact]
    public void SeparateIslandsGetTheirOwnNumberAndCount()
    {
        // Row 0: land land sea; row 1: sea sea sea; row 2: land sea land.
        byte[] biome = [Land, Land, SeedPreview.Ocean, SeedPreview.Ocean, SeedPreview.Ocean, SeedPreview.Ocean, Land, SeedPreview.Ocean, Land];
        var (mass, sizes) = SeedLandmasses.Find(biome, 3);
        Assert.Equal([0, 0, -1, -1, -1, -1, 1, -1, 2], mass);
        Assert.Equal([2, 1, 1], sizes);
    }

    [Fact]
    public void CellsTouchingOnlyAtACornerAreSeparateLandmasses()
    {
        byte[] biome = [Land, SeedPreview.Ocean, SeedPreview.Ocean, SeedPreview.Ocean, Land, SeedPreview.Ocean, SeedPreview.Ocean, SeedPreview.Ocean, SeedPreview.Ocean];
        var (_, sizes) = SeedLandmasses.Find(biome, 3);
        Assert.Equal([1, 1], sizes);
    }

    [Fact]
    public void ASeaOnlyMapHasNoLandmasses()
    {
        byte[] biome = new byte[4];
        Array.Fill(biome, SeedPreview.Ocean);
        var (mass, sizes) = SeedLandmasses.Find(biome, 2);
        Assert.Empty(sizes);
        Assert.All(mass, m => Assert.Equal(-1, m));
    }
}
