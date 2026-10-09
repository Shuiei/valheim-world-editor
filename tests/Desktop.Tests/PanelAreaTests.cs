using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Area panel through its own controls on a copy of the test world (zone 0, 0): every ground
// action chosen in its list and applied, what it says with nothing to work on, Replace chosen from its
// lists, the height average, the backups list ("Another folder…", a folder that is not a world,
// another world), a restore with a soft edge, Cancel reset, Regrow's limits, and heightmaps.
[Collection("World files")]
public class PanelAreaTests
{
	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public EditSession S { get; }
		public AreaPanel P => W.AreaPanel;
		public string Said => W.MessageText.Text ?? "";

		public Run(bool select = true, int size = 1)
		{
			var scene = WorldScene.Load(Dir, 0, 0, size);
			W.Show();
			W.View.Show(scene, null);
			W.Edit(scene.Session!);
			S = scene.Session!;
			W.Tools.ChooseMode(ToolMode.Area);
			if (select)
			{
				Select(2, 2, 62, 62);
			}
		}

		public void Select(float x0, float z0, float x1, float z1)
		{
			W.View.Area.Clear();
			W.View.Area.Points.Add(new Vector2(x0, z0));
			W.View.Area.Points.Add(new Vector2(x1, z1));
			W.View.Area.Notify();
		}

		public float H(int x, int z) => S.Scene.Heights[z * S.Scene.W + x];

