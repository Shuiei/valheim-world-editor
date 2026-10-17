using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which pieces a piece's grown boxes touch (Stability.Touches): a piece's grown box touches the other piece's collider it
/// overlaps and no other, a piece that does not support is never touched, and a piece with no data is not a neighbour.
/// </summary>
public class StabilityTouchesTests
{
    /// <summary>A unit box at a centre, axis aligned.</summary>
    private static Stability.Obb Unit(Vector3 centre) =>
        new(centre, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.One);

    /// <summary>A piece with no boxes that supports, or not.</summary>
    private static Stability.Data Piece(bool supports) =>
        new(0, supports, Vector3.Zero, []);

    /// <summary>The pieces each piece touches, as the solver's touch test finds them.</summary>
    private static List<(int J, Stability.Obb Collider)>[] Touches(Stability.Data?[] data, Stability.Obb[][] grown, Stability.Obb[][] seen) =>
        (List<(int J, Stability.Obb Collider)>[])PrivateCall.Static(typeof(Stability), "Touches", data, grown, seen)!;

    [Fact]
    public void AGrownBoxTouchesTheOverlappingColliderOfAnotherPiece()
    {
        var overlapping = Unit(Vector3.Zero);
        var touches = Touches(
            [Piece(true), Piece(true)],
            [[Unit(Vector3.Zero)], []],
            [[], [overlapping]]);

        var (j, collider) = Assert.Single(touches[0]);
        Assert.Equal(1, j);
        Assert.Equal(overlapping, collider);
    }

    [Fact]
    public void AGrownBoxDoesNotTouchAColliderFarAway()
    {
        var touches = Touches(
            [Piece(true), Piece(true)],
            [[Unit(Vector3.Zero)], []],
            [[], [Unit(new Vector3(100f, 0f, 0f))]]);

        Assert.Empty(touches[0]);
    }

    [Fact]
    public void APieceThatDoesNotSupportIsNeverTouched()
    {
        var touches = Touches(
            [Piece(true), Piece(false)],
            [[Unit(Vector3.Zero)], []],
            [[], [Unit(Vector3.Zero)]]);

        Assert.Empty(touches[0]);
    }

    [Fact]
    public void APieceWithNoDataIsNotANeighbour()
    {
        var touches = Touches(
            [Piece(true), null],
            [[Unit(Vector3.Zero)], []],
            [[], [Unit(Vector3.Zero)]]);

        Assert.Empty(touches[0]);
    }

    [Fact]
    public void APieceWithNoGrownBoxTouchesNothing()
    {
        var touches = Touches(
            [Piece(true), Piece(true)],
            [[], []],
            [[Unit(Vector3.Zero)], [Unit(Vector3.Zero)]]);

        Assert.Empty(touches[0]);
        Assert.Empty(touches[1]);
    }
}
