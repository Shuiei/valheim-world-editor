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

    [Fact]
    public void FindsItemsInContainersAndTexts()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
        var z = ZdoData.Parse(world.ObjectBytes(chest));
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
}