		public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
	}

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaTheory]
	[InlineData(AreaPanel.Act.Raise, "Raise applied to")]
	[InlineData(AreaPanel.Act.Lower, "Lower applied to")]
	[InlineData(AreaPanel.Act.Smooth, "Smooth applied to")]
	[InlineData(AreaPanel.Act.Natural, "Naturalize applied to")]
	[InlineData(AreaPanel.Act.Flatten, "Flatten applied to")]
	public async Task EachGroundActionIsAppliedFromThePanel(AreaPanel.Act act, string said)
	{
		using var r = new Run();
		float before = r.H(32, 32);
		r.P.Choose(act);
		r.P.HeightBox.Value = (decimal)(before + 3);
		r.P.AmountBox.Value = 1.5m;
		await r.P.Apply();
		Assert.StartsWith(said, r.Said);
		Assert.True(r.S.CanUndo);
		Assert.StartsWith("Area: ", r.S.UndoLabel);
		if (act == AreaPanel.Act.Raise)
		{
			Assert.Equal(before + 1.5f, r.H(32, 32), 2);
		}
		if (act == AreaPanel.Act.Lower)
		{
			Assert.Equal(before - 1.5f, r.H(32, 32), 2);
		}
		if (act == AreaPanel.Act.Flatten)
		{
			Assert.Equal(before + 3, r.H(32, 32), 2);
		}
	}

	[AvaloniaFact]
	public async Task PaintIsAppliedWithTheKindChosenInItsList()
	{
		using var r = new Run();
		r.P.Choose(AreaPanel.Act.Paint);
		int paved = Array.FindIndex(((IEnumerable<string>)r.P.PaintBox.ItemsSource!).ToArray(), l => l.Contains("aved"));
		r.P.PaintBox.SelectedIndex = paved;
		await r.P.Apply();
		Assert.StartsWith("Paint paved applied to", r.Said);
		int p = 32 * r.S.Ground.W + 32;
		Assert.Equal(Brush.PaintOf(BrushTool.PaintPaved), r.S.Ground.Paint[(p * 4)..(p * 4 + 4)]);
	}

	[AvaloniaFact]
	public async Task RestoreAndErodeAreGroundActionsToo()
	{
		using var r = new Run();
		r.P.Choose(AreaPanel.Act.Raise);
		await r.P.Apply();
		r.P.Choose(AreaPanel.Act.Restore);
		await r.P.Apply();
		Assert.StartsWith("Restore", r.Said);
		r.P.Choose(AreaPanel.Act.Erode);
		await r.P.Apply();
		Assert.Contains("applied to", r.Said);
	}

	[AvaloniaFact]
	public async Task WithoutASelectionOrSessionItSaysWhatToDo()
	{
		using var r = new Run(select: false);
		r.P.Choose(AreaPanel.Act.Raise);
		await r.P.Apply();
		Assert.Equal("Select an area first.", r.Said);
		Assert.False(r.S.CanUndo);
		// No area open at all: nothing happens.
		var lone = new MainWindow(load: false) { Width = 800, Height = 600 };
		lone.Show();
		await lone.AreaPanel.Apply();
		Assert.Equal("", lone.MessageText.Text ?? "");
	}

	[AvaloniaFact]
	public async Task RemoveWithNoneOfTheChosenKindsInsideSaysSo()
	{
		using var r = new Run();
		foreach (var b in r.P.KindButtons.Values)
		{
			b.IsChecked = false;
		}
		r.P.Choose(AreaPanel.Act.Remove);
		await r.P.Apply();
		Assert.Equal("No objects of the chosen kinds inside the selection.", r.Said);
	}

	[AvaloniaFact]
	public async Task ReplaceUsesTheTwoLists()
	{
		using var r = new Run();
		r.P.Choose(AreaPanel.Act.Replace);
		r.P.Refresh();
		r.P.FromBox.SelectedIndex = -1;
		r.P.ToBox.SelectedIndex = -1;
		await r.P.Apply();
		Assert.Equal("Choose what to replace, and with what.", r.Said);
		var from = ((IEnumerable<object>)r.P.FromBox.ItemsSource!).Cast<string>().ToList();
		Assert.NotEmpty(from);
		r.P.FromBox.SelectedIndex = 0;
		string name = from[0].Split(" (")[0];
		int count = int.Parse(from[0].Split(" (")[1].TrimEnd(')'));
		var to = ((IEnumerable<object>)r.P.ToBox.ItemsSource!).Cast<string>().ToList();
		r.P.ToBox.SelectedIndex = to.FindIndex(t => t != name);
		await r.P.Apply();
		Assert.StartsWith($"Replaced {count} object(s) with", r.Said);
		Assert.StartsWith($"Replaced {count} with", r.S.UndoLabel);
	}

	[AvaloniaFact]
	public void TheAverageButtonPutsTheMeanHeightInTheBox()
	{
		using var r = new Run();
		Click(r.P.AverageButton);
		float avg = r.W.View.Area.Average(r.S.Ground)!.Value;
		Assert.Equal((decimal)MathF.Round(avg, 1), r.P.HeightBox.Value);
	}

	[AvaloniaFact]
	public async Task TheBackupListOffersAnotherFolder()
	{
		using var r = new Run();
		r.P.Choose(AreaPanel.Act.Backup);
		var items = ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ToList();
		Assert.Equal("Another folder…", items.Last());
		// Nothing chosen yet.
		await r.P.Apply();
		Assert.Equal("Choose a backup first.", r.Said);
		// Another folder, cancelled: nothing chosen.
		r.P.PickFolder = () => Task.FromResult<string?>(null);
		r.P.BackupBox.SelectedIndex = items.Count - 1;
		await LiveTests.Until(() => r.P.BackupBox.SelectedIndex == -1);
		// A folder that is not a world: said in the panel and in the message.
		string notAWorld = Path.Combine(Path.GetDirectoryName(r.Dir)!, "empty");
		Directory.CreateDirectory(notAWorld);
		int asked = 0;
		r.P.PickFolder = () => { asked++; return Task.FromResult<string?>(notAWorld); };
		r.P.BackupBox.SelectedIndex = r.P.BackupBox.ItemCount - 1;
		await LiveTests.Until(() => r.P.BackupBox.SelectedIndex >= 0 && ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().Contains(notAWorld));
		// Asked once: the new list used to keep "Another folder…" selected and ask again, forever.
		Assert.Equal(1, asked);
		Assert.Equal(notAWorld, ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ElementAt(r.P.BackupBox.SelectedIndex));
		await r.P.Apply();
		Assert.StartsWith("That folder is not a world save that can be read", r.P.BackupInfo.Text);
		Assert.Equal(r.P.BackupInfo.Text, r.Said);
	}

	[AvaloniaFact]
	public async Task ABackupOfAnotherWorldIsRefused()
	{
		using var r = new Run();
		var other = WorldSave.Load(r.Dir);
		typeof(WorldSave).GetProperty(nameof(WorldSave.Seed))!.SetValue(other, other.Seed + 1);
		typeof(WorldSave).GetProperty(nameof(WorldSave.SeedName))!.SetValue(other, "Other");
		Assert.Null(await r.P.OpenBackup(r.Dir, other));
		Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
		// The same world is read once, then kept.
		Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
		Assert.StartsWith("Backup: save #", r.P.BackupInfo.Text);
		r.P.BackupInfo.Text = "";
		Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
		Assert.Equal("", r.P.BackupInfo.Text);
		// Kept, it is still refused for another world (the same folder chosen after opening that one).
		Assert.Null(await r.P.OpenBackup(r.Dir, other));
		Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
	}

	[AvaloniaFact]
	public async Task ARestoreWithASoftEdgeBlendsTowardsTheBackup()
	{
		using var r = new Run();
		r.Select(10, 10, 54, 54);
		r.P.SoftSlider.Value = 10;
		float was = r.H(32, 32), wasEdge = r.H(12, 32), wasOut = r.H(6, 32);
		// Raised and painted everywhere, saved: the backup is the world before.
		r.S.Shape(32, 32, Formula.Compile("3", new string[0]), 40, 0, "x");
		r.S.EditGround("paint", g =>
		{
			var touched = new List<int>();
			for (int z = 5; z < 60; z++)
			{
				for (int x = 5; x < 60; x++)
				{
					int p = z * g.W + x;
					g.PMod[p] = 1;
					Array.Copy(Brush.PaintOf(BrushTool.PaintDirt)!, 0, g.Paint, p * 4, 4);
					touched.Add(p);
				}
			}
			return (touched, (4, 4, 61, 61));
		});
		// The game's backup from before, then saved.
		WorldEditor.Tests.TempWorld.CopyDir(r.Dir, r.Dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000");
		Assert.True(r.S.Save().Saved);
		var backup = Assert.Single(Backups.Find(r.Dir));
		foreach (var b in r.P.KindButtons.Values)
		{
			b.IsChecked = false;
		}
		await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, backup.Path);
		Assert.StartsWith("Restored from the backup: the ground", r.Said);
		// The middle as it was; the soft edge part way; outside the selection, still raised.
		Assert.Equal(was, r.H(32, 32), 2);
		float edge = r.H(12, 32);
		Assert.InRange(edge, wasEdge + 0.05f, wasEdge + 2.95f);
		Assert.Equal(wasOut + 3, r.H(6, 32), 2);
		int mid = 32 * r.S.Ground.W + 32;
		Assert.Equal(0, r.S.Ground.PMod[mid]);
	}

	[AvaloniaFact]
	public async Task ARestoreOfNeitherGroundNorObjectsDoesNothing()
	{
		using var r = new Run();
		r.P.BackupGroundBox.IsChecked = false;
		r.P.BackupObjectsBox.IsChecked = false;
		await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, r.Dir);
		Assert.Equal("Nothing to restore: the selection is as it was in the backup.", r.Said);
		Assert.False(r.S.CanUndo);
	}

	[AvaloniaFact]
	public async Task CancelResetWithNothingMarkedSaysSoAndResetAsksFirst()
	{
		using var r = new Run();
		Click(r.P.UnresetButton);
		Assert.Equal("No zone marked for reset under the selection.", r.Said);
		r.P.Confirm = _ => Task.FromResult(false);
		r.P.Choose(AreaPanel.Act.Reset);
		await r.P.Apply();
		Assert.Empty(r.S.Resets);
		r.P.Confirm = _ => Task.FromResult(true);
		r.P.ResetGroundBox.IsChecked = false;
		await r.P.Apply();
		Assert.Single(r.S.Resets);
		Click(r.P.UnresetButton);
		Assert.Empty(r.S.Resets);
		Assert.StartsWith("Cancelled reset of 1 zone(s)", r.S.UndoLabel);
	}

	[AvaloniaFact]
	public async Task RegrowNeedsTheGeneratorAndASmallEnoughArea()
	{
		// An area with no world generator behind it.
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseMode(ToolMode.Area);
		w.View.Area.Points.Add(new Vector2(2, 2));
		w.View.Area.Points.Add(new Vector2(60, 60));
		w.AreaPanel.Choose(AreaPanel.Act.Regrow);
		await w.AreaPanel.Apply();
		Assert.Equal("Regrow needs the world's generator (not available here).", w.MessageText.Text);
		// More than 16 zones.
		using var r = new Run(select: false, size: 5);
		r.Select(1, 1, 319, 319);
		r.P.Choose(AreaPanel.Act.Regrow);
		await r.P.Apply();
		Assert.StartsWith("Choose a smaller area", r.Said);
	}

	[AvaloniaFact]
	public async Task HeightmapsAreExportedToTheFolderAndBadPicturesRefused()
	{
		using var r = new Run();
		string folder = Path.Combine(Path.GetDirectoryName(r.Dir)!, "maps");
		r.P.HeightmapFolder = folder;
		Click(r.P.ExportButton);
		Assert.StartsWith($"Heightmap written to {folder}", r.Said);
		string png = Assert.Single(Directory.GetFiles(folder, "*.png"));
		// A picture that is not one.
		string bad = Path.Combine(folder, "bad.png");
		File.WriteAllText(bad, "not a picture");
		r.P.PickPicture = () => Task.FromResult<string?>(bad);
		Click(r.P.ImportButton);
		await LiveTests.Until(() => r.Said.StartsWith("That picture cannot be used"));
		// The exported one, imported back over the whole area (no selection), then cancelled.
		r.W.View.Area.Clear();
		r.P.PickPicture = () => Task.FromResult<string?>(png);
		Click(r.P.ImportButton);
		await LiveTests.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
		Click(r.P.CancelPictureButton);
		Click(r.P.PutButton);
		Assert.False(r.S.CanUndo);
		// Again, with the heights pushed above the game's limit: it says how many points stopped short.
		Click(r.P.ImportButton);
		await LiveTests.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
		r.P.LowestBox.Value += 20;
		r.P.HighestBox.Value += 20;
		Click(r.P.PutButton);
		Assert.StartsWith("Imported, but", r.Said);
		Assert.Contains("could not reach their height", r.Said);
	}

	[AvaloniaFact]
	public void CopyPasteAndBlueprintButtonsAskTheWindow()
	{
		using var r = new Run();
		var asked = new List<string>();
		r.P.CopyAsked += () => asked.Add("copy");
		r.P.PasteAsked += () => asked.Add("paste");
		r.P.SaveBlueprintAsked += () => asked.Add("save");
		r.P.LibraryAsked += () => asked.Add("library");
		Click(r.P.CopyButton);
		Click(r.P.PasteButton);
		Click(r.P.SaveBlueprintButton);
		Click(r.P.LibraryButton);
		Assert.Equal(new[] { "copy", "paste", "save", "library" }, asked);
	}

	[AvaloniaFact]
	public void BoxPolygonAndClearButtons()
	{
		using var r = new Run();
		Click(r.P.PolyButton);
		Assert.False(r.W.View.Area.Box);
		Assert.Empty(r.W.View.Area.Points);
		Click(r.P.BoxButton);
		Assert.True(r.W.View.Area.Box);
		r.Select(2, 2, 10, 10);
		Click(r.P.ClearButton);
		Assert.Empty(r.W.View.Area.Points);
		Assert.Equal("Selection cleared.", r.Said);
	}
}
