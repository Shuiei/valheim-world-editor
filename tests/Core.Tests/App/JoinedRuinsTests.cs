using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A ruin's pieces joined to a player's building: the ones touching a built piece, then the ones touching those, up
/// and down as well; a ruin standing apart, and every ruin when nothing is built, stay apart.
/// </summary>
public class JoinedRuinsTests
{
    [Fact]
    public void ARuinBuiltOntoIsJoinedPieceByPiece()
    {
        Vector3[] built = [new(0, 30, 0)];
        Vector3[] ruins =
        [
            // Touching the built piece, then a chain on from it: east, then up.
            new(4, 30, 0), new(8, 30, 0), new(8, 34, 0),
            // Just out of reach of all of them; a ruin far off.
            new(8, 30, 4.6f), new(200, 30, 200),
        ];
        Assert.Equal([true, true, true, false, false], JoinedRuins.Find(built, ruins));
        Assert.True(Vector3.Distance(ruins[1], ruins[4]) > JoinedRuins.Reach);
    }

    [Fact]
    public void NothingBuiltJoinsNothing()
    {
        Assert.Equal([false, false], JoinedRuins.Find([], [new(0, 30, 0), new(1, 30, 0)]));
        Assert.Empty(JoinedRuins.Find([new(0, 30, 0)], []));
    }
}
