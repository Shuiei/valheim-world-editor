using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.AppDataPrefabs;

namespace WorldEditor.Tests;

/// <summary>
/// Searching the objects of a copy of the test world: containers by what they hold, texts, objects added in the editor and the live snapshot.
/// </summary>
[Collection("World files")]
public class AppDataSearchTests
{
    /// <summary>
    /// The test world's objects carry little data: a chest with contents and a sign text is saved first.
    /// </summary>
    private static WorldSave WithChest(TempWorld w, out int chestId)
    {
        WorldSave world = w.Load();
        int chest = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id;
        var z = ZdoData.Parse(world.ObjectBytes(chest));
        var inv = new InventoryData();
        inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 42 });
        inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 8, X = 1 });
        inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Stone"), Stack = 5, X = 2 });
        z.SetBytes(ObjectData.ItemsKey, inv.Write());
        z.Set("strings", StableHash.Of("text"), "Welcome home");
        // A second chest whose contents cannot be read.
        var broken = ZdoData.Parse(world.ObjectBytes(chest));
        broken.SetBytes(ObjectData.ItemsKey, "\t\t\t"u8.ToArray());
        var r = WorldWriter.Save(world, [], added:
        [
            new NewObject(-1, z.Prefab, z.Position + new Vector3(3, 0, 3), Vector3.Zero, 0f, Fresh: false, Raw: z.Serialize()),
            new NewObject(-2, z.Prefab, z.Position + new Vector3(6, 0, 3), Vector3.Zero, 0f, Fresh: false, Raw: broken.Serialize()),
        ]);
        Assert.True(r.Saved, r.Message);
        world = w.Load();
        chestId = Enumerable.Range(0, world.ObjectRefs.Count).Single(i => ZdoData.Parse(world.ObjectBytes(i)).StringList.Any(s => s.Value == "Welcome home"));
        return world;
    }

    [Fact]
    public void ContainersAreFoundByWhatTheyHold()
    {
        using var w = new TempWorld();
        WorldSave world = WithChest(w, out int chest);
        var edits = new EditStore(world);
        var r = WorldSearch.Search(world, edits, "WOOD", "items");
        var hit = Assert.Single(r.Hits);
        Assert.Equal(chest, hit.Id);
        Assert.Equal("Wood ×50", hit.Match);
        Assert.Equal(50, r.Counts["Wood"]);
        Assert.Empty(WorldSearch.Search(world, edits, "Silver", "items").Hits);
        // Deleted: no longer found.
        edits.SetDeleted([chest], true);
        Assert.Empty(WorldSearch.Search(world, edits, "wood", "items").Hits);
    }

    [Fact]
    public void TextsAreFound()
    {
        using var w = new TempWorld();
        WorldSave world = WithChest(w, out int chest);
        var r = WorldSearch.Search(world, new EditStore(world), "welcome", "texts");
        var hit = Assert.Single(r.Hits);
        Assert.Equal(chest, hit.Id);
        Assert.Equal("text: Welcome home", hit.Match);
        Assert.Empty(WorldSearch.Search(world, new EditStore(world), "nothing like it", "texts").Hits);
    }

    [Fact]
    public void ObjectsAddedInTheEditorAreFoundToo()
    {
        using var w = new TempWorld();
        WorldSave world = WithChest(w, out int chest);
        var edits = new EditStore(world);
        var copy = ZdoData.Parse(world.ObjectBytes(chest));
        copy.Set("strings", StableHash.Of("text"), "Copied sign");
        edits.AddObjects(
        [
            new NewObject(-5, copy.Prefab, copy.Position + new Vector3(1, 0, 0), Vector3.Zero, 0f, Fresh: false, Raw: copy.Serialize()),
            new NewObject(-6, Beech, copy.Position + new Vector3(2, 0, 0), Vector3.Zero, 1f),
        ]);
        Assert.Contains(WorldSearch.Search(world, edits, "copied", "texts").Hits, h => h.Id == -5);
        Assert.Contains(WorldSearch.Search(world, edits, "wood", "items").Hits, h => h.Id == -5);
        Assert.Contains(WorldSearch.Search(world, edits, "beech1", "kinds").Hits, h => h.Id == -6);
        // An added object of a kind the game does not have holds nothing to search.
        edits.AddObjects([new NewObject(-7, StableHash.Of("NoSuchKind_x"), copy.Position, Vector3.Zero, 1f)]);
        Assert.DoesNotContain(WorldSearch.Search(world, edits, "a", "texts").Hits, h => h.Id == -7);
    }

    [Fact]
    public void SearchingNothingFindsNothingAndManyHitsAreCut()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var none = WorldSearch.Search(world, edits, "   ", "kinds");
        Assert.Equal(0, none.Total);
        Assert.Empty(none.Hits);
        var all = WorldSearch.Search(world, edits, "e", "kinds");
        Assert.True(all.Total > 2);
        var cut = WorldSearch.Search(world, edits, "e", "kinds", limit: 2);
        Assert.Equal(2, cut.Hits.Count);
        Assert.True(cut.Truncated);
        Assert.Equal(all.Total, cut.Total);
    }

    [Fact]
    public void TheLiveWorldIsSearchedInItsSnapshot()
    {
        var live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(live);
        Assert.NotEmpty(WorldSearch.Search(live, edits, "beech", "kinds").Hits);
        // Containers and texts are read from the snapshot's bytes (no files).
        var items = WorldSearch.Search(live, edits, "zzz-no-such-item", "items");
        Assert.Empty(items.Hits);
        WorldSearch.Search(live, edits, "zzz", "texts");
    }
}
