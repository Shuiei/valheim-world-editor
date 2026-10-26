using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Searching the whole world for kinds, items in containers and texts.
/// </summary>
public class WorldSearchTests
{
    [Fact]
    public void FindsObjectsOfAKind()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var r = WorldSearch.Search(world, edits, "beech", "kinds");
        int beeches = world.Objects.Count(o => o.Prefab == Fixtures.Hash("Beech1"));
        Assert.Equal(beeches, r.Counts["Beech1"]);
        Assert.All(r.Hits, h => Assert.Contains("eech", h.Name));
        // Deleted objects are not found, new ones are.
        edits.SetDeleted([r.Hits[0].Id], true);
        edits.AddObjects([new NewObject(-5, Fixtures.Hash("Beech1"), new Vector3(1, 2, 3), Vector3.Zero, 0f)]);
        var again = WorldSearch.Search(world, edits, "Beech1", "kinds");
        Assert.Equal(beeches, again.Total);
        Assert.Contains(again.Hits, h => h.Id == -5);
        // Bookkeeping objects only when asked for.
        Assert.DoesNotContain(WorldSearch.Search(world, edits, "e", "kinds").Hits, h => h.Name.StartsWith('_'));
        Assert.NotEmpty(WorldSearch.Search(world, edits, "_zonectrl", "kinds").Hits);
    }

    /// <summary>
    /// An edit store over the world, and the id and parsed object data of its first wooden chest.
    /// </summary>
    private static (EditStore Edits, int Chest, ZdoData Z) WoodenChestOf(WorldSave world)
    {
        var edits = new EditStore(world);
        int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
        return (edits, chest, ZdoData.Parse(world.ObjectBytes(chest)));
    }

    [Fact]
    public void FindsItemsInContainersAndTexts()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (edits, chest, z) = WoodenChestOf(world);
        var inv = new InventoryData();
        inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 30 });
        inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 12, X = 1 });
        z.SetBytes(StableHash.Of("items"), inv.Write());
        z.Set("strings", StableHash.Of("text"), "Odin's stash");
        edits.AddObjects([new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, null, false, z.Serialize())]);
        var items = WorldSearch.Search(world, edits, "wood", "items");
        var hit = Assert.Single(items.Hits);
        Assert.Equal(-1, hit.Id);
        Assert.Equal("Wood ×42", hit.Match);
        Assert.Equal(42, items.Counts["Wood"]);
        var texts = WorldSearch.Search(world, edits, "odin", "texts");
        Assert.Equal("text: Odin's stash", Assert.Single(texts.Hits).Match);
        Assert.Empty(WorldSearch.Search(world, edits, "", "kinds").Hits);
    }

    // One damaged object (a byte array with a negative length) or one chest whose items bytes have a bad length prefix
    // is skipped, and the search goes on with the rest instead of ending with an exception.
    [Fact]
    public void ADamagedObjectIsSkippedAndTheRestFound()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (edits, chest, z) = WoodenChestOf(world);
        z.SetBytes(StableHash.Of("damaged"), []);
        byte[] broken = z.Serialize();
        broken.AsSpan(^4).Fill(0xFF); // the (last) byte array's length: -1
        Assert.Throws<InvalidDataException>(() => ZdoData.Parse(broken));
        // Item format 108, one item with a crafter whose name's length prefix never ends.
        byte[] badItems = [108, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        Assert.Throws<FormatException>(() => InventoryData.Read(badItems));
        var unreadable = ZdoData.Parse(world.ObjectBytes(chest));
        unreadable.SetBytes(StableHash.Of("items"), badItems);
        var good = ZdoData.Parse(world.ObjectBytes(chest));
        var inv = new InventoryData();
        inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 5 });
        good.SetBytes(StableHash.Of("items"), inv.Write());
        good.Set("strings", StableHash.Of("text"), "still here");
        edits.AddObjects(
        [
            new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, null, false, broken),
            new NewObject(-2, z.Prefab, z.Position, z.Rotation, 0f, null, false, unreadable.Serialize()),
            new NewObject(-3, z.Prefab, z.Position, z.Rotation, 0f, null, false, good.Serialize()),
        ]);
        Assert.Equal(-3, Assert.Single(WorldSearch.Search(world, edits, "wood", "items").Hits).Id);
        Assert.Equal(-3, Assert.Single(WorldSearch.Search(world, edits, "still", "texts").Hits).Id);
    }
}
