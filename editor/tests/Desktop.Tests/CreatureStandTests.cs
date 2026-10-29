using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Creatures never sunk into pieces (CreatureStand): a creature put inside a piece is stood on top of it, a creature on
/// the ground and anything that is not a creature stay; and each way of positioning objects (pieces put down, moves,
/// copies, scripts, blueprints opened and the support check) goes through it.
/// </summary>
[Collection("World files")]
public class CreatureStandTests
{
    private static readonly int Boar = StableHash.Of("Boar");
    private static readonly int Wall = StableHash.Of("woodwall");

    /// <summary>A wooden wall standing with its foot at 30 m (its middle at 31), as thing 0 of a flat area.</summary>
    private static WorldScene.Thing WallThing() => new(7, Wall, new Vector3(20, 31, 20), Vector3.Zero, 0, true);

    /// <summary>A new object at a point, scale as the prefab's.</summary>
    private static (NewObject Object, bool Piece) At(int prefab, float x, float y, float z) => (new NewObject(0, prefab, new Vector3(x, y, z), Vector3.Zero, 0), false);

    [Fact]
    public void ACreatureInsideAPieceIsStoodOnItAndOthersStay()
    {
        var s = TestScenes.Flat(1, WallThing());
        var adds = new List<(NewObject Object, bool Piece)>
        {
            At(Boar, 20, 30.5f, 20),
            At(Boar, 20, 30, 40),
            At(StableHash.Of("rock4_forest"), 20, 30.5f, 20),
            At(Boar, 20, 32.5f, 20),
        };
        Assert.Equal(1, CreatureStand.Apply(s.Scene, adds));
        Assert.Equal(32f, adds[0].Object.Position.Y, 3);
        Assert.Equal(30f, adds[1].Object.Position.Y, 3);
        Assert.Equal(30.5f, adds[2].Object.Position.Y, 3);
        Assert.Equal(32.5f, adds[3].Object.Position.Y, 3);
        Assert.Equal("1 creature(s) stood on the piece under them.", CreatureStand.Note(1).English);
        Assert.Equal(Said.Empty, CreatureStand.Note(0));
    }

