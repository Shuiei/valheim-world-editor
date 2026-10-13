using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The things of an area read from a save: its objects, building pieces and added objects inside it (not deleted ones,
/// nor the invisible ground discs), and things found by id wherever they are, gone or not; a ship's parts drawn where
/// the mod builds them; and the first world on this computer found when none is named.
/// </summary>
public class WorldSceneThingsTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half"), Beech = StableHash.Of("Beech1");

    /// <summary>
    /// A save with a tree at 5, 5 (id 1), pieces in zone 0, 0 (id 2, turned 90°, and id 4) and out of it (id 3), and
    /// object 5 removed in the game since (live).
    /// </summary>
    private static WorldSave World()
    {
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        world.Objects.Add((1, Beech, new Vector3(5, 30, 5), Vector3.Zero, Vector3.One));
        world.Pieces.Add((2, Wall, new Vector3(10, 31, -10), 90));
        world.Pieces.Add((3, Wall, new Vector3(100, 31, 0), 0));
        world.Pieces.Add((4, Wall, new Vector3(0, 31, 0), 0));
        for (int i = 0; i < 6; i++)
        {
            world.ObjectRefs.Add(new ObjectRef { File = new ChunkFile(), Start = 0, Prefab = Beech, Position = new Vector3(7, 30, 7), IsPiece = i == 5 });
        }
        world.Vanished.Add(5);
        return world;
    }

    [Fact]
    public void AnAreaHasItsObjectsPiecesAndAddedObjects()
    {
        var added = new List<NewObject>
        {
            new(-1, Beech, new Vector3(-5, 30, 5), Vector3.Zero, 1),
            new(-2, WorldSave.LocationProxyPrefab, new Vector3(1, 30, 1), Vector3.Zero, 1),
            new(-3, Beech, new Vector3(0, 30, 200), Vector3.Zero, 1),
        };
        var things = WorldScene.ReadThings(World(), 0, 0, 1, 1, new HashSet<int> { 4 }, added);
        Assert.Equal([1, 2, -1], things.Select(t => t.Id));
        Assert.False(things[0].Piece);
        Assert.True(things[1].Piece);
        Assert.Equal(new Vector3(0, 90, 0), things[1].Rotation);
        Assert.Equal(new Vector3(-5, 30, 5), things[2].Position);
        Assert.False(things[2].Piece);
    }

    // A ruin's piece (a catalogue piece with no builder) touching the area's building is joined to it, and one at
    // the area's edge touching a building just outside it, or a piece added in this session; one apart is not.
    [Fact]
    public void AnAreasRuinPiecesJoinedToBuildingsAreMarked()
    {
        var world = World();
        int stone = StableHash.Of("stone_wall_2x1");
        world.Objects.Add((6, stone, new Vector3(10, 31, -6), Vector3.Zero, Vector3.One));
        world.Objects.Add((7, stone, new Vector3(-20, 31, 20), Vector3.Zero, Vector3.One));
        world.Objects.Add((8, stone, new Vector3(31, 31, 0), Vector3.Zero, Vector3.One));
        world.Pieces.Add((9, Wall, new Vector3(34, 31, 0), 0));
        world.Objects.Add((10, stone, new Vector3(-28, 31, -28), Vector3.Zero, Vector3.One));
        var added = new List<NewObject> { new(-1, Wall, new Vector3(-30, 31, -30), Vector3.Zero, 1) };
        var things = WorldScene.ReadThings(world, 0, 0, 1, 1, new HashSet<int>(), added);
        Assert.Equal([6, 8, 10], things.Where(t => t.Joined).Select(t => t.Id).Order());
        Assert.Contains(things, t => t.Id == 7 && !t.Joined);
    }

    [Fact]
    public void ThingsAreFoundByIdGoneOrNot()
    {
        var world = World();
        var edits = new EditStore(world);
        edits.AddObjects([new NewObject(-1, Wall, new Vector3(1, 30, 1), Vector3.Zero, 1)]);
        // Added and deleted again: undo can bring it back.
        edits.KeepGone([new NewObject(-9, Beech, new Vector3(2, 30, 2), Vector3.Zero, 1)]);
        edits.SetDeleted([2], true);
        var found = WorldScene.FindThings(world, edits, [-1, -9, -7, 1, 2, 5, 99]);
        Assert.Equal([-9, -1, 1, 2, 5], found.Keys.Order());
        Assert.False(found[-1].Gone);
        Assert.True(found[-1].Piece);
        Assert.True(found[-9].Gone);
        Assert.False(found[-9].Piece);
        Assert.False(found[1].Gone);
        Assert.True(found[2].Gone);
        Assert.True(found[2].Piece);
        // Removed in the game: gone for good, where it was.
        Assert.True(found[5].Gone);
        Assert.True(found[5].Piece);
        Assert.Equal(new Vector3(7, 30, 7), found[5].Position);
    }

    // A ValheimRAFT ship's part, stored at its root, is drawn at its offset in the root's frame, the root leaning as
    // stored (a building piece read keeps only its turn about y); found by id the same way; with its root deleted,
    // where it is stored.
    [Fact]
    public void AShipsPartIsDrawnWhereTheModBuildsIt()
    {
        var world = World();
        var at = new Vector3(10, 30, 10);
        world.Pieces.Add((6, Wall, at, 90));
        world.Pieces.Add((7, Wall, at, 0));
        for (int i = 6; i < 8; i++)
        {
            world.ObjectRefs.Add(new ObjectRef { File = new ChunkFile(), Start = 0, Prefab = Wall, Position = at, IsPiece = true });
        }
        var raft = ObjectLinks.Current.Rules.Single(r => r.IdKey == StableHash.Of("PersistentID"));
        world.Links[6] = (new ObjectLink(raft, 500, [0, 0], Vector3.Zero, null, null), new Vector3(10, 90, 0));
        world.Links[7] = (new ObjectLink(raft, 0, [0, 500], new Vector3(2, 0, 0), Vector3.Zero, null), Vector3.Zero);
        var things = WorldScene.ReadThings(world, 0, 0, 1, 1, new HashSet<int>());
        Assert.Equal(new Vector3(10, 90, 0), things.Single(t => t.Id == 6).Rotation);
        var part = things.Single(t => t.Id == 7);
        Assert.True(Vector3.Distance(new Vector3(10, 30, 8), part.Position) < 1e-3f, part.Position.ToString());
        Assert.Equal(10, part.Rotation.X, 2);
        Assert.Equal(90, part.Rotation.Y, 2);
        var edits = new EditStore(world);
        Assert.Equal(part, WorldScene.FindThings(world, edits, [7])[7]);
        edits.SetDeleted([6], true);
        Assert.Equal(at, WorldScene.FindThings(world, edits, [7])[7].Position);
    }

    [Fact]
    public void WithNoWorldNamedTheFirstOnThisComputerIsTaken()
    {
        string? path = null;
        try
        {
            path = WorldScene.FindWorld(null);
        }
        catch (InvalidOperationException e)
        {
            Assert.Equal("No Valheim world found on this computer.", e.Message);
        }
        Assert.True(path == null || Directory.Exists(path) || File.Exists(path), path);
    }
}
