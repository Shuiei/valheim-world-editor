using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The history kept on disk (HistoryFile): written at each save, back when the world is opened again
// (a later run of the editor), its steps undone as before: the ground back, a deleted tree back with its
// data, a placed one taken away. Steps from then are marked, and asked about when the world was saved
// again since. On a copy of the test world; the data folder is the tests'.
[Collection("World files")]
public class HistoryFileTests
{
	private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	// Raises the ground, deletes a tree, places a rock; saves; the world is left (the editor closed).
	private static (float Was, WorldScene.Thing Tree, Vector3 Rock) EditAndSave(string dir)
	{
		var scene = WorldScene.Load(dir, 0, 0, 1);
		var s = scene.Session!;
		float was = H(s, 32, 32);
		int tree = s.Scene.Things.FindIndex(t => !t.Piece);
		var treeThing = s.Scene.Things[tree];
		s.Shape(32, 32, Formula.Compile("3", new string[0]), 4, 0, "Raise");
		s.Delete(new[] { tree });
		var rock = new Vector3(5, H(s, 37, 37), 5);
		s.Commit("Place", null, Array.Empty<int>(), new[] { (new TerrainEditor.Editing.NewObject(0, StableHash.Of("Rock_4"), rock, Vector3.Zero, 0f), false) });
		Assert.True(s.Save().Saved);
		Assert.True(File.Exists(HistoryFile.PathOf(HistoryFile.KeyOf(scene.Owner!))));
		scene.Owner!.Dispose();
		return (was, treeThing, rock);
	}

	[AvaloniaFact]
	public void TheHistoryComesBackWhenTheWorldIsOpenedAgain()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var (was, tree, rock) = EditAndSave(dir);
			// Opened again (a new run): the three steps, from an earlier session, the world as left.
			var scene = WorldScene.Load(dir, 0, 0, 1);
			var s = scene.Session!;
			Assert.Equal(new[] { "Raise", "Deleted", "Place" }, s.UndoList.Select(c => c.Label.Split(' ')[0]).ToArray());
			Assert.All(s.UndoList, c => Assert.True(c.Earlier && c.Applied));
			Assert.False(scene.Owner!.Restored!.Changed);
			Assert.False(scene.Owner.EarlierMayDiffer);
			Assert.Equal((0, 0, 0, 0), s.Pending);
			// The rock is the world's own now; undoing its placing takes it away.
			int placed = s.Scene.Things.FindIndex(t => t.Prefab == StableHash.Of("Rock_4") && Vector3.Distance(t.Position, rock) < 0.01f);
			Assert.True(placed >= 0 && s.Scene.Things[placed].Id >= 0);
			s.Undo();
			Assert.True(s.Scene.Things[placed].Gone);
			// The tree back, then the ground.
			s.Undo();
			Assert.Contains(s.Scene.Things, t => !t.Gone && t.Prefab == tree.Prefab && Vector3.Distance(t.Position, tree.Position) < 0.01f);
			s.Undo();
			Assert.Equal(was, H(s, 32, 32), 2);
			Assert.False(s.CanUndo);
			Assert.True(s.Save().Saved);
			var saved = WorldSave.Load(dir);
			Assert.Contains(saved.Objects, o => o.Prefab == tree.Prefab && Vector3.Distance(o.Position, tree.Position) < 0.01f);
			Assert.DoesNotContain(saved.Objects, o => o.Prefab == StableHash.Of("Rock_4") && Vector3.Distance(o.Position, rock) < 0.01f);
			// The undone steps are kept too: a later run can redo them.
			scene.Owner.Dispose();
			var again = WorldScene.Load(dir, 0, 0, 1).Session!;
			Assert.Empty(again.UndoList);
			Assert.Equal(3, again.RedoList.Count);
			again.Redo();
			Assert.Equal(was + 3, H(again, 32, 32), 1);
			again.Scene.Owner!.Dispose();
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public async Task AWorldSavedAgainSinceAsksOnceBeforeUndoing()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var (was, _, _) = EditAndSave(dir);
			// The game saved the world since (its latest save is not the editor's any more).
			string latest = Directory.GetFiles(dir, "_main.*.chunks").OrderBy(f => f).Last();
			File.SetLastWriteTimeUtc(latest, DateTime.UtcNow.AddMinutes(5));
			var scene = WorldScene.Load(dir, 0, 0, 1);
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			w.View.Show(scene, null);
			w.Edit(scene.Session!);
			Assert.True(scene.Owner!.EarlierMayDiffer);
			var asked = new List<string>();
			bool answer = false;
			w.Ask = (title, text, yes, no) => { asked.Add(text); return Task.FromResult(answer); };
			await w.Undo();
			Assert.Single(asked);
			Assert.Contains("earlier session", asked[0]);
			Assert.Equal(3, scene.Session!.UndoList.Count);
			answer = true;
			await w.Undo();
			await w.Undo();
			await w.Undo();
			Assert.Equal(2, asked.Count);
			Assert.Equal(was, H(scene.Session, 32, 32), 2);
			scene.Owner.Dispose();
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public void ANewStepAfterAReopenIsNotFromAnEarlierSession()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			EditAndSave(dir);
			var scene = WorldScene.Load(dir, 0, 0, 1);
			var s = scene.Session!;
			s.Shape(20, 20, Formula.Compile("2", new string[0]), 3, 0, "Raise again");
			Assert.False(s.UndoList[^1].Earlier);
			Assert.Equal(4, s.UndoList.Count);
			// Not saved: the next run has the three saved steps only.
			scene.Owner!.Dispose();
			Assert.Equal(3, WorldScene.Load(dir, 0, 0, 1).Session!.UndoList.Count);
		}
		finally
		{
			Done(dir);
		}
	}
}
