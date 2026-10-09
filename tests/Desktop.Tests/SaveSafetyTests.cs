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
}
