using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Live: the editor follows the game in the zones it shows. A stand-in game says what each zone holds
// now; what changed there (objects made, removed or changed, ground dug) comes into the open world
// and area without undo steps, and ground the editor changed itself is kept and asked about on apply.
public class LiveFollowTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);
	private static readonly int TCData = StableHash.Of("TCData");

	// What the game has in a zone, as the world was read (its objects' ZDOIDs and bytes).
	private static List<(long, uint, byte[])> AsRead(WorldSave w, (int X, int Z) zone) =>
		Enumerable.Range(0, w.ObjectRefs.Count).Where(id => !w.Vanished.Contains(id) && w.ObjectRefs[id].Zone == zone)
			.Select(id => (w.ObjectRefs[id].LiveId.User, w.ObjectRefs[id].LiveId.Id, w.ObjectBytes(id))).ToList();

	private static async Task<(FakeGame Game, WorldSession World, EditSession Area)> Open()
	{
		var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
		var area = WorldScene.Load(world, 0, 0, 1).Session!;
		return (game, world, area);
	}

	// One look at the game: read and merged (null: nothing changed).
	private static async Task<WorldSession.Followed?> Follow(WorldSession w, params (int, int)[] zones) =>
		await w.FetchGame(zones) is { } f ? w.MergeGame(f) : null;

	private static int Tree(EditSession s, int skip = 0) =>
		s.Scene.Things.Select((t, i) => (t, i)).Where(x => !x.t.Piece && x.t.Id >= 0 && s.Scene.World.ObjectRefs[x.t.Id].Zone == (0, 0)).Skip(skip).First().i;

	private static (long, uint) Zdo(WorldSave w, int id) => w.ObjectRefs[id].LiveId;

	[Fact]
	public async Task ObjectsRemovedAndMadeInTheGameComeIn()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		int tree = Tree(s);
		int treeId = s.Scene.Things[tree].Id;
		var bytes = w.ObjectBytes(treeId);
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
		Assert.DoesNotContain(TerrainEditor.App.WorldSearch.Search(w, world.Edits, TerrainEditor.Terrain.PrefabCatalog.DisplayName(w.ObjectRefs[treeId].Prefab)!, "kinds").Hits, h => h.Id == treeId);
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
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		int tree = Tree(s);
		int id = s.Scene.Things[tree].Id;
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
		s.Delete(new[] { mine });
		Assert.True((await world.ApplyLive()).Done);
		// Deleted and undone (on the redo list); then removed in the game.
		s.Delete(new[] { cut });
		s.Undo();
		// Deleted in the editor, not applied; then removed in the game.
		s.Delete(new[] { taken });
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
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		int tree = Tree(s);
		int id = s.Scene.Things[tree].Id;
		var old = Zdo(w, id);
		s.Delete(new[] { tree });
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

	// The editor's own new objects: not taken twice when the game has them, and gone when the game no
	// longer does (a player picked it), with nothing left to apply.
	[Fact]
	public async Task TheEditorsOwnObjectsAreFollowed()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var tree = s.Scene.Things[Tree(s)];
		var placed = s.Commit("Place", null, Array.Empty<int>(), new[] { (new NewObject(0, tree.Prefab, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f), false) });
		Assert.True((await world.ApplyLive()).Done);
		var objects = AsRead(w, (0, 0));
		objects.Add((77, 1000, w.ObjectBytes(tree.Id)));
		game.Zones[(0, 0)] = (1, objects);
		var f = await Follow(world, (0, 0));
		Assert.True(f == null || !f.Any);
		game.Zones[(0, 0)] = (2, AsRead(w, (0, 0)));
		f = (await Follow(world, (0, 0)))!;
		Assert.Equal(new[] { s.Scene.Things[placed[0]].Id }, f.Ours);
		s.TakeGameChanges(f);
		Assert.True(s.Scene.Things[placed[0]].Gone);
		Assert.Equal((0, 0, 0, 0), world.Pending);
	}

	// The game's ground for zone 0, 0: its terrain object's bytes with this data.
	private static List<(long, uint, byte[])> Dug(WorldSave w, ZoneEdit ground)
	{
		var src = w.TerrainZones.First(t => t.ZoneX == 0 && t.ZoneZ == 0).Source!;
		int compiler = Enumerable.Range(0, w.ObjectRefs.Count).First(id => w.ObjectRefs[id].IsTerrain && w.ObjectRefs[id].File == src.File && w.ObjectRefs[id].Start == src.Start);
		var z = ZdoData.Parse(w.ObjectBytes(compiler));
		z.Set("bytes", TCData, Convert.ToBase64String(WorldWriter.EncodeTerrain(ground)));
		var objects = AsRead(w, (0, 0));
		int at = objects.FindIndex(o => (o.Item1, o.Item2) == Zdo(w, compiler));
		objects[at] = (objects[at].Item1, objects[at].Item2, z.Serialize());
		return objects;
	}

	// Ground dug in the game comes in where the editor has no changes of its own; where it has, they are
	// kept (applying asks first), and undoing them brings in the game's ground.
	[Fact]
	public async Task GroundChangedInTheGameComesInOrWaits()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var dug = world.Edits.Get(0, 0)!;
		dug.Modified[2000] = true;
		dug.Level[2000] = -3;
		dug.Smooth[2000] = 0;
		game.Zones[(0, 0)] = (1, Dug(w, dug));
		var f = (await Follow(world, (0, 0)))!;
		Assert.Equal(new[] { (0, 0) }, f.Ground);
		Assert.Equal(-3, world.Edits.Get(0, 0)!.Level[2000], 3);
		Assert.True(s.TakeGameChanges(f));
		int p = (2000 / 65) * s.Ground.W + 2000 % 65;
		Assert.Equal(-3, s.Ground.Level[p], 3);
		Assert.Empty(world.Edits.GameChanged);
		// The editor changes that zone, and the game digs it again: the editor's changes stay.
		s.Shape(10, 10, Two, 3, 0, "raise");
		dug.Level[2000] = -5;
		game.Zones[(0, 0)] = (2, Dug(w, dug));
		f = (await Follow(world, (0, 0)))!;
		Assert.Equal(new[] { (0, 0) }, f.Kept);
		Assert.Equal(new[] { (0, 0) }, world.Edits.PendingOverGame());
		Assert.Equal(-3, world.Edits.Get(0, 0)!.Level[2000], 3);
		// Applying asks; no: not applied.
		var asked = new List<IReadOnlyList<(int X, int Z)>>();
		int sent = game.Terrain.Count;
		var o = await world.ApplyLive(over => { asked.Add(over); return Task.FromResult(false); });
		Assert.True(o.Declined);
		Assert.Single(asked);
		Assert.Equal(sent, game.Terrain.Count);
		// Undone: the zone takes the game's ground.
		s.Undo();
		Assert.Equal(new[] { (0, 0) }, world.TakeWaitingGround());
		Assert.Equal(-5, world.Edits.Get(0, 0)!.Level[2000], 3);
		Assert.Empty(world.Edits.GameChanged);
		Assert.Equal((0, 0, 0, 0), world.Pending);
		// Changed again and applied (yes): the mark goes.
		s.Shape(10, 10, Two, 3, 0, "raise");
		dug.Level[2000] = -7;
		game.Zones[(0, 0)] = (3, Dug(w, dug));
		await Follow(world, (0, 0));
		Assert.NotEmpty(world.Edits.PendingOverGame());
		Assert.True((await world.ApplyLive(_ => Task.FromResult(true))).Done);
		Assert.Empty(world.Edits.GameChanged);
		// The game then sends back what was applied: nothing new.
		game.Zones[(0, 0)] = (4, Dug(w, world.Edits.Get(0, 0)!));
		f = (await Follow(world, (0, 0)))!;
		Assert.Empty(f.Ground);
		Assert.Empty(f.Kept);
	}

	// In the window: what the game changed is said in the status bar, and applying over ground the game
	// changed asks first (not applied when the answer is no).
	[AvaloniaFact]
	public async Task TheWindowSaysWhatCameAndAsksBeforeApplyingOverIt()
	{
		using var game = new FakeGame();
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var asked = new List<string>();
		bool answer = false;
		w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
		await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
		await w.EditArea(0, 0, 1);
		var world = w.World!;
		var s = w.Session!;
		var wsave = world.World;
		int tree = Tree(s);
		var cut = Zdo(wsave, s.Scene.Things[tree].Id);
		game.Zones[(0, 0)] = (1, AsRead(wsave, (0, 0)).Where(o => (o.Item1, o.Item2) != cut).ToList());
		await w.FollowGame();
		Assert.True(s.Scene.Things[tree].Gone);
		Assert.StartsWith("From the game: 1 removed.", w.MessageText.Text);
		// Then ground: the editor raises zone 0, 0 while the game digs there.
		s.Shape(10, 10, Two, 3, 0, "raise");
		var dug = new EditStore(wsave).Get(0, 0)!;
		dug.Modified[2000] = true;
		dug.Level[2000] = -3;
		game.Zones[(0, 0)] = (2, Dug(wsave, dug));
		await w.FollowGame();
		Assert.Contains("where you have changes not applied", w.MessageText.Text);
		int sent = game.Terrain.Count;
		await w.ApplyNow();
		Assert.Equal(new[] { "Apply over the game's changes" }, asked);
		Assert.Equal(sent, game.Terrain.Count);
		answer = true;
		await w.ApplyNow();
		Assert.Equal(sent + 1, game.Terrain.Count);
		Assert.Empty(world.Edits.GameChanged);
		w.Close();
	}
}
