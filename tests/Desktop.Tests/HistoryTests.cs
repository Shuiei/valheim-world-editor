using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The history panel: back to a change, redo to one, and taking out one change only.
[Collection("World files")]
public class HistoryTests
{
	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	[Fact]
	public void BackToAndForwardTo()
	{
		var s = EditTests.Flat(2);
		s.Shape(30, 30, Two, 3, 0, "one");
		s.Shape(60, 60, Two, 3, 0, "two");
		s.Shape(90, 90, Two, 3, 0, "three");
		var first = s.UndoList[0];
		s.BackTo(first);
		Assert.Single(s.UndoList);
		Assert.Equal(30, H(s, 60, 60), 3);
		Assert.Equal(2, s.RedoList.Count);
		s.ForwardTo(s.RedoList[0]);
		Assert.Equal(3, s.UndoList.Count);
		Assert.Equal(32, H(s, 90, 90), 3);
	}

	[Fact]
	public void RemovingOneChangeKeepsTheOnesAfter()
	{
		var s = EditTests.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
		s.Shape(60, 60, Two, 3, 0, "raise");
		s.Delete(new[] { 0 });
		// Raised again on the same spot afterwards: that stays.
		s.Shape(60, 60, Two, 3, 0, "raise more");
		Assert.Equal(34, H(s, 60, 60), 3);
		Assert.Equal("Removed: raise", s.UndoList.Count is > 0 ? Remove(s, s.UndoList[0]) : "");
		Assert.Equal(32, H(s, 60, 60), 3);
		Assert.True(s.Scene.Things[0].Gone);
		Assert.True(s.UndoList[0].Removed);
		// The delete taken out: the tree is back.
		Remove(s, s.UndoList[1]);
		Assert.False(s.Scene.Things[0].Gone);
		Assert.Equal(0, s.Pending.Deleted);
		// A removal is undone like any change.
		s.Undo();
		Assert.True(s.Scene.Things[0].Gone);
		Assert.False(s.UndoList[1].Removed);
		Assert.Equal("That change cannot be removed.", s.RemoveChange(s.UndoList[0]));
	}

	private static string Remove(EditSession s, EditSession.Change c)
	{
		s.RemoveChange(c);
		return s.UndoLabel!;
	}

	[AvaloniaFact]
	public void ThePanelListsTheChanges()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		s.Shape(30, 30, Two, 3, 0, "one");
		s.Shape(60, 60, Two, 3, 0, "two");
		s.Undo();
		w.HistoryButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.True(w.History.Card.IsVisible);
		Assert.Equal(2, w.History.Rows.Children.Count);
		Assert.Equal("1 change(s)", w.History.Count.Text);
		// The undone one is on top, greyed, with Redo to here.
		var top = (Avalonia.Controls.Border)w.History.Rows.Children[0];
		Assert.Equal(0.5, top.Opacity);
		var buttons = top.GetVisualDescendants().OfType<Avalonia.Controls.Button>().ToList();
		Assert.Equal("Redo to here", buttons.Single().Content);
		buttons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(2, s.UndoList.Count);
	}

	[AvaloniaFact]
	public void TheSelectToolReplacesWithAnotherKind()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), new Vector3(0, 40, 0), 1, false));
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		int i = w.SelectPanel.ReplaceKinds.IndexOf(StableHash.Of("Oak1"));
		Assert.True(i >= 0);
		w.SelectPanel.ReplaceBox.SelectedIndex = i;
		w.SelectPanel.ReplaceButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.True(s.Scene.Things[0].Gone);
		var oak = s.Scene.Things[^1];
		Assert.Equal(StableHash.Of("Oak1"), oak.Prefab);
		Assert.Equal(40, oak.Rotation.Y);
		Assert.Empty(w.View.Selected);
	}

	// As used: a world, an area from the map, History open, then edits. The history follows the world
	// to the map and back, and into another area as far as it fits there.
	[AvaloniaFact]
	public async Task TheHistoryFollowsTheWorldFromAreaToArea()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
			await w.EditArea(0, 0, 3);
			w.ShowRight(w.History.Card);
			var s = w.Session!;
			// Ground in the middle zone, and an object deleted.
			s.Shape(96, 96, Two, 3, 0, "raise");
			int tree = s.Scene.Things.FindIndex(t => !t.Gone && !t.Piece);
			Assert.True(tree >= 0);
			int id = s.Scene.Things[tree].Id;
			s.Delete(new[] { tree });
			Dispatcher.UIThread.RunJobs();
			Assert.Equal("2 change(s)", w.History.Count.Text);
			// To the map and back to the same area: both are there, and undo brings the object back.
			w.ShowMap();
			await w.EditArea(0, 0, 3);
			w.ShowRight(w.History.Card);
			Dispatcher.UIThread.RunJobs();
			s = w.Session!;
			Assert.Equal(new[] { "raise", $"Deleted 1" }, s.UndoList.Select(c => c.Label));
			Assert.Equal("2 change(s)", w.History.Count.Text);
			s.Undo();
			var back = s.Scene.Things.Single(t => t.Id == id);
			Assert.False(back.Gone);
			Assert.DoesNotContain(id, w.World!.Edits.Deleted);
			Assert.Single(s.RedoList);
			// A bigger area around it: the raise fits (same world points), the redo too.
			w.ShowMap();
			await w.EditArea(0, 0, 5);
			s = w.Session!;
			Assert.Equal(new[] { "raise" }, s.UndoList.Select(c => c.Label));
			Assert.Single(s.RedoList);
			s.Undo();
			Assert.Equal(0, w.World.Pending.Zones);
			// Far away: nothing fits there.
			w.ShowMap();
			await w.EditArea(20, 20, 1);
			Assert.Empty(w.Session!.UndoList);
			Assert.Empty(w.Session.RedoList);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
		}
	}
}
