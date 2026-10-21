using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.AppDataPrefabs;

namespace WorldEditor.Tests;

/// <summary>
/// An object's data for the inspector and the contents built from items within the game's limits.
/// </summary>
[Collection("World files")]
public class AppDataObjectTests
{
    [Fact]
    public void AnObjectsBytesComeFromTheSaveOrTheSession()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        Assert.NotNull(ObjectData.Bytes(world, edits, 0));
        Assert.Null(ObjectData.Bytes(world, edits, world.ObjectRefs.Count));
        Assert.Null(ObjectData.Bytes(world, edits, -9));
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Beech);
        edits.AddObjects([new NewObject(-9, Beech, position + Vector3.UnitX, Vector3.Zero, 1f)]);
        var z = ZdoData.Parse(ObjectData.Bytes(world, edits, -9)!);
        Assert.Equal(Beech, z.Prefab);
        // Deleted again, it is still there for undo.
        edits.SetDeleted([-9], true);
        Assert.NotNull(ObjectData.Bytes(world, edits, -9));
    }

    [Fact]
    public void ContentsAreBuiltFromItemsWithinTheGamesLimits()
    {
        // The catalogue knows how large a stack can be (50 for Wood and Stone; CryptKey does not stack).
        Assert.Equal(50, TerrainEditor.Terrain.PrefabCatalog.ItemKindOf(StableHash.Of("Wood"))!.MaxStack);
        Assert.Equal(1, TerrainEditor.Terrain.PrefabCatalog.ItemKindOf(StableHash.Of("CryptKey"))!.MaxStack);
        var notes = new List<string>();
        var inv = ObjectData.BuildInventory(
        [
            new ItemUpload("Wood", null, Stack: 0, Quality: 99999, X: 300, Y: -4, WorldLevel: 900, CrafterId: "123", CrafterName: "Ragnar"),
            new ItemUpload(null, StableHash.Of("Stone"), Stack: 70000, CrafterId: "not a number"),
            new ItemUpload("Stone", null, Stack: 50),
            new ItemUpload("CryptKey", null, Stack: 3),
        ], notes);
        var wood = inv.Items[0];
        Assert.Equal((1, 65535, 255, 0, 255, 123L, "Ragnar"), (wood.Stack, wood.Quality, wood.X, wood.Y, wood.WorldLevel, wood.CrafterId, wood.CrafterName));
        // A stack above the item's maximum is cut to it and said; one at the maximum is kept; an item that does not
        // stack is cut to one.
        Assert.Equal(50, inv.Items[1].Stack);
        Assert.Equal(0, inv.Items[1].CrafterId);
        Assert.Equal(50, inv.Items[2].Stack);
        Assert.Equal(1, inv.Items[3].Stack);
        Assert.Equal(["Stone: stack of 65535 cut to 50 (the item's maximum)", "CryptKey: stack of 3 cut to 1 (the item's maximum)"], notes);
        var ex = Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory([new ItemUpload("NoSuchItem", null)]));
        Assert.Contains("NoSuchItem", ex.Message);
        Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory([new ItemUpload(null, 12345)]));
        // A prefab the game knows but that is not an item (a creature, a piece) is refused too: the game drops it.
        Assert.Contains("Troll", Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory([new ItemUpload("Troll", null)])).Message);
        Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory([new ItemUpload(null, StableHash.Of("piece_chest"))]));
    }

    [Fact]
    public void AnEditedObjectGetsItsValuesAndContents()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        byte[] bytes = world.ObjectBytes(world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id);
        var z = ObjectData.Edited(bytes, [new FieldChange("strings", "text", "Hi"), new FieldChange("ints", "health_level", "3")], [new ItemUpload("Wood", null, Stack: 7)]);
        Assert.Equal("Hi", z.StringList.Single(s => s.Key == StableHash.Of("text")).Value);
        Assert.Equal(7, InventoryData.Read(z.GetBytes(ObjectData.ItemsKey)!).Items.Single().Stack);
        // Nothing asked: the same object.
        Assert.Equal(bytes, ObjectData.Edited(bytes, null, null).Serialize());
    }
}
