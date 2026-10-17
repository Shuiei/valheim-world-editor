using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveFollowSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Live: the editor follows the game in the zones it shows. A stand-in game says what each zone holds now; what changed there
/// (objects made, removed or changed, ground dug) comes into the open world and area without undo steps.
/// </summary>
public class LiveFollowTests
{
    [Fact]
    public async Task ObjectsRemovedAndMadeInTheGameComeIn()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        int tree = Tree(s);
        int treeId = s.Scene.Things[tree].Id;
        byte[] bytes = w.ObjectBytes(treeId);
        var objects = AsRead(w, (0, 0)).Where(o => (o.Item1, o.Item2) != Zdo(w, treeId)).ToList();
        objects.Add((99, 1, bytes));
        game.Zones[(0, 0)] = (1, objects);
        var f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { treeId }, f.Merged.Vanished);
        int added = Assert.Single(f.Merged.Added);
        var drawn = new List<int>();
        s.ThingsChanged += drawn.AddRange;
        Assert.True(s.TakeGameChanges(f));
        Assert.True(s.Scene.Things[tree].Gone);
        int at = s.Scene.Things.FindIndex(t => t.Id == added);
        Assert.False(s.Scene.Things[at].Gone);
        // Both go to the 3D view (a new thing is drawn only once it is told about).
        Assert.Contains(tree, drawn);
        Assert.Contains(at, drawn);
        Assert.Equal((99L, 1u), Zdo(w, added));
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // The removed one is not found by the map's search any more.
        Assert.DoesNotContain(TerrainEditor.App.WorldSearch.Search(w, world.Edits, Terrain.PrefabCatalog.DisplayName(w.ObjectRefs[treeId].Prefab)!, "kinds").Hits, h => h.Id == treeId);
        // Taken twice (an area opened meanwhile may have it): nothing more.
        int count = s.Scene.Things.Count;
        s.TakeGameChanges(f);
        Assert.Equal(count, s.Scene.Things.Count);
        // Asked again with no change: the zone is not read again.
        int reads = game.ZoneReads;
        Assert.Null(await Follow(world, (0, 0)));
        Assert.Equal(reads, game.ZoneReads);
    }

    // An object the game changed (its data, its place) stays the same object: same id and thing, shown
    // as it is now; one that came from another zone (a creature walking in) is moved, not doubled.
    [Fact]
    public async Task AChangedObjectStaysTheSameObject()
    {
        var (game, world, s, tree, id) = await OpenWithTree();
        using var _ = game;
        var w = world.World;
        var z = ZdoData.Parse(w.ObjectBytes(id));
        z.Position += new Vector3(1, 0, 0);
        int other = Enumerable.Range(0, w.ObjectRefs.Count).First(i => w.ObjectRefs[i].Zone != (0, 0) && w.Objects.Any(o => o.Id == i));
        var walker = ZdoData.Parse(w.ObjectBytes(other));
        walker.Position = new Vector3(5, walker.Position.Y, 5);
        var objects = AsRead(w, (0, 0));
        int at = objects.FindIndex(o => (o.Item1, o.Item2) == Zdo(w, id));
        objects[at] = (objects[at].Item1, objects[at].Item2, z.Serialize());
        objects.Add((Zdo(w, other).Item1, Zdo(w, other).Item2, walker.Serialize()));
        game.Zones[(0, 0)] = (1, objects);
        var f = (await Follow(world, (0, 0)))!;
        Assert.Empty(f.Merged.Added);
        Assert.Empty(f.Merged.Vanished);
        Assert.Equal(new[] { id, other }.Order(), f.Merged.Updated.Order());
        Assert.Equal((0, 0), w.ObjectRefs[other].Zone);
        Assert.True(s.TakeGameChanges(f));
        Assert.Equal(z.Position.X, s.Scene.Things[tree].Position.X, 3);
        Assert.False(s.Scene.Things[tree].Gone);
        Assert.Single(s.Scene.Things, t => t.Id == other);
    }

    // The editor's applied deletions stand whatever the game did; an object the game removed is gone for
    // good: undo and redo leave it alone, and a pending deletion of it is nothing left to apply.
    [Fact]
    public async Task DeletionsAndUndoAgainstWhatTheGameRemoved()
    {
        var (game, world, s) = await Open();
        using var _ = game;
        var w = world.World;
        int mine = Tree(s), cut = Tree(s, 1), taken = Tree(s, 2);
        int mineId = s.Scene.Things[mine].Id, cutId = s.Scene.Things[cut].Id, takenId = s.Scene.Things[taken].Id;
        s.Delete([mine]);
        Assert.True((await world.ApplyLive()).Done);
        // Deleted and undone (on the redo list); then removed in the game.
        s.Delete([cut]);
        s.Undo();
        // Deleted in the editor, not applied; then removed in the game.
        s.Delete([taken]);
        var objects = AsRead(w, (0, 0)).Where(o => (o.Item1, o.Item2) != Zdo(w, mineId) && (o.Item1, o.Item2) != Zdo(w, cutId) && (o.Item1, o.Item2) != Zdo(w, takenId)).ToList();
        game.Zones[(0, 0)] = (1, objects);
        var f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { cutId, takenId }.Order(), f.Merged.Vanished.Order());
        s.TakeGameChanges(f);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        Assert.True(s.Scene.Things[cut].Gone);
        // Undo (bring the taken one back) then redo: nothing to bring back, nothing to delete.
        s.Undo();
        Assert.True(s.Scene.Things[taken].Gone);
        Assert.Equal((0, 0, 0, 0), world.Pending);
        s.Redo();
        Assert.Equal((0, 0, 0, 0), world.Pending);
        // The editor's own applied deletion: undo brings it back (to apply).
        s.Undo();
        s.Undo();
        Assert.False(s.Scene.Things[mine].Gone);
    }

    // An applied delete undone and applied again: the game has the object back under a new ZDOID, which
    // is followed as that object's (it vanished at the next look before).
    [Fact]
    public async Task AnAppliedDeleteUndoneIsFollowedUnderItsNewId()
    {
        var (game, world, s, tree, id) = await OpenWithTree();
        using var _ = game;
        var w = world.World;
        var old = Zdo(w, id);
        s.Delete([tree]);
        Assert.True((await world.ApplyLive()).Done);
        s.Undo();
        Assert.True((await world.ApplyLive()).Done);
        // The stand-in game gives new objects the ZDOIDs 77:1000 on.
        Assert.Equal((77L, 1000u), Zdo(w, id));
        var objects = AsRead(w, (0, 0)).Where(o => (o.Item1, o.Item2) != old).ToList();
        game.Zones[(0, 0)] = (1, objects);
        var f = await Follow(world, (0, 0));
        Assert.True(f == null || !f.Any);
        Assert.False(s.Scene.Things[tree].Gone);
        // Then a player cuts it: it goes.
        game.Zones[(0, 0)] = (2, objects.Where(o => (o.Item1, o.Item2) != (77L, 1000u)).ToList());
        f = (await Follow(world, (0, 0)))!;
        Assert.Equal(new[] { id }, f.Merged.Vanished);
    }
}
