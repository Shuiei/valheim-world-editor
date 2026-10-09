using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// What a save must never throw away or break: the game's own saves, made while the world is open.
[Collection("World files")]
public class SaveSafetyTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	// The game saving the world: its next save number, made from the latest save (same files).
	private static int GameSaves(string dir)
	{
		int n = WorldWriter.LatestNumber(dir);
		foreach (string ext in new[] { ".chunks", ".db2", ".fwl2", ".ok" })
		{
			File.Copy(Path.Combine(dir, $"_main.{n}{ext}"), Path.Combine(dir, $"_main.{n + 1}{ext}"));
		}
		return n + 1;
	}

	private static Dictionary<string, DateTime> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => f, File.GetLastWriteTimeUtc);

	// Save, play test in the game, save again: the second save is refused and the game's save stays,
	// with every file it lists; the edits stay pending.
	[Fact]
	public void ASaveAfterTheGameSavedIsRefused()
	{
		using var w = new TempWorld();
		using var world = WorldSession.Open(w.Dir);
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		s.Shape(32, 32, Two, 3, 0, "raise");
		var first = s.Save();
		Assert.True(first.Saved, first.Message);
		int game = GameSaves(w.Dir);
		var before = Files(w.Dir);
		s.Shape(40, 40, Two, 3, 0, "raise");
		var second = s.Save();
		Assert.False(second.Saved);
		Assert.Contains($"save #{game}", second.Message);
		Assert.Equal(before, Files(w.Dir));
		Assert.True(world.Pending.Zones > 0);
		// The game's save still reads, with all its chunk files.
		Assert.Equal(game, WorldSave.Load(w.Dir).SaveNumber);
	}

	// The writer itself refuses when told which save it knows of.
	[Fact]
	public void TheWriterRefusesOverANewerSave()
	{
		using var w = new TempWorld();
		WorldSave save = w.Load();
		int game = GameSaves(w.Dir);
		var t = save.TerrainZones[0];
		var edit = new TerrainEditor.Editing.ZoneEdit(t.ZoneX, t.ZoneZ);
		edit.Modified[100] = true;
		edit.Level[100] = 1f;
		var r = WorldWriter.Save(save, new[] { edit }, options: new WorldWriter.Options(Latest: save.SaveNumber));
		Assert.False(r.Saved);
		Assert.Contains($"save #{game}", r.Message);
		Assert.Equal(game, WorldWriter.LatestNumber(w.Dir));
	}

	// Saved from the map (no area open): the steps are saved, so Discard in the area opened next
	// undoes only what came after, never the saved ones.
	[Fact]
	public void StepsSavedFromTheMapAreNotDiscardedLater()
	{
		using var w = new TempWorld();
		using var world = WorldSession.Open(w.Dir);
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		s.Shape(20, 20, Two, 3, 0, "raise");
		s.Shape(30, 30, Two, 3, 0, "raise");
		s.Shape(40, 40, Two, 3, 0, "raise");
		// To the map (as MainWindow.ShowMap does), saved there.
		world.History = s.Export();
		world.Area = null;
		var o = world.Save();
		Assert.True(o.Done, o.Message);
		var again = WorldScene.Load(world, 0, 0, 1).Session!;
		Assert.Equal(0, again.UnappliedSteps);
		again.Shape(50, 50, Two, 3, 0, "raise");
		Assert.Equal(1, again.UnappliedSteps);
		Assert.Equal(1, again.UndoUnapplied());
		Assert.Equal(3, again.UndoList.Count);
	}

	// Live: a stroke made while the game answers an Apply is not marked as applied; it stays pending
	// and goes with the next Apply.
	[Fact]
	public async Task AStrokeDuringALiveApplyStaysPending()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		s.Shape(32, 32, Two, 3, 0, "raise");
		game.HoldTerrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var applying = s.ApplyLive();
		await game.TerrainArrived.Task;
		s.Shape(34, 34, Two, 3, 0, "raise");
		game.HoldTerrain.SetResult();
		var o = await applying;
		Assert.True(o.Done, o.Message);
		Assert.Equal(1, world.Pending.Zones);
		Assert.Equal(1, s.UnappliedSteps);
		game.HoldTerrain = null;
		int sent = game.Terrain.Count;
		o = await s.ApplyLive();
		Assert.True(o.Done, o.Message);
		Assert.Equal(sent + 1, game.Terrain.Count);
		Assert.Equal(0, world.Pending.Zones);
		Assert.Equal(0, s.UnappliedSteps);
	}

	// A zone with two terrain objects (the game makes them now and then): saving its ground no longer
	// fails, and both get it.
	[Fact]
	public void AZoneWithTwoTerrainObjectsSaves()
	{
		using var w = new TempWorld();
		WorldSave save = w.Load();
		var t = save.TerrainZones[0];
		int id = save.ObjectRefs.FindIndex(o => o.IsTerrain && o.File == t.Source!.File && o.Start == t.Source.Start);
		Assert.True(id >= 0);
		var copy = new TerrainEditor.Editing.NewObject(-1, t.Prefab, t.Center, System.Numerics.Vector3.Zero, 0f, Raw: save.ObjectBytes(id));
		var r = WorldWriter.Save(save, Array.Empty<TerrainEditor.Editing.ZoneEdit>(), added: new[] { copy });
		Assert.True(r.Saved, r.Message);
		save = w.Load();
		Assert.Equal(2, save.TerrainZones.Count(z => z.ZoneX == t.ZoneX && z.ZoneZ == t.ZoneZ));

		var edit = new TerrainEditor.Editing.EditStore(save).Get(t.ZoneX, t.ZoneZ)!;
		edit.Modified[200] = true;
		edit.Level[200] = 3f;
		edit.Smooth[200] = 0f;
		r = WorldWriter.Save(save, new[] { edit });
		Assert.True(r.Saved, r.Message);
		var both = w.Load().TerrainZones.Where(z => z.ZoneX == t.ZoneX && z.ZoneZ == t.ZoneZ).ToList();
		Assert.Equal(2, both.Count);
		Assert.All(both, z => Assert.Equal(3f, z.LevelDelta[200], 4));
	}

	// A save cut short (no commit marker, .ok) is not the world's save: it opens from the last complete
	// one, which leaving the world keeps, and a later save goes past it.
	[Fact]
	public void ASaveWithoutItsCommitMarkerIsNotUsed()
	{
		using var w = new TempWorld();
		int good = w.Load().SaveNumber;
		int cut = GameSaves(w.Dir);
		File.Delete(Path.Combine(w.Dir, $"_main.{cut}.ok"));
		File.WriteAllText(Path.Combine(w.Dir, "_main.backup.zip"), "mine");
		Assert.Equal(good, WorldSave.Load(w.Dir).SaveNumber);
		WorldWriter.Prune(w.Dir);
		Assert.True(File.Exists(Path.Combine(w.Dir, $"_main.{good}.ok")));
		Assert.True(File.Exists(Path.Combine(w.Dir, $"_main.{cut}.chunks")));
		Assert.True(File.Exists(Path.Combine(w.Dir, "_main.backup.zip")));
		Assert.Equal(good, WorldSave.Load(w.Dir).SaveNumber);
		// Saving works (the cut save does not count as newer) and takes the next free number.
		using var world = WorldSession.Open(w.Dir);
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		s.Shape(32, 32, Two, 3, 0, "raise");
		var o = s.Save();
		Assert.True(o.Saved, o.Message);
		Assert.Equal(cut + 1, WorldSave.Load(w.Dir).SaveNumber);
	}

	// An object put far outside the world (beyond ±16 km) is skipped and said; the rest is saved (the
	// whole save failed).
	[Fact]
	public void AnObjectOutsideTheWorldIsSkipped()
	{
		using var w = new TempWorld();
		WorldSave save = w.Load();
		var (_, prefab, near, _, _) = save.Objects.First(o => MathF.Abs(o.Position.X) < 200 && MathF.Abs(o.Position.Z) < 200);
		var added = new[]
		{
			new TerrainEditor.Editing.NewObject(-1, prefab, near + new System.Numerics.Vector3(1, 0, 1), System.Numerics.Vector3.Zero, 0f),
			new TerrainEditor.Editing.NewObject(-2, prefab, new System.Numerics.Vector3(20000, 30, 0), System.Numerics.Vector3.Zero, 0f),
		};
		var r = WorldWriter.Save(save, Array.Empty<TerrainEditor.Editing.ZoneEdit>(), added: added);
		Assert.True(r.Saved, r.Message + " | " + string.Join(" | ", r.Skipped));
		Assert.Equal(1, r.ObjectsAdded);
		Assert.Contains(r.Skipped, s => s.Contains("outside the world"));
	}
}
