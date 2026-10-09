using System.Numerics;
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

	[Fact]
	public async Task ObjectsRemovedAndMadeInTheGameComeIn()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var objects = AsRead(w, (0, 0));
		int tree = s.Scene.Things.FindIndex(t => !t.Piece && t.Id >= 0 && w.ObjectRefs[t.Id].Zone == (0, 0));
		Assert.True(tree >= 0);
		int treeId = s.Scene.Things[tree].Id;
		// The game: that tree cut down, and a new one (a copy, under a new ZDOID).
		var bytes = w.ObjectBytes(treeId);
		objects.RemoveAll(o => o.Item1 == w.ObjectRefs[treeId].LiveId.User && o.Item2 == w.ObjectRefs[treeId].LiveId.Id);
		objects.Add((99, 1, bytes));
		game.Zones[(0, 0)] = (1, objects);
		var f = await world.FollowGame(new[] { (0, 0) });
		Assert.NotNull(f);
		Assert.Equal(new[] { treeId }, f.Merged.Vanished);
		int added = Assert.Single(f.Merged.Added);
		Assert.True(s.TakeGameChanges(f.Merged, f.Ground));
		Assert.True(s.Scene.Things[tree].Gone);
		Assert.Contains(s.Scene.Things, t => t.Id == added && !t.Gone);
		Assert.Equal((99L, 1u), w.ObjectRefs[added].LiveId);
		Assert.Equal(bytes, w.ObjectBytes(added));
		// Nothing pending: what the game did is not the editor's to apply.
		Assert.Equal((0, 0, 0, 0), world.Pending);
		// Asked again with no change: the zone is not read again.
		int reads = game.ZoneReads;
		Assert.Null(await world.FollowGame(new[] { (0, 0) }));
		Assert.Equal(reads, game.ZoneReads);
	}

	// An object the editor deleted stays deleted (undo brings it back) whatever the game did; one the
	// game removed is not brought back by undo.
	[Fact]
	public async Task TheEditorsDeletionsStandAndUndoDoesNotRaiseTheGamesRemovals()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var trees = s.Scene.Things.Select((t, i) => (t, i)).Where(x => !x.t.Piece && x.t.Id >= 0 && w.ObjectRefs[x.t.Id].Zone == (0, 0)).Take(2).ToList();
		Assert.Equal(2, trees.Count);
		var (mine, gameCuts) = (trees[0], trees[1]);
		s.Delete(new[] { mine.i });
		s.Delete(new[] { gameCuts.i });
		var objects = AsRead(w, (0, 0)).Where(o => o.Item2 != w.ObjectRefs[mine.t.Id].LiveId.Id && o.Item2 != w.ObjectRefs[gameCuts.t.Id].LiveId.Id).ToList();
		game.Zones[(0, 0)] = (1, objects);
		var f = (await world.FollowGame(new[] { (0, 0) }))!;
		// Both were deleted in the editor: kept as they are.
		Assert.Empty(f.Merged.Vanished);
		s.Undo();
		Assert.False(s.Scene.Things[gameCuts.i].Gone);
		// Now the game's removal of the one brought back is taken in, and undoing it again does nothing.
		game.Zones[(0, 0)] = (2, objects);
		f = (await world.FollowGame(new[] { (0, 0) }))!;
		Assert.Equal(new[] { gameCuts.t.Id }, f.Merged.Vanished);
		s.TakeGameChanges(f.Merged, f.Ground);
		Assert.True(s.Scene.Things[gameCuts.i].Gone);
		s.Redo();
		s.Undo();
		Assert.True(s.Scene.Things[gameCuts.i].Gone);
	}

	// Ground dug in the game comes in where the editor has no changes of its own; where it has, they
	// are kept and the zone is marked (applying asks first).
	[Fact]
	public async Task GroundChangedInTheGameComesInOrIsMarked()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var tz = w.TerrainZones.First(z => z.ZoneX == 0 && z.ZoneZ == 0);
		int compiler = Enumerable.Range(0, w.ObjectRefs.Count).First(id => w.ObjectRefs[id].IsTerrain && w.ObjectRefs[id].Start == tz.Source!.Start && w.ObjectRefs[id].File == tz.Source.File);
		var dug = world.Edits.Get(0, 0)!;
		dug.Modified[2000] = true;
		dug.Level[2000] = -3;
		dug.Smooth[2000] = 0;
		var z = ZdoData.Parse(w.ObjectBytes(compiler));
		z.Set("bytes", TCData, Convert.ToBase64String(WorldWriter.EncodeTerrain(dug)));
		var objects = AsRead(w, (0, 0));
		int at = objects.FindIndex(o => o.Item1 == w.ObjectRefs[compiler].LiveId.User && o.Item2 == w.ObjectRefs[compiler].LiveId.Id);
		objects[at] = (objects[at].Item1, objects[at].Item2, z.Serialize());
		game.Zones[(0, 0)] = (1, objects);
		var f = (await world.FollowGame(new[] { (0, 0) }))!;
		Assert.Equal(new[] { (0, 0) }, f.Ground);
		Assert.Equal(-3, world.Edits.Get(0, 0)!.Level[2000], 3);
		Assert.True(s.TakeGameChanges(f.Merged, f.Ground));
		Assert.Equal(-3, s.Ground.Level[(2000 / 65) * s.Ground.W + 2000 % 65], 3);
		Assert.Empty(world.Edits.GameChanged);
		// Now the editor changes that zone, and the game digs it again: the editor's changes stay.
		s.Shape(10, 10, Two, 3, 0, "raise");
		dug.Level[2000] = -5;
		z.Set("bytes", TCData, Convert.ToBase64String(WorldWriter.EncodeTerrain(dug)));
		objects = AsRead(w, (0, 0));
		var terrain = w.TerrainZones.First(t => t.ZoneX == 0 && t.ZoneZ == 0).Source!;
		at = objects.FindIndex(o => Enumerable.Range(0, w.ObjectRefs.Count).Any(id => w.ObjectRefs[id].IsTerrain && w.ObjectRefs[id].File == terrain.File && w.ObjectRefs[id].Start == terrain.Start && w.ObjectRefs[id].LiveId == (o.Item1, o.Item2)));
		objects[at] = (objects[at].Item1, objects[at].Item2, z.Serialize());
		game.Zones[(0, 0)] = (2, objects);
		f = (await world.FollowGame(new[] { (0, 0) }))!;
		Assert.Equal(new[] { (0, 0) }, f.Kept);
		Assert.Equal(new[] { (0, 0) }, world.Edits.GameChanged);
		Assert.NotEqual(-5, world.Edits.Get(0, 0)!.Level[2000], 3);
		// Applied (after asking): no longer marked.
		Assert.True((await world.ApplyLive()).Done);
		Assert.Empty(world.Edits.GameChanged);
	}

	// What the editor made itself and the game now has is not taken for something new.
	[Fact]
	public async Task TheEditorsOwnObjectsAreNotTakenTwice()
	{
		var (game, world, s) = await Open();
		using var _ = game;
		var w = world.World;
		var tree = s.Scene.Things.First(t => !t.Piece && t.Id >= 0 && w.ObjectRefs[t.Id].Zone == (0, 0));
		s.Commit("Place", null, Array.Empty<int>(), new[] { (new NewObject(0, tree.Prefab, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f), false) });
		Assert.True((await world.ApplyLive()).Done);
		// The stand-in game gives its new objects the ZDOIDs 77:1000 on.
		var objects = AsRead(w, (0, 0));
		objects.Add((77, 1000, w.ObjectBytes(tree.Id)));
		game.Zones[(0, 0)] = (1, objects);
		var f = (await world.FollowGame(new[] { (0, 0) }))!;
		Assert.Empty(f.Merged.Added);
		Assert.Empty(f.Merged.Vanished);
	}
}
