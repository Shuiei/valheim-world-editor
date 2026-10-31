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

    // Snapping by hand: only the chosen snap point of the piece meets another, and it reaches further than the game's
    // half metre.
    [Fact]
    public void ByHandOnlyTheChosenSnapPointMeetsAndReachesFurther()
    {
        var wall = Hammer.Get("woodwall")!;
        var pieces = new List<Hammer.Placed> { new(7, "woodwall", Vector3.Zero, Quaternion.Identity) };
        var theirs = wall.Snaps.ToHashSet();
        // (Not its first: meeting the standing wall's first, it would stand right where that wall stands.)
        for (int k = 1; k < wall.Snaps.Length; k++)
        {
            // The piece's snap point k a metre from the standing wall's first: too far for the game's way.
            var pos = wall.Snaps[0] - wall.Snaps[k] + new Vector3(0, 0, 1);

            var (position, snapped) = Hammer.Snap("woodwall", Quaternion.Identity, pos, pieces, own: k);

            Assert.Equal(7, snapped);
            var mine = position + wall.Snaps[k];
            Assert.Contains(theirs, p => Vector3.Distance(p, mine) < 1e-4f);
        }
        Assert.Null(Hammer.NearestSnapMove(wall, Quaternion.Identity, new Vector3(0, 0, 1.6f) + wall.Snaps[0] - wall.Snaps[1], pieces, 1));
    }

    [Fact]
    public void SnapPointsAreCountedAndFoundWrapped()
    {
        int n = Hammer.SnapCount("woodwall");
        Assert.True(n > 1);
        Assert.Equal(0, Hammer.SnapCount("no_such_piece_for_snap_test"));
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var at = new Vector3(5, 1, 5);
        Assert.Equal(Hammer.SnapPointAt("woodwall", turn, at, n - 1), Hammer.SnapPointAt("woodwall", turn, at, -1));
        Assert.Equal(Hammer.SnapPointAt("woodwall", turn, at, 0), Hammer.SnapPointAt("woodwall", turn, at, n));
        Assert.Equal(at + Vector3.Transform(Hammer.Get("woodwall")!.Snaps[0], turn), Hammer.SnapPointAt("woodwall", turn, at, 0));
        Assert.Null(Hammer.SnapPointAt("no_such_piece_for_snap_test", turn, at, 0));
    }
}
