using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static TerrainEditor.App.GameBundles;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The items the 3D view draws on the area's stands (StandItems): each item stand's item and each armour stand's
/// slots from their data, placed on their stand and keyed by their held model; stands holding nothing, items without
/// a model and objects that are no stand left out; read again after an edit.
/// </summary>
public class StandItemsTests
{
    private static readonly int Wall = StableHash.Of("itemstand"), Armour = StableHash.Of("ArmorStand"), Shield = StableHash.Of("Shield"), Helmet = StableHash.Of("HelmetBronze");

    /// <summary>A stand's points: a wall stand with its attach point 0.2 m up, an armour stand with a head and a left hand.</summary>
    private static StandPoints? Points(string name) => name switch
    {
        "itemstand" => new StandPoints(Matrix.Trs(0, 0.2f, 0, 0, 0, 0, 1, 1, 1, 1), 1, null, new Dictionary<string, float[]>()),
        "ArmorStand" => new StandPoints(null, 0, [6, 0], new Dictionary<string, float[]> { ["helmet"] = Matrix.Trs(0, 1.7f, 0, 0, 0, 0, 1, 1, 1, 1), ["leftHand"] = Matrix.Trs(0.3f, 1, 0, 0, 0, 0, 1, 1, 1, 1) }),
        _ => null,
    };

    /// <summary>The held models: the shield's and the helmet's attach child 0.05 m down, nothing else.</summary>
    private static HeldModel? Held(string item, HeldUse use) =>
        item is "Shield" or "HelmetBronze" ? new HeldModel(new Model(StandLook.ModelName(item, use), [], [1, 1, 1]), [0, -0.05f, 0], [0, 0, 0, 1], [1, 1, 1], false, 0, [], null) : null;

    /// <summary>A stand's saved data with these values.</summary>
    private static byte[] Data(int prefab, Vector3 at, params (string Key, int Value)[] ints) =>
        ZdoBuilder.Blank(prefab, 0x0B00, at, Vector3.Zero, 0f, [.. ints.Select(i => (StableHash.Of(i.Key), i.Value))]);

    [Fact]
    public void TheStandsItemsAreFoundFromTheirDataAndPlacedOnThem()
    {
        var session = TestScenes.Flat();
        var s = session.Scene;
        var wallAt = new Vector3(10, 30, 10);
        var armourAt = new Vector3(20, 30, 20);
        session.Commit("stands", null, [],
        [
            (new NewObject(0, Wall, wallAt, new Vector3(0, 90, 0), 0, Fresh: false, Raw: Data(Wall, wallAt, ("item", Shield), ("quality", 2), ("type", 1))), true),
            (new NewObject(0, Armour, armourAt, Vector3.Zero, 0, Fresh: false, Raw: Data(Armour, armourAt, ("0_item", Helmet), ("1_item", Shield))), true),
            // Holding nothing, holding an item without a model, and no stand at all.
            (new NewObject(0, Wall, new Vector3(30, 30, 30), Vector3.Zero, 0, Fresh: false, Raw: Data(Wall, new Vector3(30, 30, 30))), true),
            (new NewObject(0, Wall, new Vector3(40, 30, 40), Vector3.Zero, 0, Fresh: false, Raw: Data(Wall, new Vector3(40, 30, 40), ("item", StableHash.Of("Wood")))), true),
            (new NewObject(0, StableHash.Of("Beech1"), new Vector3(50, 30, 50), Vector3.Zero, 0, Fresh: false, Raw: Data(StableHash.Of("Beech1"), new Vector3(50, 30, 50), ("item", Shield))), false),
        ]);
        var found = StandItems.Find(s, Points, Held);
        Assert.Equal(3, found.Count);
        var onWall = found[0];
        Assert.Equal(StandLook.KeyOf("Shield/stand"), onWall.Model);
        Assert.Equal((wallAt, new Vector3(0, 90, 0), 0f, ObjectKind.Buildings), (onWall.Position, onWall.Rotation, onWall.Scale, onWall.Kind));
        // 0.2 m up the stand, the attach child 0.05 m down: in the view's space.
        Assert.Equal(new Vector3(0, 0.15f, 0), onWall.Pre.Translation);
        Assert.Equal([StandLook.KeyOf("HelmetBronze/hand"), StandLook.KeyOf("Shield/hand")], found.Skip(1).Select(h => h.Model));
        Assert.Equal(new Vector3(0, 1.7f, 0), found[1].Pre.Translation);
        Assert.Equal(new Vector3(0.3f, 1, 0), found[2].Pre.Translation);
        // The first stand deleted: its item goes with it.
        session.Delete([0]);
        Assert.Equal(2, StandItems.Find(s, Points, Held).Count);
    }

    [Fact]
    public void WithoutTheGameNothingIsDrawnAndAReadIsKeptUntilAnEdit()
    {
        var session = TestScenes.Flat();
        var s = session.Scene;
        var first = StandItems.Of(s);
        Assert.Empty(first);
        Assert.Same(first, StandItems.Of(s));
        Assert.Same(first, StandItems.Shown(s, () => { }));
        session.Commit("stand", null, [], [(new NewObject(0, Wall, new Vector3(10, 30, 10), Vector3.Zero, 0, Fresh: false, Raw: Data(Wall, new Vector3(10, 30, 10), ("item", Shield))), true)]);
        Assert.NotSame(first, StandItems.Of(s));
    }
}
