using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biome map's grid: a map cell in world metres (12 m cells, the map's cell 1024 at six metres), the cell that
/// holds a world position, cut toward zero as the game casts it, and the grid Expand World Size makes for a radius
/// and a stretch.
/// </summary>
public class BiomeGridTests
{
    [Fact]
    public void MapCellOneThousandTwentyFourIsAtSixMetres()
    {
        Assert.Equal(6f, BiomeGrid.Game.MapToWorld(1024f));
    }

    [Fact]
    public void TheNextMapCellIsTwelveMetresOn()
    {
        Assert.Equal(18f, BiomeGrid.Game.MapToWorld(1025f));
    }

    [Fact]
    public void APositionAtTheStartOfACellIsInThatCell()
    {
        Assert.Equal(1024, BiomeGrid.Game.WorldToMap(6f));
    }

    [Fact]
    public void APositionAlmostTwelveMetresIntoACellIsStillInThatCell()
    {
        Assert.Equal(1024, BiomeGrid.Game.WorldToMap(17.9f));
    }

    [Fact]
    public void TheCellAfterStartsTwelveMetresOn()
    {
        Assert.Equal(1025, BiomeGrid.Game.WorldToMap(18f));
    }

    [Fact]
    public void APositionJustBelowACellStartIsInTheCellBefore()
    {
        Assert.Equal(1023, BiomeGrid.Game.WorldToMap(5.9f));
    }

    [Fact]
    public void ACellsMapToWorldPositionConvertsBackToThatCell()
    {
        Assert.Equal(1000, BiomeGrid.Game.WorldToMap(BiomeGrid.Game.MapToWorld(1000f)));
    }

    [Fact]
    public void TheGamesRadiusWithoutAStretchIsTheGamesGrid() =>
        Assert.Equal(BiomeGrid.Game, BiomeGrid.Of(new WorldRules()));

    [Fact]
    public void ABiggerWorldHasMorePointsAtTheSameDensity()
    {
        // 2048 x 16500 / 10500 = 3218.3: rounded up.
        var grid = BiomeGrid.Of(new WorldRules { Radius = 16000f });
        Assert.Equal(new BiomeGrid(3219, 1609.5f, 12f), grid);
        Assert.Equal(6f, grid.Half);
    }

    [Fact]
    public void AStretchedWorldHasFewerWiderPoints()
    {
        var grid = BiomeGrid.Of(new WorldRules { Radius = 16000f, Stretch = 1.3f });
        Assert.Equal(2476, grid.Size);
        Assert.Equal(1238f, grid.Middle);
        Assert.Equal(12f * 1.3f, grid.Pixel);
        Assert.Equal(grid.Middle, grid.WorldToMap(grid.Half));
        Assert.Equal(grid.Half, grid.MapToWorld(grid.Middle));
    }

    [Fact]
    public void AMapTooBigToHoldIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => BiomeGrid.Of(new WorldRules { Radius = 1e7f }));
        Assert.Throws<InvalidDataException>(() => BiomeGrid.Of(new WorldRules { Radius = 16000f, Stretch = 1e-4f }));
        Assert.Throws<InvalidDataException>(() => BiomeGrid.Of(new WorldRules { Radius = 16000f, Stretch = float.MaxValue }));
    }
}