    // Moved: onto a piece that stays, onto one moved in the same step (where it goes), and not onto one leaving.
    [Fact]
    public void AMovedCreatureStandsOnThePiecesAsTheyWillBe()
    {
        var s = TestScenes.Flat(1, WallThing(), new WorldScene.Thing(8, Boar, new Vector3(40, 30, 40), Vector3.Zero, 0, false));
        var moves = new List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)> { (1, new Vector3(20, 30.5f, 20), Vector3.Zero, 0) };
        Assert.Equal(1, CreatureStand.Apply(s.Scene, null, moves));
        Assert.Equal(32f, moves[0].Position.Y, 3);
        // The wall moved away in the same step: the creature stays where it is put.
        moves = [(1, new Vector3(20, 30.5f, 20), Vector3.Zero, 0), (0, new Vector3(50, 31, 50), Vector3.Zero, 0)];
        Assert.Equal(0, CreatureStand.Apply(s.Scene, null, moves));
        // The wall taken away in the same step: the same.
        moves = [(1, new Vector3(20, 30.5f, 20), Vector3.Zero, 0)];
        Assert.Equal(0, CreatureStand.Apply(s.Scene, null, moves, [0]));
        // A wall added in the same step holds the creature up.
        var adds = new List<(NewObject Object, bool Piece)> { (new NewObject(0, Wall, new Vector3(40, 31, 40), Vector3.Zero, 0), true), At(Boar, 40, 30.5f, 40) };
        Assert.Equal(1, CreatureStand.Apply(s.Scene, adds));
        Assert.Equal(32f, adds[1].Object.Position.Y, 3);
    }

    [Fact]
    public void TheCreaturesSunkIntoPiecesAreFound()
    {
        var s = TestScenes.Flat(1, WallThing(), new WorldScene.Thing(8, Boar, new Vector3(20, 30.5f, 20), Vector3.Zero, 0, false),
            new WorldScene.Thing(9, Boar, new Vector3(40, 30, 40), Vector3.Zero, 0, false));
        Assert.Equal([1], CreatureStand.Sunk(s.Scene));
    }

    [Fact]
    public void AScriptsNewAndMovedCreaturesStandOnThePieces()
    {
        var s = TestScenes.Flat(1, WallThing(), new WorldScene.Thing(8, Boar, new Vector3(40, 30, 40), Vector3.Zero, 0, false));
        var ch = new ScriptHost.Changes();
        ch.Add.Add((Boar, "Boar", new Vector3(20, 30.5f, 20), Vector3.Zero, 0, false));
        ch.Moves[1] = (new Vector3(20.5f, 30.2f, 20), Vector3.Zero, 0);
        Assert.Equal((0, 2), ScriptHost.Apply(s, ch, "Script: test"));
        Assert.All(s.Scene.Things.Where(t => !t.Gone && t.Prefab == Boar), t => Assert.Equal(32f, t.Position.Y, 3));
    }

    [Fact]
    public void ABlueprintsCreatureStandsOnItsPieces()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-cs-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "Pen.blueprint");
            File.WriteAllText(path, "#Name:Pen\n#HomesteadVersion:1\n#Pieces\nwoodwall;Building;0;1;0;0;0;0;1;\"\";1;1;1\nBoar;Building;0;0.5;0;0;0;0;1;\"\";1;1;1\n");
            var scene = Workshop.Create("Workshop");
            var (placed, _, lift, _, stood) = Workshop.Open(scene.Session!, path);
            Assert.Equal((2, 1), (placed, stood));
            var boar = Assert.Single(scene.Things, t => !t.Gone && t.Prefab == Boar);
            Assert.Equal(Workshop.Ground + lift + 2, boar.Position.Y, 3);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Through Claude's tools on the plot: put down, placed exactly, moved and copied, each answer giving the final y
    // and how many creatures were stood.
    [AvaloniaFact]
    public async Task ClaudesToolsStandCreaturesOnThePieces()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        var put = ClaudeRun.J(await r.T.PlacePieces(
        [
            new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 0, Y = 35, Snap = false },
            new ClaudeTools.PieceSpec { Prefab = "Boar", X = 0, Z = 0, Y = 34.5f, Snap = false },
            new ClaudeTools.PieceSpec { Prefab = "Boar", X = 20, Z = 20, Snap = false },
        ]));
        Assert.Equal(1, put.GetProperty("stood").GetInt32());
        Assert.Equal(36f, put.GetProperty("placed")[1].GetProperty("piece").GetProperty("y").GetSingle(), 3);
        Assert.EndsWith("1 creature(s) stood on the piece under them.", r.W.LastMessageEn);
        int free = put.GetProperty("placed")[2].GetProperty("id").GetInt32();

        var placed = ClaudeRun.J(await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = free, X = 0, Y = 34.5f, Z = 0 }]));
        Assert.Equal(1, placed.GetProperty("stood").GetInt32());
        Assert.Equal(36f, placed.GetProperty("placed")[0].GetProperty("y").GetSingle(), 3);
        int boar = placed.GetProperty("placed")[0].GetProperty("id").GetInt32();

        var moved = ClaudeRun.J(await r.T.MoveObjects([boar], dy: -1));
        Assert.Equal(1, moved.GetProperty("stood").GetInt32());
        Assert.Equal(36f, moved.GetProperty("moved")[0].GetProperty("y").GetSingle(), 3);
        boar = moved.GetProperty("moved")[0].GetProperty("id").GetInt32();

        var copied = ClaudeRun.J(await r.T.CopyObjects([boar], dy: -1));
        Assert.Equal(1, copied.GetProperty("stood").GetInt32());
        Assert.Equal(36f, copied.GetProperty("copied")[0].GetProperty("y").GetSingle(), 3);

        // Nothing left inside a piece: the support check says so.
        Assert.Empty(ClaudeRun.J(await r.T.SupportCheck()).GetProperty("creaturesInPieces").EnumerateArray());
    }
}
