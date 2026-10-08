using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Workshop: a blueprint opened onto the blank plot and saved back the same (building pieces only,
// measured from the anchor), and in the window: opening one, the support check (on and off, a floating
// floor in pink), saving it as a Homestead blueprint, and leaving (asking when it is not saved).
public class WorkshopTests
{
	private static string Hut(string dir, string name = "Hut")
	{
		Directory.CreateDirectory(dir);
		string path = Path.Combine(dir, Homestead.FileName(name));
		File.WriteAllText(path, "#Name:" + name + "\n#Creator:test\n#HomesteadVersion:1\n#Pieces\n"
			+ "wood_floor;Building;0;0;0;0;0;0;1;\"\";1;1;1\n"
			+ "woodwall;Building;1;1;0;0;0.707;0;0.707;\"\";1;1;1\n"
			// Up in the air, touching nothing: it would fall.
			+ "wood_floor;Building;0;12;6;0;0;0;1;\"\";1;1;1\n"
			+ "no_such_piece_xyz;Building;0;0;0;0;0;0;1;\"\";1;1;1\n");
		return path;
	}

	[Fact]
	public void ABlueprintOpensOnThePlotAndComesBackTheSame()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-ws-" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var scene = Workshop.Create("Workshop");
			var s = scene.Session!;
			var (placed, unknown, lift) = Workshop.Open(s, Hut(dir));
			Assert.Equal(3, placed);
			// Its lowest point (the floor's collider, a little below its origin) on the ground.
			Assert.Equal(-TerrainEditor.Editing.Hammer.Bottom("wood_floor", Quaternion.Identity), lift, 3);
			Assert.Equal(new[] { "no_such_piece_xyz" }, unknown);
			Assert.True(s.CanUndo);
			Assert.Equal(3, Workshop.Pieces(scene));
			Assert.Contains(scene.Things, t => !t.Gone && Vector3.Distance(t.Position, Workshop.Anchor(scene) + new Vector3(1, 1 + lift, 0)) < 1e-3f);
			// Saved back: as it was, from Homestead's anchor.
			var clip = Workshop.Building(scene, "Hut", lift);
			var text = Homestead.Write(clip, "Hut", "test", null, DateTime.Now);
			var back = BlueprintFormats.Parse("Hut.blueprint", text);
			Assert.Equal(3, back.Pieces.Count);
			Assert.Contains(back.Pieces, p => p.Name == "woodwall" && Vector3.Distance(p.Position, new Vector3(1, 1, 0)) < 1e-3f && MathF.Abs(p.Euler.Y - 90) < 0.5f);
			// Other files are centred, their lowest point on the ground (a wall reaches 1 m below its middle).
			string other = Path.Combine(dir, "other.blueprint");
			File.WriteAllText(other, "#Name:Other\n#Pieces\nwoodwall;Misc;10;5;10;0;0;0;1;\"\";1;1;1\nwoodwall;Misc;14;7;10;0;0;0;1;\"\";1;1;1\n");
			var plot = Workshop.Create("Workshop");
			Workshop.Open(plot.Session!, other);
			var walls = plot.Things.Where(t => !t.Gone).Select(t => t.Position - Workshop.Anchor(plot)).OrderBy(p => p.X).ToList();
			Assert.Equal(new Vector3(-2, 1, 0), walls[0]);
			Assert.Equal(new Vector3(2, 3, 0), walls[1]);
		}
		finally
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
	}

	[AvaloniaFact]
	public async Task TheWorkshopOpensChecksSupportSavesAndAsksBeforeLeaving()
	{
		using var r = new PanelBlueprintsTests.Run();
		var w = r.W;
		string path = Hut(r.Homestead);
		var asked = new List<string>();
		bool answer = false;
		w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
		w.Tell = _ => Task.CompletedTask;
		await w.OpenWorkshop(path);
		Assert.True(w.InWorkshop);
		Assert.True(w.SupportBox.IsVisible);
		Assert.Contains("Opened “Hut”: 3 piece(s)", w.MessageText.Text);
		Assert.Contains("no_such_piece_xyz", w.MessageText.Text);
		// The floating floor would fall; the rest stands.
		var support = w.LastSupport!;
		Assert.Equal(1, support.Breaking);
		Assert.NotNull(w.View.Support);
		Assert.Contains("1 would fall", w.PendingText.Text);
		Assert.Contains("saved", w.PendingText.Text);
		w.SupportBox.IsChecked = false;
		Assert.Null(w.LastSupport);
		Assert.Null(w.View.Support);
		w.SupportBox.IsChecked = true;
		Assert.NotNull(w.LastSupport);
		// Something changed: leaving asks, and staying stays.
		w.Session!.Commit("Moved", null, new[] { w.Session.Scene.Things.FindIndex(t => !t.Gone) }, Array.Empty<(NewObject, bool)>());
		Dispatcher.UIThread.RunJobs();
		Assert.Contains("not saved as a blueprint", w.PendingText.Text);
		await w.LeaveWorkshop();
		Assert.Equal("Leave the Workshop", Assert.Single(asked));
		Assert.True(w.InWorkshop);
		// Saving asks first, since a piece would fall; then writes the building only.
		// The details asked start from the blueprint's own, with its cost.
		string? costAsked = null;
		r.B.AskDetails = (d, cost) => { costAsked = cost; return Task.FromResult<Homestead.Details?>(d with { Name = "Hut 2", Tags = new() { "hut" } }); };
		Assert.StartsWith("Wood ", w.WorkshopCost);
		answer = true;
		await w.SaveWorkshop();
		Assert.Equal("Save blueprint", asked[^1]);
		var saved = Homestead.List(r.Homestead).Single(e => e.Name == "Hut 2");
		Assert.Equal(2, saved.Pieces);
		Assert.Equal(new[] { "hut" }, saved.Tags);
		Assert.StartsWith("Wood ", costAsked);
		Assert.NotNull(saved.Picture);
		Assert.Contains("· saved", w.PendingText.Text);
		// Saved: leaving does not ask.
		answer = false;
		int before = asked.Count;
		await w.LeaveWorkshop();
		Assert.Equal(before, asked.Count);
		Assert.False(w.InWorkshop);
		Assert.False(w.SupportBox.IsVisible);
	}

	// The Workshop keeps to building: Build, Select and View on the rail (other tools and keys do
	// nothing there), the Build panel instead of Place's, no Mask, View panel or zone borders; leaving
	// gives the Place tool and the rest back as they were.
	[AvaloniaFact]
	public async Task TheWorkshopOnlyBuildsAndGivesTheToolsBackWhenLeft()
	{
		using var r = new PanelBlueprintsTests.Run();
		var w = r.W;
		w.Ask = (_, _, _, _) => Task.FromResult(true);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Beech1");
		w.PlaceTool.RandomYaw = true;
		w.View.SetOverlay(Overlays.Layer.Borders, true);
		await w.OpenWorkshop(null);
		Assert.True(w.Tools.Workshop);
		Assert.Equal(ToolMode.Place, w.Tools.Mode);
		Assert.True(w.BuildPanel.Card.IsVisible);
		Assert.False(w.PlacePanel.Card.IsVisible);
		Assert.False(w.MaskPanel.Card.IsVisible);
		Assert.False(w.ViewButton.IsVisible);
		Assert.False(w.View.IsOverlayShown(Overlays.Layer.Borders));
		// A building piece, one at a time, facing the turn.
		Assert.Equal(new[] { "woodwall" }, w.PlaceTool.Chosen);
		Assert.True(w.PlaceTool.Building && w.PlaceTool.OneAtATime && !w.PlaceTool.RandomYaw);
		// World tools are not there.
		w.Tools.ChooseMode(ToolMode.Mountain);
		w.Tools.Choose(BrushTool.Raise);
		Assert.Equal(ToolMode.Place, w.Tools.Mode);
		w.Tools.ChooseSelect();
		Assert.Equal(ToolMode.Select, w.Tools.Mode);
		Assert.False(w.BuildPanel.Card.IsVisible);
		w.Tools.ChooseMode(ToolMode.Place);
		// Choosing a piece, the grid and the turn step.
		w.BuildPanel.Choose("stone_wall_2x1");
		Assert.Equal(new[] { "stone_wall_2x1" }, w.PlaceTool.Chosen);
		Assert.Contains("Stone 4", w.BuildPanel.Cost.Text);
		w.BuildPanel.GridBox.SelectedIndex = 3;
		Assert.Equal(2, w.PlaceTool.GridStep);
		w.BuildPanel.TurnBox.SelectedIndex = 3;
		Assert.True(w.PlaceInput.Key(Avalonia.Input.Key.OemPeriod, false, false));
		Assert.Equal(45, w.PlaceTool.Rotation);
		// Everything players build: the cultivator's and the serving tray's pieces too.
		Assert.Contains(BuildPanel.Pieces.Value, p => p.Category == BuildPanel.Plants);
		Assert.Contains(BuildPanel.Pieces.Value, p => p.Category == BuildPanel.Feasts);
		w.BuildPanel.SnapBox.IsChecked = false;
		Assert.False(w.PlaceTool.SnapTo);
		await w.LeaveWorkshop();
		Assert.False(w.Tools.Workshop);
		Assert.Equal(new[] { "Beech1" }, w.PlaceTool.Chosen);
		Assert.True(w.PlaceTool.RandomYaw && w.PlaceTool.SnapTo && !w.PlaceTool.Building);
		Assert.Equal(0, w.PlaceTool.GridStep);
		Assert.Null(w.PlaceInput.TurnStep);
		Assert.True(w.ViewButton.IsVisible);
		Assert.True(w.View.IsOverlayShown(Overlays.Layer.Borders));
	}

	[Fact]
	public void BuildPiecesAreTheHammersByStationThenName()
	{
		var pieces = BuildPanel.Pieces.Value;
		Assert.Contains(pieces, p => p.Prefab == "woodwall" && p.Name == "Wood Wall" && p.Category == 2);
		Assert.DoesNotContain(pieces, p => p.Prefab == "Beech1");
		for (int i = 1; i < pieces.Count; i++)
		{
			string? a = TerrainEditor.Terrain.PieceCost.Get(pieces[i - 1].Prefab)?.Station, b = TerrainEditor.Terrain.PieceCost.Get(pieces[i].Prefab)?.Station;
			Assert.True(BuildPanel.StationRank(a) <= BuildPanel.StationRank(b));
			if (a == b)
			{
				Assert.True(string.Compare(pieces[i - 1].Name, pieces[i].Name, StringComparison.OrdinalIgnoreCase) <= 0);
			}
		}
	}

	// Building as the game's hammer (Hammer): the piece goes where the build ray points (a wall's top:
	// on it), turned by the game's step; the grid rounds a ground point; Ctrl + wheel lifts it (a notch
	// at a time, parts of a notch added up).
	[AvaloniaFact]
	public async Task PiecesGoUpWhereTheCursorPointsAndKeepTheirTurn()
	{
		using var r = new PanelBlueprintsTests.Run();
		var w = r.W;
		await w.OpenWorkshop(null);
		var s = w.Session!;
		var t = w.PlaceTool;
		int c = (s.Scene.W - 1) / 2;
		// The build ray, as the view gives it (world space): straight down onto the plot's middle.
		var mid = new Vector3(s.Scene.X0 * 64f - 32f + c, 0, s.Scene.Z0 * 64f - 32f + c);
		void Aim(float dx, float dz) => t.AimRay = (mid + new Vector3(dx, Workshop.Ground + 50, dz), -Vector3.UnitY, 50);
		Aim(0, 0);
		var first = t.Preview(new Vector2(c, c)).Single();
		Assert.Equal(Workshop.Ground + 1, first.Position.Y, 2);
		s.Commit("wall", null, Array.Empty<int>(), new[] { (new NewObject(0, StableHash.Of("woodwall"), first.Position, first.Rotation, 0), true) });
		// Pointed at its top: on it.
		Aim(0.3f, 0);
		var up = t.Preview(new Vector2(c, c)).Single();
		Assert.Equal(Workshop.Ground + 3, up.Position.Y, 2);
		Assert.Equal("snapped to Wood Wall", t.SnappedTo);
		// Turned as asked, by the game's 22.5° step.
		Assert.True(w.PlaceInput.Key(Avalonia.Input.Key.OemPeriod, false, false));
		Assert.Equal(22.5f, t.Rotation);
		Assert.Equal(22.5f, t.Preview(new Vector2(c, c)).Single().Rotation.Y, 2);
		// The grid rounds the ground's point (Off by default, as in the game).
		t.Rotation = 0;
		w.BuildPanel.SnapBox.IsChecked = false;
		w.BuildPanel.GridBox.SelectedIndex = 3;
		Aim(5.4f, 0);
		Assert.Equal(mid.X + 6, t.Preview(new Vector2(c, c)).Single().Position.X, 2);
		w.BuildPanel.GridBox.SelectedIndex = 0;
		w.BuildPanel.SnapBox.IsChecked = true;
		// Ctrl + wheel: half a metre a notch; two half notches make one; Shift: 0.1 m.
		t.Rotation = 0;
		void Wheel(double dy, Avalonia.Input.KeyModifiers mods) => w.Surface.RaiseEvent(new Avalonia.Input.PointerWheelEventArgs(w.Surface,
			new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true), w.Surface, new Avalonia.Point(10, 10), 0,
			new Avalonia.Input.PointerPointProperties(), mods, new Avalonia.Vector(0, dy)));
		Wheel(1, Avalonia.Input.KeyModifiers.Control);
		Assert.Equal(0.5f, t.HeightNudge);
		Wheel(0.5, Avalonia.Input.KeyModifiers.Control);
		Assert.Equal(0.5f, t.HeightNudge);
		Wheel(0.5, Avalonia.Input.KeyModifiers.Control);
		Assert.Equal(1f, t.HeightNudge);
		Wheel(-1, Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift);
		Assert.Equal(0.9f, t.HeightNudge, 3);
		Assert.Equal("+0.9 m", w.BuildPanel.LiftText.Text);
		// Alt + wheel turns by the step, a notch at a time.
		t.Rotation = 0;
		w.BuildPanel.TurnBox.SelectedIndex = 4;
		Wheel(0.5, Avalonia.Input.KeyModifiers.Alt);
		Assert.Equal(0, t.Rotation);
		Wheel(0.5, Avalonia.Input.KeyModifiers.Alt);
		Assert.Equal(90, MathF.Abs(t.Rotation));
		Wheel(1, Avalonia.Input.KeyModifiers.Alt);
		Assert.Equal(180, MathF.Abs(t.Rotation));
		// Reset: no lift.
		w.BuildPanel.LiftReset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(0, t.HeightNudge);
	}

	// The Workshop's Library: Homestead's blueprints, searched, opened alone on the plot, added to it
	// (one undo step each), and dropped onto the view (from the Library, or files from the computer:
	// imported first; other files said so). The unsaved marks are off in the Workshop.
	[AvaloniaFact]
	public async Task TheLibraryOpensAddsAndTakesDrops()
	{
		using var r = new PanelBlueprintsTests.Run();
		var w = r.W;
		r.Keep("Gate", "woodwall", "wood_floor");
		r.Keep("Tower", "woodwall");
		w.Ask = (_, _, _, _) => Task.FromResult(true);
		await w.OpenWorkshop(null);
		Assert.False(w.View.ShowNewMarkers);
		var bp = w.BuildPanel;
		bp.ShowLibrary(true);
		Assert.True(bp.LibraryShown);
		Assert.Equal(2, bp.LibraryList.Children.Count);
		bp.LibrarySearch.Text = "tow";
		Assert.Single(bp.LibraryList.Children);
		bp.LibrarySearch.Text = "";
		// Add: into the plot, with what is there.
		w.AddToWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"), null);
		Assert.Equal(2, Workshop.Pieces(w.Session!.Scene));
		w.AddToWorkshop(Path.Combine(r.Homestead, "Tower.blueprint"), new Vector2(5, 5));
		Assert.Equal(3, Workshop.Pieces(w.Session!.Scene));
		Assert.Contains(w.Session.Scene.Things, t => !t.Gone && MathF.Abs(t.Position.X - 5) < 0.01f && MathF.Abs(t.Position.Z - 5) < 0.01f);
		Assert.StartsWith("Added “Tower”: 1 piece(s).", w.MessageText.Text);
		w.Session.Undo();
		Assert.Equal(2, Workshop.Pieces(w.Session.Scene));
		// Dropped: from the Library, and a file from elsewhere (imported, then added).
		w.Drop(Path.Combine(r.Homestead, "Tower.blueprint"), Array.Empty<string>(), new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
		Assert.Equal(3, Workshop.Pieces(w.Session.Scene));
		string other = Path.Combine(r.Dir, "Wall.vbuild");
		File.WriteAllText(other, BlueprintFormats.Write(System.Text.Json.Nodes.JsonNode.Parse(CopyFormat.ToJson(PanelBlueprintsTests.Clip("Wall", "woodwall", "woodwall")).ToJsonString())!.AsObject(), "vbuild", "Wall", _ => 0));
		w.Drop(null, new[] { other, Path.Combine(r.Dir, "notes.txt") }, new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
		Assert.Equal(5, Workshop.Pieces(w.Session.Scene));
		Assert.Contains(r.Listed, e => e.Name == "Wall");
		Assert.Equal(3, bp.LibraryList.Children.Count);
		w.Drop(null, new[] { Path.Combine(r.Dir, "notes.txt") }, new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
		Assert.Equal("Drop .blueprint (Homestead, PlanBuild) or .vbuild files.", w.MessageText.Text);
		// Open: that one alone (the plot was not saved: asked first).
		await w.OpenWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"));
		Assert.Equal(2, Workshop.Pieces(w.Session.Scene));
		await w.LeaveWorkshop();
		Assert.Equal(w.UnsavedBox.IsChecked == true, w.View.ShowNewMarkers);
	}

	// The support check's colours are the game's (WearNTear.Highlight): light blue on the ground, red to
	// green as support grows, red when it breaks; and the cut hides the building above a height.
	[AvaloniaFact]
	public async Task SupportColoursAreTheGamesAndTheCutHidesAboveIt()
	{
		Assert.Equal(new Vector3(0.6f, 0.8f, 1f), GlView.SupportTint(-1));
		var red = GlView.SupportTint(0);
		Assert.True(red.X > 1 && red.Y < 0.01f && red.Z < 0.01f, red.ToString());
		Assert.Equal(red, GlView.SupportTint(-2));
		var green = GlView.SupportTint(1);
		Assert.True(green.Y > green.X && green.Y > green.Z && green.X > 0.4f, green.ToString());
		var middle = GlView.SupportTint(0.5f);
		Assert.True(middle.X > 0.5f && middle.Y > 0.5f && middle.Z < middle.X, middle.ToString());
		using var r = new PanelBlueprintsTests.Run();
		var w = r.W;
		await w.OpenWorkshop(null);
		Assert.True(w.CutBox.IsVisible);
		Assert.Null(w.View.CutY);
		w.CutSlider.Value = 6;
		Assert.Equal(Workshop.Ground + 6, w.View.CutY);
		Assert.Equal(Workshop.Ground + 6, w.PlaceTool.CutY);
		Assert.Equal("Cut at 6 m", w.CutText.Text);
		w.CutSlider.Value = 0;
		Assert.Null(w.PlaceTool.CutY);
		w.CutSlider.Value = 3;
		await w.LeaveWorkshop();
		Assert.False(w.CutBox.IsVisible);
		Assert.Null(w.View.CutY);
	}
}
