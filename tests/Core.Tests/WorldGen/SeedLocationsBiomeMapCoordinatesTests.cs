using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biome map's coordinates: a map cell in world metres (12 m cells, the map's cell 1024 at six metres), and the
/// cell that holds a world position, cut toward zero as the game casts it.
/// </summary>
public class SeedLocationsBiomeMapCoordinatesTests
{
    [Fact]
    public void MapCellOneThousandTwentyFourIsAtSixMetres()
    {
        Assert.Equal(6f, SeedLocations.BiomeMap.MapToWorld(1024f));
    }

    [Fact]
    public void TheNextMapCellIsTwelveMetresOn()
    {
        Assert.Equal(18f, SeedLocations.BiomeMap.MapToWorld(1025f));
    }

    [Fact]
    public void APositionAtTheStartOfACellIsInThatCell()
    {
        Assert.Equal(1024, SeedLocations.BiomeMap.WorldToMap(6f));
    }

    [Fact]
    public void APositionAlmostTwelveMetresIntoACellIsStillInThatCell()
    {
        Assert.Equal(1024, SeedLocations.BiomeMap.WorldToMap(17.9f));
    }

    [Fact]
    public void TheCellAfterStartsTwelveMetresOn()
    {
        Assert.Equal(1025, SeedLocations.BiomeMap.WorldToMap(18f));
    }

    [Fact]
    public void APositionJustBelowACellStartIsInTheCellBefore()
    {
        Assert.Equal(1023, SeedLocations.BiomeMap.WorldToMap(5.9f));
    }

    [Fact]
    public void ACellsMapToWorldPositionConvertsBackToThatCell()
    {
        Assert.Equal(1000, SeedLocations.BiomeMap.WorldToMap(SeedLocations.BiomeMap.MapToWorld(1000f)));
    }
}
