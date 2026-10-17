using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A segment of the dense line: where it starts, how far it runs, and its length squared.
/// </summary>
public class PathToolSegmentTests
{
    [Fact]
    public void ASegmentRunsFromItsStartToTheNextPoint()
    {
        var curve = new List<(Vector2 P, int Seg)> { (new Vector2(0, 0), 0), (new Vector2(3, 4), 1) };
        var (a, d, l2) = PathTool.Segment(curve, 1);
        Assert.Equal(new Vector2(0, 0), a);
        Assert.Equal(new Vector2(3, 4), d);
        Assert.Equal(25f, l2, 3);
    }

    [Fact]
    public void APointSegmentStillHasALengthSquaredToProjectOnto()
    {
        var curve = new List<(Vector2 P, int Seg)> { (new Vector2(2, 2), 0), (new Vector2(2, 2), 1) };
        var (_, _, l2) = PathTool.Segment(curve, 1);
        Assert.Equal(1e-9f, l2, 12);
    }
}
