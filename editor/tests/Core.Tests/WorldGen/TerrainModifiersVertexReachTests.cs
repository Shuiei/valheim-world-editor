using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The vertices within a reach of a point: the square around it, row by row, its own vertex alone at no reach.
/// </summary>
public class TerrainModifiersVertexReachTests
{
    [Fact]
    public void AReachOfOneVisitsTheNineVerticesAroundThePoint()
    {
        var visited = new List<(int I, int J)>();

        TerrainModifiers.ForEachVertexInReach(5, 7, 1, (i, j) => visited.Add((i, j)));

        Assert.Equal(9, visited.Count);
        Assert.Equal((6, 4), visited[0]);
        Assert.Equal((8, 6), visited[8]);
    }

    [Fact]
    public void ANoReachVisitsOnlyThePointsOwnVertex()
    {
        var visited = new List<(int I, int J)>();

        TerrainModifiers.ForEachVertexInReach(5, 7, 0, (i, j) => visited.Add((i, j)));

        Assert.Equal([(7, 5)], visited);
    }
}
