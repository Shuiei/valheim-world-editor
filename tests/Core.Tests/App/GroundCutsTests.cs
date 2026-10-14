using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Ground cuts marked on a grid (GroundCuts): the points inside an outline (and on it) marked, those outside not, the
/// even-odd rule for an outline crossing itself, an outline past the grid's edge clipped, too few points marking nothing.
/// </summary>
public class GroundCutsTests
{
    [Fact]
    public void ThePointsInsideAnOutlineAreMarked()
    {
        float[] cut = new float[10 * 10];
        // A square from (2, 2) to (5, 5), either way round.
        GroundCuts.Mark(cut, 10, 10, [new(2, 2), new(5, 2), new(5, 5), new(2, 5)]);
        for (int gz = 0; gz < 10; gz++)
        {
            for (int gx = 0; gx < 10; gx++)
            {
                Assert.Equal(gx is >= 2 and <= 5 && gz is >= 2 and <= 5 ? 1 : 0, cut[gz * 10 + gx]);
            }
        }
        // A triangle: its middle in, its far corners out.
        Assert.True(GroundCuts.Inside([new(0, 0), new(4, 0), new(0, 4)], 1, 1));
        Assert.False(GroundCuts.Inside([new(0, 0), new(4, 0), new(0, 4)], 3, 3));
    }

    [Fact]
    public void ABowTieCutsItsTwoLobesAndTheGridsEdgeClips()
    {
        float[] cut = new float[8 * 8];
        // Crossing itself at (4, 4): the lobes are in, their meeting point on the outline too.
        GroundCuts.Mark(cut, 8, 8, [new(2, 2), new(6, 6), new(6, 2), new(2, 6)]);
        Assert.Equal(1, cut[3 * 8 + 2]);
        Assert.Equal(1, cut[3 * 8 + 5]);
        Assert.Equal(0, cut[2 * 8 + 4]);
        Assert.Equal(1, cut[4 * 8 + 4]);
        // Past the edge: only what is on the grid.
        float[] edge = new float[4 * 4];
        GroundCuts.Mark(edge, 4, 4, [new(-5, -5), new(1.5f, -5), new(1.5f, 1.5f), new(-5, 1.5f)]);
        Assert.Equal([1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], edge);
        float[] none = new float[4 * 4];
        GroundCuts.Mark(none, 4, 4, [new(0, 0), new(3, 3)]);
        Assert.All(none, v => Assert.Equal(0, v));
    }
}
