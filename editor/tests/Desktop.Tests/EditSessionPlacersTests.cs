using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Mods' placers in a step (EditSession.Placed): handed the step's placed objects of their prefabs together, in their
/// new places, with the area; what they set is saved with the objects; objects changed where they stand, and those of
/// other prefabs, are not handed over. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class EditSessionPlacersTests : IDisposable
{
    private static readonly int Rock = StableHash.Of("Rock_4");

    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>Places rocks: notes each step's objects, and gives each its x as an int value.</summary>
    private sealed class RockPlacer : IWorldEditorExtension, IObjectPlacer
    {
        public List<List<Vector3>> Steps { get; } = [];

        public List<int> AreaRocks { get; } = [];

        public void Register(IExtensionRegistry registry) => registry.AddPlacer(this, "Rock_4");

        public void Place(IReadOnlyList<ICopy> placed, IAreaView area)
        {
            Steps.Add([.. placed.Select(p => p.Position)]);
            AreaRocks.Add(area.ObjectsOf("Rock_4").Count);
            foreach (var p in placed)
            {
                Assert.Equal("Rock_4", p.Prefab);
                p.SetInt("x", (int)p.Position.X);
            }
        }
    }

    private static (NewObject, bool) New(int prefab, Vector3 at) => (new NewObject(0, prefab, at, Vector3.Zero, 0), false);

    private static int? X(EditSession s, int index) =>
        ZdoData.Parse(ObjectData.Bytes(s.Scene.World, s.Edits, s.Scene.Things[index].Id)!).GetInt(StableHash.Of("x"));

    [Fact]
    public void PlacedObjectsAreHandedOverTogetherAndKeepWhatIsSet()
    {
        var placer = new RockPlacer();
        ModExtensions.UseForTests(("Rocks", placer));
        var s = TestScenes.Flat(1);

        var made = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5)), New(StableHash.Of("Beech1"), new Vector3(12, 30, 5)), New(Rock, new Vector3(20, 30, 5))]);

        var step = Assert.Single(placer.Steps);
        Assert.Equal([new Vector3(10, 30, 5), new Vector3(20, 30, 5)], step);
        Assert.Equal([10, null, 20], [X(s, made[0]), X(s, made[1]), X(s, made[2])]);
        Assert.Equal(0, Assert.Single(placer.AreaRocks));
    }

    [Fact]
    public void AnObjectMovedIsPlacedAndLeftOutOfTheArea()
    {
        var placer = new RockPlacer();
        ModExtensions.UseForTests(("Rocks", placer));
        var s = TestScenes.Flat(1);
        int rock = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5))])[0];

        int moved = s.Move([(rock, new Vector3(30, 30, 5), Vector3.Zero)])[0];

        Assert.Equal([new Vector3(30, 30, 5)], placer.Steps[1]);
        Assert.Equal(0, placer.AreaRocks[1]);
        Assert.Equal(30, X(s, moved));
    }

    [Fact]
    public void ObjectsAStepsGroundPutsDownArePlaced()
    {
        // A paste's objects are listed by its ground step, as they stand on the ground it leaves.
        var placer = new RockPlacer();
        ModExtensions.UseForTests(("Rocks", placer));
        var s = TestScenes.Flat(1);
        var add = new List<(NewObject, bool)>();

        var made = s.Commit("Paste", g =>
        {
            add.Add(New(Rock, new Vector3(40, 30, 5)));
            return ([], (0, 0, 0, 0));
        }, [], add);

        Assert.Equal([new Vector3(40, 30, 5)], Assert.Single(placer.Steps));
        Assert.Equal(40, X(s, made[0]));
    }

    [Fact]
    public void AnObjectChangedWhereItStandsIsNoPlacement()
    {
        var placer = new RockPlacer();
        ModExtensions.UseForTests(("Rocks", placer));
        var s = TestScenes.Flat(1);
        int rock = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5))])[0];
        byte[] bytes = ObjectData.Bytes(s.Scene.World, s.Edits, s.Scene.Things[rock].Id)!;

        s.Commit("Edit", null, [rock], [(new NewObject(0, Rock, new Vector3(10, 30, 5), Vector3.Zero, 0, null, false, bytes), false)]);

        Assert.Single(placer.Steps);
    }

    [Fact]
    public void WithoutPlacersAStepIsLeftAsItIs()
    {
        var s = TestScenes.Flat(1);

        var made = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5))]);

        Assert.Null(X(s, made[0]));
    }
}
