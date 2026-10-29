using System.Numerics;
using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The point at a straight distance along a run of straight pieces, searching forwards from a segment and a fraction
/// of it; a point past the end of the run, or before the fraction the search starts at, is not found.
/// </summary>
public class PlaceToolAlongRunTests
{
    /// <summary>
    /// The point along the run at straight distance <paramref name="dist"/> from <paramref name="start"/>, through the
    /// tool's own search (boxed: the point, the segment and the fraction; null when not found).
    /// </summary>
    private static object? AlongRun(List<Vector2> run, Vector2 start, int seg, float t0, float dist) =>
        typeof(PlaceTool).GetMethod("AlongRun", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [run, start, seg, t0, dist]);

    [Fact]
    public void ADistanceWithinTheFirstPieceIsFoundOnIt()
    {
        var run = new List<Vector2> { new(0, 0), new(10, 0) };

        var (q, seg, t) = ((Vector2 Q, int Seg, float T))AlongRun(run, Vector2.Zero, 0, 0f, 4f)!;

        Assert.Equal(new Vector2(4, 0), q);
        Assert.Equal(0, seg);
        Assert.Equal(0.4f, t, 4);
    }

    [Fact]
    public void ADistancePastTheEndOfTheRunIsNotFound()
    {
        var run = new List<Vector2> { new(0, 0), new(10, 0) };

        Assert.Null(AlongRun(run, Vector2.Zero, 0, 0f, 12f));
    }

    [Fact]
    public void ADistanceOnTheNextPieceIsFoundOnThatPiece()
    {
        // The first piece runs out at 10; the second rises from (10, 0), and a straight distance of 12 from the
        // start is reached at y = sqrt(44) on it.
        var run = new List<Vector2> { new(0, 0), new(10, 0), new(10, 10) };

        var (q, seg, _) = ((Vector2 Q, int Seg, float T))AlongRun(run, Vector2.Zero, 0, 0f, 12f)!;

        Assert.Equal(1, seg);
        Assert.Equal(10f, q.X, 3);
        Assert.Equal(MathF.Sqrt(44), q.Y, 3);
    }

    [Fact]
    public void APointBeforeTheStartingFractionOfItsPieceIsNotFound()
    {
        // The distance 4 falls at fraction 0.4 of the piece, which the search starts past.
        var run = new List<Vector2> { new(0, 0), new(10, 0) };

        Assert.Null(AlongRun(run, Vector2.Zero, 0, 0.5f, 4f));
    }
}
