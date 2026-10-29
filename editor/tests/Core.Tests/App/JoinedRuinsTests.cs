using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A ruin's pieces joined to a player's building: the ones touching a built piece, then the ones touching those, up
/// and down as well, over a whole world by id; a ruin standing apart, and every ruin when nothing is built, stay apart.
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

    // Over a world: its ruin pieces (catalogue pieces among the builder-less objects; not trees, not deleted ones)
    // joined to its built pieces or to more given ones.
    [Fact]
    public void AWorldsJoinedRuinPiecesAreFoundById()
    {
        var world = new TerrainEditor.Save.WorldSave { Directory = "none", SaveNumber = 1 };
        int stone = TerrainEditor.Save.StableHash.Of("stone_wall_2x1"), beech = TerrainEditor.Save.StableHash.Of("Beech1");
        world.Pieces.Add((1, TerrainEditor.Save.StableHash.Of("wood_wall_half"), new Vector3(0, 30, 0), Vector3.Zero));
        world.Objects.Add((2, stone, new Vector3(2, 30, 0), Vector3.Zero, Vector3.One));
        world.Objects.Add((3, beech, new Vector3(1, 30, 0), Vector3.Zero, Vector3.One));
        world.Objects.Add((4, stone, new Vector3(100, 30, 0), Vector3.Zero, Vector3.One));
        world.Objects.Add((5, stone, new Vector3(-2, 30, 0), Vector3.Zero, Vector3.One));
        Assert.Equal([2], JoinedRuins.InWorld(world, [5]).Order());
        Assert.Equal([2, 4], JoinedRuins.InWorld(world, [5], [new Vector3(102, 30, 0)]).Order());
    }

    [Fact]
    public void NothingBuiltJoinsNothing()
    {
        Assert.Equal([false, false], JoinedRuins.Find([], [new(0, 30, 0), new(1, 30, 0)]));
        Assert.Empty(JoinedRuins.Find([new(0, 30, 0)], []));
    }
}
