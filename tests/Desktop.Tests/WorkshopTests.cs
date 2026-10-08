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
			var (placed, unknown) = Workshop.Open(s, Hut(dir));
			Assert.Equal(3, placed);
			Assert.Equal(new[] { "no_such_piece_xyz" }, unknown);
			Assert.True(s.CanUndo);
			Assert.Equal(3, Workshop.Pieces(scene));
			Assert.Contains(scene.Things, t => !t.Gone && t.Position == Workshop.Anchor(scene) + new Vector3(1, 1, 0));
			var clip = Workshop.Building(scene, "Hut");
			var text = Homestead.Write(clip, "Hut", "test", null, DateTime.Now);
			var back = BlueprintFormats.Parse("Hut.blueprint", text);
			Assert.Equal(3, back.Pieces.Count);
			Assert.Contains(back.Pieces, p => p.Name == "woodwall" && Vector3.Distance(p.Position, new Vector3(1, 1, 0)) < 1e-3f && MathF.Abs(p.Euler.Y - 90) < 0.5f);
			// Other files are centred, their lowest piece on the ground.
			string other = Path.Combine(dir, "other.blueprint");
			File.WriteAllText(other, "#Name:Other\n#Pieces\nwoodwall;Misc;10;5;10;0;0;0;1;\"\";1;1;1\nwoodwall;Misc;14;7;10;0;0;0;1;\"\";1;1;1\n");
			var plot = Workshop.Create("Workshop");
			Workshop.Open(plot.Session!, other);
			var walls = plot.Things.Where(t => !t.Gone).Select(t => t.Position - Workshop.Anchor(plot)).OrderBy(p => p.X).ToList();
			Assert.Equal(new Vector3(-2, 0, 0), walls[0]);
			Assert.Equal(new Vector3(2, 2, 0), walls[1]);
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
		r.B.AskName = _ => Task.FromResult<string?>("Hut 2");
		answer = true;
		await w.SaveWorkshop();
		Assert.Equal("Save blueprint", asked[^1]);
		var saved = Homestead.List(r.Homestead).Single(e => e.Name == "Hut 2");
		Assert.Equal(2, saved.Pieces);
		Assert.Contains("· saved", w.PendingText.Text);
		// Saved: leaving does not ask.
		answer = false;
		int before = asked.Count;
		await w.LeaveWorkshop();
		Assert.Equal(before, asked.Count);
		Assert.False(w.InWorkshop);
		Assert.False(w.SupportBox.IsVisible);
	}
}
