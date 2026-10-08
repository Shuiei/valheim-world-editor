using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// One open world shared by every area, and the pages: start, map, editor.
[Collection("World files")]
public class PagesTests
{
	private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

	[Fact]
	public void AreasOfOneWorldShareTheirPendingEdits()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var world = WorldSession.Open(dir);
			var a = WorldScene.Load(world, 0, 0, 1);
			var b = WorldScene.Load(world, 1, 0, 1);
			Assert.Same(world.Edits, a.Session!.Edits);
			// A tree placed in the first area, and one in the second: different ids.
			var tree = (new TerrainEditor.Editing.NewObject(0, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 1), false);
			var i = a.Session.Commit("x", null, Array.Empty<int>(), new[] { tree });
			var j = b.Session!.Commit("y", null, Array.Empty<int>(), new[] { tree with { Item1 = tree.Item1 with { Position = new Vector3(70, 30, 10) } } });
			Assert.NotEqual(a.Things[i[0]].Id, b.Things[j[0]].Id);
			Assert.Equal(2, world.Pending.Added);
			// Opened again, the first area shows the tree it got.
			var again = WorldScene.Load(world, 0, 0, 1);
			Assert.Contains(again.Things, t => t.Id == a.Things[i[0]].Id);
			// Saved from either area: the whole world is written.
			Assert.True(b.Session.Save().Saved);
			Assert.Equal((0, 0, 0, 0), world.Pending);
			var fresh = WorldSave.Load(dir);
			Assert.Equal(2, fresh.Objects.Count(o => o.Prefab == StableHash.Of("Beech1") && (Vector3.Distance(o.Position, new Vector3(10, 30, 10)) < 0.01f || Vector3.Distance(o.Position, new Vector3(70, 30, 10)) < 0.01f)));
		}
		finally
		{
			Done(dir);
		}
	}

	[Fact]
	public void DiscardReadsTheWorldAgain()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var world = WorldSession.Open(dir);
			var a = WorldScene.Load(world, 0, 0, 1);
			a.Session!.Shape(32, 32, Formula.Compile("2", new string[0]), 4, 0, "x");
			Assert.Equal(1, world.Pending.Zones);
			world.Discard();
			Assert.Equal((0, 0, 0, 0), world.Pending);
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public async Task StartMapEditorAndBack()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			w.ShowStart();
			var start = w.StartPage!;
			start.SetMode("offline");
			Assert.True(start.OfflinePanel.IsVisible);
			// A folder that is not a world says why.
			start.OpenFolder(Path.GetTempPath());
			Assert.NotEqual("", start.PathError.Text);
			// The world: its map.
			await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
			Assert.NotNull(w.World);
			var map = w.MapPage!;
			Assert.Equal("Everything is saved.", map.Pending.Text);
			Assert.False(map.SaveButton.IsEnabled);
			map.Pick(0, 0);
			Assert.Equal((0, 0), map.Spot);
			Assert.StartsWith("Zone 0, 0", map.PickTitle.Text);
			Assert.Equal((0, 0, 5), map.Map.Chosen);
			// Edit in 3D, change the ground, back to the map: it is pending there.
			map.SizeBox.SelectedIndex = 0;
			await w.EditArea(0, 0, map.Size);
			Assert.True(w.MapButton.IsVisible);
			w.Session!.Shape(96, 96, Formula.Compile("2", new string[0]), 4, 0, "x");
			w.ShowMap();
			Assert.StartsWith("Not saved yet: 1 zone of ground", map.Pending.Text);
			Assert.True(map.SaveButton.IsEnabled);
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public void RightPanelsOneAtATime()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		// The View panel at first; Help and History take its place, one at a time.
		Assert.False(w.HelpCard.IsVisible);
		w.ShowRight(w.HelpCard);
		Assert.True(w.HelpCard.IsVisible);
		Assert.True(w.HelpButton.Classes.Contains("on"));
		Assert.False(w.ViewButton.Classes.Contains("on"));
		w.ShowRight(w.History.Card);
		Assert.False(w.HelpCard.IsVisible);
		Assert.True(w.History.Card.IsVisible);
		w.ShowRight(null);
		Assert.False(w.History.Card.IsVisible);
	}

	[AvaloniaFact]
	public void EveryIconParses()
	{
		foreach (var name in Icons.Paths.Keys)
		{
			Assert.NotNull(Icons.Make(name));
		}
	}
}
