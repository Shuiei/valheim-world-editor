using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where an object inside a building piece is lifted to (Stability.Standing): the top of the piece's collider over the
/// point, climbing a stack; nothing for a point above a piece, on its face, or with no piece there.
/// </summary>
public class StabilityStandingTests
{
    /// <summary>A wood wall (2 × 2 m, its middle at its pivot, 0.3 m thick) standing with its foot at height y.</summary>
    private static Stability.Piece Wall(float y, float x = 0) => new("woodwall", new Vector3(x, y + 1, 0), Quaternion.Identity);

    [Fact]
    public void APointInsideAPieceIsLiftedToItsTop()
    {
        var standing = new Stability.Standing([Wall(30)]);
        Assert.Equal(32f, standing.Top(new Vector3(0.2f, 30.5f, 0.1f)) ?? -1, 3);
    }

    [Fact]
    public void APointAboveOnAFaceOrBesideAPieceStays()
    {
        var standing = new Stability.Standing([Wall(30)]);
        Assert.Null(standing.Top(new Vector3(0, 32.5f, 0)));
        Assert.Null(standing.Top(new Vector3(0, 32f, 0)));
        Assert.Null(standing.Top(new Vector3(0, 31f, 0.3f)));
    }

    [Fact]
    public void WithoutPiecesNothingIsLifted()
    {
        Assert.Null(new Stability.Standing([]).Top(new Vector3(0, 30, 0)));
        // A prefab the support data does not know has no boxes.
        Assert.Null(new Stability.Standing([new Stability.Piece("no_such_piece_xyz", new Vector3(0, 30, 0), Quaternion.Identity)]).Top(new Vector3(0, 30, 0)));
    }

    // Lifted onto a wall whose top is inside the next wall (one standing a little into the other): the point climbs to
    // the highest piece holding it, not the first.
    [Fact]
    public void AStackIsClimbedToTheHighestPieceHoldingThePoint()
    {
        var standing = new Stability.Standing([Wall(30), Wall(31.5f)]);
        Assert.Equal(33.5f, standing.Top(new Vector3(0, 30.5f, 0)) ?? -1, 3);
    }

    // A turned piece: the top along the vertical through the point, where it leaves the turned box.
    [Fact]
    public void ATurnedPiecesTopIsTakenAlongTheVertical()
    {
        var turned = new Stability.Piece("woodwall", new Vector3(0, 31, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2));
        // Lying flat: 0.3 m high (its thickness), 2 m across.
        Assert.Equal(31.15f, new Stability.Standing([turned]).Top(new Vector3(0.5f, 31f, 0.5f)) ?? -1, 3);
        // Scaled twice: its box doubles.
        var big = new Stability.Piece("woodwall", new Vector3(0, 31, 0), Quaternion.Identity, 2f);
        Assert.Equal(33f, new Stability.Standing([big]).Top(new Vector3(0, 30f, 0)) ?? -1, 3);
    }
}
