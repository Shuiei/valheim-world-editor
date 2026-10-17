using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a biome map's start is (SeedStart): the Meadows cell nearest the middle, or the nearest land to the start
/// temple; and what lies around it (the distances to each biome, the land near it).
/// </summary>
public class SeedStartTests
{
    private static byte Meadows => (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Meadows);

    private static byte Plains => (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Plains);

    private static float CellOf(int size) => 2 * SeedPreview.Radius / size;

    private static float CentreOf(int i, float cell) => -SeedPreview.Radius + (i + 0.5f) * cell;

    [Fact]
    public void TheMiddleStartIsTheMeadowsCellNearestTheMiddle()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[4] = Meadows;
        var (cell, _) = SeedStart.MiddleCell(biome, 3, CellOf(3));
        Assert.Equal(4, cell);
    }

    [Fact]
    public void TheMiddleStartIsTheOriginWhenThereIsNoMeadows()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, Plains);
        var (cell, position) = SeedStart.MiddleCell(biome, 3, CellOf(3));
        Assert.Equal(-1, cell);
        Assert.Equal((0f, 0f), position);
    }

    [Fact]
    public void TheTempleStartIsTheNearestLandCellToTheTemple()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[2] = Plains;
        biome[8] = Plains;
        float cell = CellOf(3);
        var (found, _) = SeedStart.TempleCell(biome, 3, cell, (CentreOf(2, cell), CentreOf(0, cell)));
        Assert.Equal(2, found);
    }

    [Fact]
    public void TheTempleStartKeepsThePositionOfTheTemple()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, Plains);
        float cell = CellOf(3);
        var temple = (CentreOf(1, cell), CentreOf(1, cell));
        var (_, position) = SeedStart.TempleCell(biome, 3, cell, temple);
        Assert.Equal(temple, position);
    }

    [Fact]
    public void TheTempleStartHasNoCellWhenThereIsNoLand()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        var (found, _) = SeedStart.TempleCell(biome, 3, CellOf(3), (0f, 0f));
        Assert.Equal(-1, found);
    }

    [Fact]
    public void DistancesAreFromTheStartToTheNearestCellOfEachBiome()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[2] = Plains;
        float cell = CellOf(3);
        var (mass, _) = SeedLandmasses.Find(biome, 3);
        var start = (CentreOf(0, cell), CentreOf(0, cell));
        var (_, _, distance, _) = SeedStart.Distances(biome, 3, cell, start, mass, mass[0]);
        Assert.InRange(distance[Heightmap.Biome.Plains], 2 * cell - 1f, 2 * cell + 1f);
    }

    [Fact]
    public void ABiomeOnAnotherLandmassIsNotOnTheStartsLandmass()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, SeedPreview.Ocean);
        biome[0] = Meadows;
        biome[2] = Plains;
        float cell = CellOf(3);
        var (mass, _) = SeedLandmasses.Find(biome, 3);
        var start = (CentreOf(0, cell), CentreOf(0, cell));
        var (_, _, _, same) = SeedStart.Distances(biome, 3, cell, start, mass, mass[0]);
        Assert.False(same[Heightmap.Biome.Plains]);
    }

    [Fact]
    public void NearCountsTheCellsWithin400MetresOfTheStartAndTheLandAmongThem()
    {
        byte[] biome = new byte[9];
        Array.Fill(biome, Plains);
        biome[1] = SeedPreview.Ocean;
        float cell = 10f;
        var (mass, _) = SeedLandmasses.Find(biome, 3);
        var start = (CentreOf(0, cell), CentreOf(0, cell));
        var (near, nearLand, _, _) = SeedStart.Distances(biome, 3, cell, start, mass, mass[0]);
        Assert.Equal(9, near);
        Assert.Equal(8, nearLand);
    }
}
