using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The four edges of a quad drawn as line pairs (the Dungeon tool's openings and joints), in order round the quad,
/// each corner taken through the view.
/// </summary>
public class GlViewQuadEdgesTests
{
    [Fact]
    public void AQuadsFourEdgesJoinItsCornersInOrder()
    {
        var into = new List<float>();
        GlView.QuadEdges(into, [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], p => p);
        float[] expected = [0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0];
        Assert.Equal(expected, into);
    }

    [Fact]
    public void EveryCornerGoesThroughTheViewFirst()
    {
        var into = new List<float>();
        GlView.QuadEdges(into, [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], p => p + new Vector3(10, 0, 0));
        Assert.Equal(24, into.Count);
        Assert.Equal([10f, 0, 0, 11, 0, 0], into.Take(6));
    }
}
