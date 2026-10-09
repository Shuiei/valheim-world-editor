using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The 3D editor's world controls, as the web editor had them: Discard (undoing the steps not saved or
// applied, or reading the world again for older ones) and Reload in the top bar, Auto apply, the
// players of a live world with Go to, the area moved by one zone or following the view, and the note
// about the area's locations.
[Collection("World files")]
public class ParityEditorTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public List<string> Asked { get; } = new();
		public Queue<bool> Answers { get; } = new();
		public int Saves;
		public FakeGame? Game { get; private set; }

		public Run()
		{
			W.Show();
			W.Tell = _ => Task.CompletedTask;
			W.ConfirmSave = _ => Task.FromResult(true);
			W.Ask = (title, text, _, _) => { Asked.Add(text); return Task.FromResult(Answers.Count > 0 && Answers.Dequeue()); };
			W.SaveSettings = _ => Saves++;
			W.Settings.AutoApply = W.Settings.AreaFollow = false;
		}

		public async Task Open(int x = 0, int z = 0, int size = 1)
		{
			await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
			await W.EditArea(x, z, size);
			Assert.NotNull(W.Session);
		}

		public async Task OpenLive(int x = 0, int z = 0, int size = 1)
		{
			Game = new FakeGame();
			var game = Game;
			await W.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
			W.MapPage?.Stop();
			await W.EditArea(x, z, size);
			Assert.True(W.Session!.IsLive);
		}

		public void Raise(float gx = 32, float gz = 32, string label = "raise") => W.Session!.Shape(gx, gz, Two, 3, 0, label);

		public void Dispose()
		{
			Game?.Dispose();
			try
			{
				Directory.Delete(Path.GetDirectoryName(Dir)!, true);
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

	// ---- Discard.

	[AvaloniaFact]
	public async Task DiscardWithNothingWaitingSaysSoWithoutAsking()
	{
		using var r = new Run();
		await r.Open();
		Assert.False(r.W.DiscardButton.IsEnabled);
		await r.W.DiscardHere();
		Assert.Empty(r.Asked);
		Assert.Equal("There is nothing to discard.", r.W.MessageText.Text);
	}

	[AvaloniaFact]
	public async Task DiscardUndoesTheUnsavedStepsHereAndKeepsTheView()
	{
		using var r = new Run();
		await r.Open();
		r.Raise(20, 20, "one");
		r.Raise(40, 40, "two");
		await LiveTests.Until(() => r.W.DiscardButton.IsEnabled);
		var cam = r.W.View.WorldCamera;
		// Asked no: nothing changes.
		r.Answers.Enqueue(false);
		await r.W.DiscardHere();
		Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)? The last 2 step(s) in History are undone.", Assert.Single(r.Asked));
		Assert.Equal(2, r.W.Session!.UndoList.Count);
		// Yes: both undone, nothing to redo, the area and the view stay.
		r.W.Session.Undo();
		r.W.Session.Redo();
		r.Answers.Enqueue(true);
		await r.W.DiscardHere();
		Assert.Empty(r.W.Session.UndoList);
		Assert.False(r.W.Session.CanRedo);
		Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
		Assert.Equal("Discarded. Everything else is kept.", r.W.MessageText.Text);
		Assert.False(r.W.DiscardButton.IsEnabled);
		Assert.Equal(cam, r.W.View.WorldCamera);
		Assert.Equal(2, r.Asked.Count);
	}

	[AvaloniaFact]
	public async Task ChangesWithNoStepHereNeedTheWorldReadAgain()
	{
		using var r = new Run();
		await r.Open();
		r.Raise();
		// Another area: the change of the first one has no step here.
		await r.W.EditArea(4, 0, 1);
		Assert.Equal(0, r.W.Session!.UnappliedSteps);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		// The first question (no steps to undo), then the second answered no: kept.
		r.Answers.Enqueue(true);
		r.Answers.Enqueue(false);
		await r.W.DiscardHere();
		Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)?", r.Asked[0]);
		Assert.Contains("can only be discarded by reading the world again", r.Asked[1]);
		Assert.Equal(1, r.W.World.Pending.Zones);
		// Yes: the world is read again, the same area opened, the view kept.
		var cam = r.W.View.WorldCamera;
		r.Answers.Enqueue(true);
		r.Answers.Enqueue(true);
		await r.W.DiscardHere();
		Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
		Assert.Equal(4, r.W.View.Scene!.X0);
		Assert.Equal("Discarded: the world was read again.", r.W.MessageText.Text);
		Assert.Equal(cam!.Value.World, r.W.View.WorldCamera!.Value.World);
	}

	[AvaloniaFact]
	public async Task LiveDiscardKeepsTheStepsAlreadyApplied()
	{
		using var r = new Run();
		await r.OpenLive();
		r.Raise(20, 20, "applied");
		await r.W.ApplyNow();
		r.Raise(40, 40, "not yet");
		Assert.Equal(1, r.W.Session!.UnappliedSteps);
		r.Answers.Enqueue(true);
		await r.W.DiscardHere();
		Assert.StartsWith("Discard the changes not applied yet (1 zone(s) of ground)? The last 1 step(s)", r.Asked[0]);
		Assert.Equal("applied", Assert.Single(r.W.Session.UndoList).Label);
		Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
	}

	[AvaloniaFact]
	public async Task DiscardButtonFollowsWhatWaits()
	{
		using var r = new Run();
		await r.Open();
		r.Raise();
		await LiveTests.Until(() => r.W.DiscardButton.IsEnabled);
		r.Answers.Enqueue(true);
		Click(r.W.DiscardButton);
		await LiveTests.Until(() => !r.W.DiscardButton.IsEnabled);
		Assert.Single(r.Asked);
	}

	// ---- Reload (live).

	[AvaloniaFact]
	public async Task ReloadIsForLiveWorldsOnly()
	{
		using var r = new Run();
		await r.Open();
		Assert.False(r.W.ReloadButton.IsVisible);
		Assert.False(r.W.AutoApplyBox.IsVisible);
		r.Raise();
		await r.W.ReloadHere();
		Assert.Empty(r.Asked);
		Assert.Equal(1, r.W.World!.Pending.Zones);
	}

	[AvaloniaFact]
	public async Task ReloadAsksWhenSomethingWaitsThenReadsTheGameAgain()
	{
		using var r = new Run();
		await r.OpenLive(1, 0, 1);
		Assert.True(r.W.ReloadButton.IsVisible);
		int snapshots = r.Game!.Snapshots;
		// Nothing waits: no question.
		await r.W.ReloadHere();
		Assert.Empty(r.Asked);
		Assert.Equal(snapshots + 1, r.Game.Snapshots);
		Assert.Equal("The world was loaded again from the game.", r.W.MessageText.Text);
		Assert.Equal(1, r.W.View.Scene!.X0);
		// Something waits: asked; no keeps it.
		r.Raise();
		r.Answers.Enqueue(false);
		await r.W.ReloadHere();
		Assert.Single(r.Asked);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		Assert.Equal(snapshots + 1, r.Game.Snapshots);
		r.Answers.Enqueue(true);
		Click(r.W.ReloadButton);
		await LiveTests.Until(() => r.W.World!.Pending == (0, 0, 0, 0) && r.W.Session != null && r.W.Session.UndoList.Count == 0);
		Assert.Equal(snapshots + 2, r.Game.Snapshots);
	}

	[AvaloniaFact]
	public async Task AGameThatDoesNotAnswerTheReloadIsReported()
	{
		using var r = new Run();
		await r.OpenLive();
		r.Game!.Fail["/snapshot"] = 500;
		await r.W.ReloadHere();
		Assert.StartsWith("Could not reach the game", r.W.MessageText.Text);
		Assert.NotNull(r.W.Session);
	}

	// ---- Auto apply.

	[AvaloniaFact]
	public async Task AutoSendsEveryChangeAndUndoToTheGame()
	{
		using var r = new Run();
		await r.OpenLive();
		Assert.True(r.W.AutoApplyBox.IsVisible);
		r.Raise();
		Assert.Equal(1, r.W.World!.Pending.Zones);
		// Switched on with something waiting: sent at once, and remembered.
		r.W.AutoApplyBox.IsChecked = true;
		await LiveTests.Until(() => r.W.World.Pending == (0, 0, 0, 0));
		Assert.True(r.W.Settings.AutoApply);
		Assert.Equal(1, r.Saves);
		Assert.Single(r.Game!.Terrain);
		// A new change, then its undo: each goes to the game by itself.
		r.Raise(40, 40, "second");
		await LiveTests.Until(() => r.Game.Terrain.Count == 2 && r.W.World.Pending == (0, 0, 0, 0));
		r.W.Undo();
		await LiveTests.Until(() => r.Game.Terrain.Count == 3 && r.W.World.Pending == (0, 0, 0, 0));
		// Off: changes wait again.
		r.W.AutoApplyBox.IsChecked = false;
		Assert.False(r.W.Settings.AutoApply);
		r.Raise(10, 10, "third");
		await Task.Delay(100);
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		Assert.Equal(1, r.W.World.Pending.Zones);
		Assert.Equal(3, r.Game.Terrain.Count);
	}

	[AvaloniaFact]
	public async Task AutoWaitsForTheEndOfAStroke()
	{
		using var r = new Run();
		await r.OpenLive();
		r.W.AutoApplyBox.IsChecked = true;
		var s = r.W.Session!;
		s.Brush.Radius = 4;
		s.BeginStroke(BrushTool.Raise, 32, 32);
		s.StrokeStep(32, 32, 0.1f);
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		Assert.Empty(r.Game!.Terrain);
		s.EndStroke();
		await LiveTests.Until(() => r.Game.Terrain.Count == 1 && r.W.World!.Pending == (0, 0, 0, 0));
	}

	[AvaloniaFact]
	public async Task AutoDoesNothingOffline()
	{
		using var r = new Run();
		await r.Open();
		r.W.Settings.AutoApply = true;
		r.Raise();
		await Task.Delay(100);
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		Assert.Equal(1, r.W.World!.Pending.Zones);
		// Apply now on an offline world does nothing either.
		await r.W.ApplyNow();
		Assert.Equal(1, r.W.World.Pending.Zones);
	}

	[AvaloniaFact]
	public async Task AppliesAskedWhileOneRunsAreMerged()
	{
		using var r = new Run();
		await r.OpenLive();
		r.Raise();
		var first = r.W.ApplyNow();
		r.Raise(40, 40, "during");
		var second = r.W.ApplyNow();
		Assert.True(second.IsCompleted);
		await first;
		Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
		// The second change went with the first apply or with the one merged after it.
		Assert.InRange(r.Game!.Terrain.Count, 1, 2);
		// The Save button (Apply live) goes the same way.
		r.Raise(10, 10, "button");
		await r.W.Save();
		Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
	}

	// ---- Players.

	[AvaloniaFact]
	public async Task ALiveWorldShowsItsPlayersAndGoesToThem()
	{
		using var r = new Run();
		await r.OpenLive();
		await r.W.PollPlayers();
		Assert.True(r.W.PlayersRow.IsVisible);
		Assert.Equal(new[] { "Ada", "Bjorn" }, r.W.LivePlayers.Select(p => p.Name));
		Assert.Equal(new Vector3(10, 30, -20), r.W.LivePlayers[0].World);
		Assert.Equal(r.W.LivePlayers, r.W.View.Players);
		Assert.Equal("Ada", r.W.PlayerBox.SelectedItem);
		Assert.True(r.W.GoPlayerButton.IsEnabled);
		// Ada is in the area: the camera turns to her.
		await r.W.GoToPlayer("Ada");
		var s = r.W.View.Scene!;
		Assert.Equal(0, s.X0);
		var t = r.W.View.Camera.Target;
		Assert.Equal(new Vector3(10 - s.Cx, 30, -(-20 - s.Cz)), t);
		Assert.Equal("Looking at Ada.", r.W.MessageText.Text);
		// Bjorn is outside: the area around him opens (his zone -1, 0).
		r.W.PlayerBox.SelectedItem = "Bjorn";
		Click(r.W.GoPlayerButton);
		await LiveTests.Until(() => r.W.View.Scene!.X0 == -1);
		await LiveTests.Until(() => r.W.MessageText.Text == "Looking at Bjorn.");
		Assert.Equal(0, r.W.View.Scene!.Z0);
		// Unknown or no name: nothing.
		await r.W.GoToPlayer("Nobody");
		await r.W.GoToPlayer(null);
		Assert.Equal("Looking at Bjorn.", r.W.MessageText.Text);
	}

	[AvaloniaFact]
	public async Task PlayersComeAndGoAndABrokenAnswerKeepsTheLast()
	{
		using var r = new Run();
		await r.OpenLive();
		await r.W.PollPlayers();
		r.W.PlayerBox.SelectedItem = "Bjorn";
		r.Game!.Players = "[{\"name\":\"Bjorn\",\"x\":1,\"y\":2,\"z\":3},{\"name\":\"Cid\",\"x\":4,\"y\":5,\"z\":6},{\"x\":9}]";
		await r.W.PollPlayers();
		Assert.Equal(new[] { "Bjorn", "Cid" }, r.W.LivePlayers.Select(p => p.Name));
		// The one chosen stays chosen.
		Assert.Equal("Bjorn", r.W.PlayerBox.SelectedItem);
		r.Game.Players = "not json";
		await r.W.PollPlayers();
		Assert.Equal(2, r.W.LivePlayers.Count);
		r.Game.Fail["/players"] = 500;
		await r.W.PollPlayers();
		Assert.Equal(2, r.W.LivePlayers.Count);
		r.Game.Fail.Remove("/players");
		r.Game.Players = "[]";
		await r.W.PollPlayers();
		Assert.Empty(r.W.LivePlayers);
		Assert.False(r.W.GoPlayerButton.IsEnabled);
		Assert.Null(r.W.PlayerBox.SelectedItem);
	}

	[AvaloniaFact]
	public async Task NamesAreShownOverThePlayersTheCameraSees()
	{
		using var r = new Run();
		await r.OpenLive();
		await r.W.PollPlayers();
		var s = r.W.View.Scene!;
		// Looking east at Ada; Bjorn (west) is behind the camera.
		var ada = new Vector3(10 - s.Cx, 30, 20 + s.Cz);
		r.W.View.SetCamera(ada + new Vector3(-10, 3, 0), ada, 1600 / 1000f);
		r.W.PlaceLabels();
		var label = Assert.IsType<TextBlock>(Assert.Single(r.W.PlayerLabels.Children));
		Assert.Equal("Ada", label.Text);
		// Back on the map: no labels.
		r.W.ShowMap();
		r.W.PlaceLabels();
		Assert.Empty(r.W.PlayerLabels.Children);
	}

	[AvaloniaFact]
	public async Task AnOfflineWorldHasNoPlayers()
	{
		using var r = new Run();
		await r.Open();
		Assert.False(r.W.PlayersRow.IsVisible);
		Assert.Empty(r.W.LivePlayers);
		await r.W.PollPlayers();
		Assert.Empty(r.W.View.Players);
		r.W.PlaceLabels();
		Assert.Empty(r.W.PlayerLabels.Children);
	}

	[Fact]
	public void APlayersPostIsABodyAndABeam()
	{
		var s = OverlayTests.Flat();
		var posts = GlView.PlayerPosts(s, new[] { ("Ada", new Vector3(10, 30, -20)) });
		// 12 sides of two rings, 4 sides of the body, the beam: 29 lines of 6 numbers.
		Assert.Equal(29 * 6, posts.Length);
		// The beam: from the feet 40 m up, at the player's place in view space.
		var beam = posts[^6..];
		Assert.Equal(new[] { 10 - s.Cx, 30, 20 + s.Cz, 10 - s.Cx, 70, 20 + s.Cz }, beam);
		Assert.Empty(GlView.PlayerPosts(s, Array.Empty<(string, Vector3)>()));
	}

	// ---- Moving the area.

	[AvaloniaFact]
	public async Task TheArrowsMoveTheAreaOneZoneKeepingTheViewAndTheEdits()
	{
		using var r = new Run();
		await r.Open();
		Assert.True(r.W.AreaNav.IsVisible);
		r.Raise();
		var cam = r.W.View.WorldCamera!.Value;
		Click(r.W.AreaEast);
		await LiveTests.Until(() => r.W.View.Scene!.X0 == 1);
		Assert.Equal(0, r.W.View.Scene!.Z0);
		var now = r.W.View.WorldCamera!.Value;
		Assert.Equal(cam.World.X, now.World.X, 3);
		Assert.Equal(cam.World.Z, now.World.Z, 3);
		Assert.Equal(cam.Distance, now.Distance);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		await r.W.MoveArea(0, 1);
		Assert.Equal((1, 1), (r.W.View.Scene!.X0, r.W.View.Scene.Z0));
		Click(r.W.AreaSouth);
		await LiveTests.Until(() => r.W.View.Scene!.Z0 == 0);
		Click(r.W.AreaWest);
		await LiveTests.Until(() => r.W.View.Scene!.X0 == 0);
		// Back where the change was made: it is still waiting.
		Assert.Equal(1, r.W.World.Pending.Zones);
		Click(r.W.AreaNorth);
		await LiveTests.Until(() => r.W.View.Scene!.Z0 == 1);
	}

	[AvaloniaFact]
	public async Task ALargerAreaMovesByOneZoneToo()
	{
		using var r = new Run();
		await r.Open(0, 0, 3);
		Assert.Equal(-1, r.W.View.Scene!.X0);
		await r.W.MoveArea(-1, 0);
		Assert.Equal(-2, r.W.View.Scene!.X0);
		Assert.Equal(3, r.W.View.Scene.Size);
	}

	// The area made bigger (or smaller) on the fly, around the same middle zone, keeping the view and
	// what waits to be saved; not while something unfinished would be lost.
	[AvaloniaFact]
	public async Task TheAreaGrowsAndShrinksOnTheFly()
	{
		using var r = new Run();
		await r.Open(0, 0, 3);
		Assert.Equal(0, r.W.AreaSizeBox.SelectedIndex);
		r.Raise();
		var cam = r.W.View.WorldCamera!.Value;
		r.W.AreaSizeBox.SelectedIndex = 2;
		await LiveTests.Until(() => r.W.View.Scene!.Size == 7);
		Assert.Equal((-3, -3), (r.W.View.Scene!.X0, r.W.View.Scene.Z0));
		Assert.Equal(cam.World.X, r.W.View.WorldCamera!.Value.World.X, 3);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		await r.W.ResizeArea(5);
		Assert.Equal((5, -2), (r.W.View.Scene!.Size, r.W.View.Scene.X0));
		Assert.Equal(1, r.W.AreaSizeBox.SelectedIndex);
		// Something selected: it waits, and says why.
		r.W.View.Select(new[] { 0 });
		if (r.W.View.Selected.Count > 0)
		{
			await r.W.ResizeArea(7);
			Assert.Equal(5, r.W.View.Scene!.Size);
			Assert.StartsWith("The area cannot change size now", r.W.MessageText.Text);
			Assert.Equal(1, r.W.AreaSizeBox.SelectedIndex);
		}
	}

	[AvaloniaFact]
	public async Task NoAreaNoMove()
	{
		using var r = new Run();
		await r.W.MoveArea(1, 0);
		Assert.Null(r.W.View.Scene);
		Assert.Null(await r.W.CheckFollow());
	}

	private static void LookAt(MainWindow w, float wx, float wz)
	{
		var c = w.View.WorldCamera!.Value;
		w.View.WorldCamera = (c.Yaw, c.Pitch, c.Distance, new Vector3(wx, 30, wz));
	}

	[AvaloniaFact]
	public async Task FollowMovesTheAreaWhenTheViewNearsItsEdge()
	{
		using var r = new Run();
		await r.Open();
		// Off: nothing moves.
		LookAt(r.W, 28, 0);
		Assert.Null(await r.W.CheckFollow());
		r.W.SetFollow(true);
		Assert.True(r.W.Settings.AreaFollow);
		Assert.Equal(1, r.Saves);
		Assert.True(r.W.FollowButton.Classes.Contains("on"));
		Assert.Contains("Follow: the area moves", r.W.MessageText.Text);
		// In the middle: stays.
		LookAt(r.W, 0, 0);
		Assert.Null(await r.W.CheckFollow());
		// Within 12 m of the east edge: one zone east, the view where it was.
		LookAt(r.W, 25, 3);
		Assert.Equal((1, 0), await r.W.CheckFollow());
		Assert.Equal(1, r.W.View.Scene!.X0);
		Assert.Equal(25, r.W.View.WorldCamera!.Value.World.X, 2);
		// Near a corner: both ways (the view is in zone 1, 0; south-west corner of the area at 32..96).
		LookAt(r.W, 36, -27);
		Assert.Equal((0, -1), await r.W.CheckFollow());
		// Off again (the button): stays.
		Click(r.W.FollowButton);
		Assert.False(r.W.Settings.AreaFollow);
		Assert.Contains("Follow is off", r.W.MessageText.Text);
		LookAt(r.W, 25 - 64, -64);
		Assert.Null(await r.W.CheckFollow());
	}

	[AvaloniaFact]
	public async Task FollowInALargerAreaGoesToTheZoneUnderTheView()
	{
		using var r = new Run();
		await r.Open(0, 0, 3);
		r.W.SetFollow(true);
		// West edge of zones -1..1 is at -96: the view at -88 is in zone -1, so the middle goes there.
		LookAt(r.W, -88, 0);
		Assert.Equal((-1, 0), await r.W.CheckFollow());
		Assert.Equal(-2, r.W.View.Scene!.X0);
	}

	[AvaloniaFact]
	public async Task FollowWaitsWhileSomethingWouldBeLost()
	{
		using var r = new Run();
		await r.Open();
		r.W.SetFollow(true);
		LookAt(r.W, 26, 0);
		// A selection.
		int i = r.W.View.Scene!.Things.FindIndex(t => !t.Gone);
		Assert.True(i >= 0);
		r.W.View.Select(new[] { i });
		Assert.Null(await r.W.CheckFollow());
		Assert.Equal("Follow waits: objects are selected (Esc clears).", r.W.MessageText.Text);
		// Said once while the reason stays.
		r.W.MessageText.Text = "other";
		Assert.Null(await r.W.CheckFollow());
		Assert.Equal("other", r.W.MessageText.Text);
		r.W.View.Select(Array.Empty<int>());
		// A path drawn.
		r.W.View.Path.Points.Add(new Vector2(10, 10));
		Assert.Null(await r.W.CheckFollow());
		Assert.Equal("Follow waits: a path is drawn (apply or clear it).", r.W.MessageText.Text);
		r.W.View.Path.Points.Clear();
		// A stroke on.
		r.W.Session!.BeginStroke(BrushTool.Raise, 32, 32);
		Assert.Equal("a stroke is on", r.W.MoveBlocker());
		r.W.Session.EndStroke();
		Assert.Null(r.W.MoveBlocker());
		// Nothing in the way: it moves.
		Assert.Equal((1, 0), await r.W.CheckFollow());
	}

	[AvaloniaFact]
	public async Task FollowWaitsWhileChangesAreSent()
	{
		using var r = new Run();
		await r.OpenLive();
		r.Raise();
		var apply = r.W.ApplyNow();
		Assert.Equal("changes are still being sent", r.W.MoveBlocker());
		await apply;
		Assert.Null(r.W.MoveBlocker());
	}

	[AvaloniaFact]
	public async Task FollowDoesNothingOnTheMap()
	{
		using var r = new Run();
		await r.Open();
		r.W.SetFollow(true);
		LookAt(r.W, 26, 0);
		r.W.ShowMap();
		Assert.Null(await r.W.CheckFollow());
	}

	// ---- Locations.

	[Fact]
	public void LocationsInOrNearTheAreaAreCounted()
	{
		var s = OverlayTests.Flat();
		var world = new WorldSave { Directory = "none", SaveNumber = 1 };
		// Area 0, 0 spans -32..33 m; 40 m more around it counts.
		world.Locations.Add((new Vector3(0, 30, 0), 1));
		world.Locations.Add((new Vector3(-70, 30, 70), 2));
		world.Locations.Add((new Vector3(-73, 30, 0), 3));
		world.Locations.Add((new Vector3(500, 30, 0), 4));
		s.World = world;
		Assert.Equal(2, Overlays.LocationsNear(s));
		Assert.Equal(Overlays.Build(s, null, _ => null).Locations, Overlays.LocationsNear(s));
		Assert.Equal(0, Overlays.LocationsNear(OverlayTests.Flat()));
	}

	[AvaloniaFact]
	public async Task TheLocationsNoteShowsWhenTheAreaHasSome()
	{
		using var r = new Run();
		await r.Open();
		Assert.False(r.W.LocationNote.IsVisible);
		// Two locations in the area (the test world has none of its own).
		r.W.World!.World.Locations.Add((new Vector3(5, 30, 5), 1));
		r.W.World.World.Locations.Add((new Vector3(-20, 30, 10), 2));
		await r.W.EditArea(0, 0, 1);
		int n = Overlays.LocationsNear(r.W.View.Scene!);
		Assert.Equal(2, n);
		Assert.True(r.W.LocationNote.IsVisible);
		Assert.Equal($"{n} location(s) here. The ground already includes the flattening the game does around them (turn on Location markers in View to see where).", r.W.LocationText.Text);
		// Closed with its ×.
		var close = ((Grid)r.W.LocationNote.Child!).Children.OfType<Button>().Single();
		Click(close);
		Assert.False(r.W.LocationNote.IsVisible);
		// An area far from any: no note.
		await r.W.EditArea(5, 5, 1);
		Assert.False(r.W.LocationNote.IsVisible);
		Assert.Equal("", r.W.LocationText.Text);
	}

	// ---- The session's side.

	[Fact]
	public void OnlyTheStepsNotAppliedAreUndone()
	{
		var s = EditTests.Flat(2);
		s.Shape(20, 20, Two, 3, 0, "a");
		s.Shape(40, 40, Two, 3, 0, "b");
		s.UndoList[0].Applied = true;
		Assert.Equal(1, s.UnappliedSteps);
		s.Shape(60, 60, Two, 3, 0, "c");
		s.Undo();
		Assert.True(s.CanRedo);
		Assert.Equal(1, s.UndoUnapplied());
		Assert.Equal("a", Assert.Single(s.UndoList).Label);
		Assert.False(s.CanRedo);
		Assert.Equal(0, s.UnappliedSteps);
		Assert.Equal(0, s.UndoUnapplied());
	}

	[Fact]
	public void NothingIsUndoneDuringAStroke()
	{
		var s = EditTests.Flat(2);
		s.Shape(20, 20, Two, 3, 0, "a");
		s.Undo();
		s.Redo();
		s.Undo();
		s.BeginStroke(BrushTool.Raise, 40, 40);
		Assert.Equal(0, s.UndoUnapplied());
		// The redo list stays too.
		Assert.True(s.CanRedo);
		s.EndStroke();
	}

	[Fact]
	public void EditsAreToldApartFromSaves()
	{
		var s = EditTests.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
		int edits = 0, changes = 0;
		s.EditMade += () => edits++;
		s.Changed += () => changes++;
		s.Shape(20, 20, Two, 3, 0, "a");
		s.Undo();
		s.Redo();
		s.Delete(new[] { 0 });
		s.RemoveChange(s.UndoList[0]);
		s.BeginStroke(BrushTool.Raise, 40, 40);
		s.StrokeStep(40, 40, 0.1f);
		s.EndStroke();
		Assert.Equal(6, edits);
		int before = changes;
		s.UndoUnapplied();
		Assert.True(changes > before);
	}
}
