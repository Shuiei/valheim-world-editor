using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Mods' creation rules in a step (EditSession.Created): the objects a step makes go through them (left out, changed,
/// others added beside), what they do is saved with the step; objects moved or changed where they stand are not made.
/// Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class EditSessionCreationTests : IDisposable
{
    private static readonly int Rock = StableHash.Of("Rock_4"), Beech = StableHash.Of("Beech1"), Oak = StableHash.Of("Oak1");

    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>Swaps each Beech1 made for an Oak1 marked "swapped", and marks each Rock_4 made with its x.</summary>
    private sealed class Swapper : IWorldEditorExtension, ICreationRules
    {
        public int Steps { get; private set; }

        public void Register(IExtensionRegistry registry) => registry.AddCreationRules(this);

        public bool Covers(string prefab) => prefab is "Beech1" or "Rock_4";

        public void Created(ICreation made)
        {
            Steps++;
            foreach (var obj in made.Objects)
            {
                if (obj.Prefab == "Beech1")
                {
                    made.Remove(obj);
                    made.Add("Oak1", obj.Position, obj.Rotation).SetInt("swapped", 1);
                }
                else
                {
                    obj.SetInt("x", (int)obj.Position.X);
                }
            }
        }
    }

    private static (NewObject, bool) New(int prefab, Vector3 at) => (new NewObject(0, prefab, at, Vector3.Zero, 0), false);

    private static int? Int(EditSession s, int index, string key) =>
        ZdoData.Parse(ObjectData.Bytes(s.Scene.World, s.Edits, s.Scene.Things[index].Id)!).GetInt(StableHash.Of(key));

    [Fact]
    public void ObjectsMadeAreSwappedAndChanged()
    {
        ModExtensions.UseForTests(("Prefabs", new Swapper()));
        var s = TestScenes.Flat(1);

        var made = s.Commit("Put", null, [], [New(Beech, new Vector3(12, 30, 5)), New(Rock, new Vector3(10, 30, 5))]);

        Assert.Equal([Rock, Oak], made.Select(i => s.Scene.Things[i].Prefab));
        Assert.Equal(10, Int(s, made[0], "x"));
        Assert.Equal(1, Int(s, made[1], "swapped"));
        Assert.Equal(new Vector3(12, 30, 5), s.Scene.Things[made[1]].Position);
    }

    [Fact]
    public void AnObjectMovedOrChangedWhereItStandsIsNotMade()
    {
        var s = TestScenes.Flat(1);
        int rock = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5))])[0];
        var rules = new Swapper();
        ModExtensions.UseForTests(("Prefabs", rules));
        byte[] bytes = ObjectData.Bytes(s.Scene.World, s.Edits, s.Scene.Things[rock].Id)!;

        int edited = s.Commit("Edit", null, [rock], [(new NewObject(0, Rock, new Vector3(10, 30, 5), Vector3.Zero, 0, null, false, bytes), false)])[0];
        int moved = s.Move([(edited, new Vector3(30, 30, 5), Vector3.Zero)])[0];

        Assert.Equal(0, rules.Steps);
        Assert.Null(Int(s, moved, "x"));
    }

    [Fact]
    public void WithoutCreationRulesAStepIsLeftAsItIs()
    {
        var s = TestScenes.Flat(1);

        var made = s.Commit("Put", null, [], [New(Beech, new Vector3(12, 30, 5))]);

        Assert.Equal(Beech, s.Scene.Things[made[0]].Prefab);
    }

    [Fact]
    public void AnEditThatTurnsAnObjectIsNotMadeButANewOneBesideARemovalIs()
    {
        var s = TestScenes.Flat(1);
        int rock = s.Commit("Put", null, [], [New(Rock, new Vector3(10, 30, 5))])[0];
        int beech = s.Commit("Put", null, [], [New(Beech, new Vector3(20, 30, 5))])[0];
        var rules = new Swapper();
        ModExtensions.UseForTests(("Prefabs", rules));
        int id = s.Scene.Things[rock].Id;

        // The inspector turns the rock: it is the rock again.
        var edited = s.Commit("Edit", null, [rock], [(new NewObject(0, Rock, new Vector3(10, 30, 5), new Vector3(0, 90, 0), 0, Remakes: id), false)]);
        // The beech taken away and an unrelated beech put elsewhere: that one is made.
        var made = s.Commit("Swap", null, [beech], [New(Beech, new Vector3(40, 30, 5))]);

        Assert.Single(edited);
        Assert.Null(Int(s, edited[0], "x"));
        Assert.Equal(Oak, s.Scene.Things[Assert.Single(made)].Prefab);
    }
}
