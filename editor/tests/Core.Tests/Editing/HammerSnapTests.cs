using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The snap search: a piece with no neighbour that has snap points stays where it is, and only neighbours with snap data
/// take part in the closest pair.
/// </summary>
public class HammerSnapTests
{
    [Fact]
    public void APieceWithNoNeighboursStaysWhereItIs()
    {
        var pos = new Vector3(3, 0, 3);

        var (position, snapped) = Hammer.Snap("woodwall", Quaternion.Identity, pos, []);

        Assert.Equal(pos, position);
        Assert.Null(snapped);
    }

    [Fact]
    public void ANeighbourWithoutSnapDataIsNotSnappedTo()
    {
        var data = Hammer.Get("woodwall")!;
        var pieces = new List<Hammer.Placed> { new(1, "no_such_piece_for_snap_test", Vector3.Zero, Quaternion.Identity) };

        Assert.Null(Hammer.NearestSnapMove(data, Quaternion.Identity, new Vector3(1, 0, 1), pieces));
    }
}
