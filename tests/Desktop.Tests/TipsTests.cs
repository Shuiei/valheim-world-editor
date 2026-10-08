using System.Numerics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Every control explains itself when hovered, as the web editor's did (Tips): each button, switch,
// list, slider and text field of the editor (with every tool, Area action and Place mode, the Mask,
// the right-hand panels), the map page, the start page and Settings has a tip, shown or not yet (a
// hidden one shows in another state). The few without one are listed below with the reason.
[Collection("World files")]
public class TipsTests
{
	// Controls left without a tip, with the reason. None today: even a dialog's answers say what
	// they do and their key.
	private static bool Exempt(Control c) => false;

	// The controls shown without a tip, described so they can be found in the code.
	internal static List<string> Untipped(Control root)
	{
		var missing = new List<string>();
		foreach (var c in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
		{
			if (c is not (Button or ComboBox or Slider or TextBox or NumericUpDown) || Exempt(c))
			{
				continue;
			}
			if (ToolTip.GetTip(c) is null or "")
			{
				missing.Add(Describe(c));
			}
		}
		return missing;
	}

	private static string Text(object? o) => o switch
	{
		string s => s,
		TextBlock t => t.Text ?? "",
		Panel p => string.Join(" ", p.Children.Select(Text).Where(t => t.Length > 0)),
		ContentControl cc => Text(cc.Content),
		_ => "",
	};

	internal static string Describe(Control c)
	{
		string what = c switch
		{
			ContentControl cc => Text(cc.Content),
			TextBox t => t.PlaceholderText ?? t.Text ?? "",
			ComboBox b => b.SelectedItem?.ToString() ?? "",
			_ => "",
		};
		// The label beside it, when there is one.
		string label = "";
		if (c.Parent is Panel p)
		{
			int i = p.Children.IndexOf(c);
			label = string.Join(" | ", p.Children.Take(Math.Max(0, i)).OfType<TextBlock>().Select(t => t.Text));
		}
		return $"{c.GetType().Name} '{what}' (label: {label}) in {string.Join("/", c.GetLogicalAncestors().OfType<Control>().Take(4).Select(a => a.GetType().Name))}";
	}

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

	private static void Check(HashSet<string> missing, Control root)
	{
		Dispatcher.UIThread.RunJobs();
		foreach (var m in Untipped(root))
		{
			missing.Add(m);
		}
	}

	private static void NoneMissing(HashSet<string> missing) => Assert.True(missing.Count == 0, $"{missing.Count} control(s) without a tip:\n" + string.Join("\n", missing.OrderBy(m => m)));

	[AvaloniaFact]
	public void EveryControlOfTheEditorHasATip()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
		w.View.Show(s.Scene, null);
		w.Edit(s);
		var missing = new HashSet<string>();
		Check(missing, w);
		foreach (var t in Enum.GetValues<BrushTool>())
		{
			w.Tools.Choose(t);
			Check(missing, w);
		}
		w.MaskPanel.OnBox.IsChecked = true;
		Check(missing, w);
		foreach (var m in new[] { ToolMode.Measure, ToolMode.Shape, ToolMode.Path, ToolMode.View })
		{
			w.Tools.ChooseMode(m);
			Check(missing, w);
		}
		w.Tools.ChooseMode(ToolMode.Area);
		for (int i = 0; i < w.AreaPanel.ActionBox.ItemCount; i++)
		{
			w.AreaPanel.ActionBox.SelectedIndex = i;
			Check(missing, w);
		}
		w.Tools.ChooseMode(ToolMode.Place);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.AddRange(new[] { "Beech1", "woodwall" });
		foreach (var m in Enum.GetValues<PlaceTool.Modes>())
		{
			Click(w.PlacePanel.ModeButtons[m]);
			Check(missing, w);
		}
		Click(w.PlacePanel.KindsButton);
		// A starred kind: the favourites' chips.
		w.PlacePanel.Memory.ToggleFavourite("Beech1");
		Click(w.PlacePanel.KindsButton);
		Click(w.PlacePanel.KindsButton);
		Check(missing, w);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		Check(missing, w);
		w.Inspect();
		Check(missing, w);
		Click(w.AreaPanel.LibraryButton);
		Check(missing, w);
		foreach (var right in new Control?[] { w.History.Card, w.HelpCard, null })
		{
			w.ShowRight(right);
			Check(missing, w);
		}
		var box = new AreaTool { Soft = 0 };
		box.Points.Add(new Vector2(32, 32));
		box.Points.Add(new Vector2(48, 48));
		w.View.Paste.Clip = CopyData.FromArea(box, s.Ground, s.Scene, Array.Empty<int>(), _ => null);
		w.StartPaste();
		Check(missing, w);
		NoneMissing(missing);
	}

	[AvaloniaFact]
	public async Task EveryControlOfTheMapHasATip()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
			var missing = new HashSet<string>();
			Check(missing, w);
			w.MapPage!.Pick(0, 0);
			Check(missing, w);
			NoneMissing(missing);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, true);
		}
	}

	[AvaloniaFact]
	public void EveryControlOfTheStartPageHasATip()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		// A saved server: its row's fields and buttons.
		ServerConfig.Remember(new ServerConfig.Server { Name = "Tips", Host = "tips.example", User = "valheim" });
		w.ShowStart();
		var page = w.StartPage!;
		page.FillServers();
		var missing = new HashSet<string>();
		foreach (var mode in new[] { "game", "server", "offline" })
		{
			page.SetMode(mode);
			Check(missing, w);
		}
		page.SetMode("server");
		page.ServerForm.IsExpanded = true;
		Check(missing, w);
		ServerConfig.Forget("valheim@tips.example:22");
		NoneMissing(missing);
	}

	[AvaloniaFact]
	public void EveryControlOfSettingsHasATip()
	{
		var owner = new Window();
		owner.Show();
		var settings = new AppSettings { BepInExFolders = { "/b" }, WorldFolders = { "/w" } };
		_ = SettingsDialog.Show(owner, settings);
		Dispatcher.UIThread.RunJobs();
		var dialog = Assert.Single(owner.OwnedWindows);
		var missing = new HashSet<string>();
		Check(missing, dialog);
		dialog.Close();
		NoneMissing(missing);
	}

	[AvaloniaFact]
	public void TheTipsNameTheKeysAsTheHelpDoes()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		string Tip(Control c) => ToolTip.GetTip(c) as string ?? "";
		Assert.Contains("[ and ]", Tip(w.Tools.SizeSlider));
		Assert.Contains("[ and ]", Tip(w.PlacePanel.SizeSlider));
		Assert.Contains(", and .", Tip(w.PlacePanel.RotationSlider));
		Assert.Contains("(R)", Tip(w.PastePanel.TurnButton));
		Assert.Contains("(F)", Tip(w.PastePanel.MirrorButton));
		Assert.Contains("(Esc)", Tip(w.PastePanel.DoneButton));
		Assert.Contains("(L)", Tip(w.HistoryButton));
		Assert.Contains("(V)", Tip(w.ViewButton));
		Assert.Contains("(?)", Tip(w.HelpButton));
		Assert.Contains("(Ctrl+Z)", Tip(w.UndoButton));
		Assert.Contains("(Ctrl+S)", Tip(w.SaveButton));
		Assert.Contains("(Del)", Tip(w.SelectPanel.DeleteButton));
		Assert.Contains("(I)", Tip(w.SelectPanel.InspectButton));
		Assert.Contains("(Enter)", Tip(w.PathPanel.ApplyButton));
		Assert.Contains("(Esc)", Tip(w.PathPanel.ClearButton));
		Assert.Contains("(Ctrl+C)", Tip(w.AreaPanel.CopyButton));
		Assert.Contains("(Ctrl+V)", Tip(w.AreaPanel.PasteButton));
		Assert.Contains("(Backspace)", Tip(w.PlacePanel.UndoPointButton));
		Assert.Contains("(R)", Tip(w.PlacePanel.NewLayoutButton));
		Assert.Contains("PgUp / PgDn", Tip(w.PlacePanel.ElevationBox));
		Assert.Contains("Alt + click", Tip(w.Tools.TargetBox));
		Assert.Contains("Alt + Shift + click", Tip(w.MaskPanel.HeightMin));
		// The rail's buttons name their key.
		Assert.StartsWith("Raise (1)", Tip(w.Tools.ButtonOf(BrushTool.Raise)));
		Assert.StartsWith("Select (E)", Tip(w.Tools.SelectButton));
	}

	[AvaloniaFact]
	public void TheAreaActionsExplainThemselvesOnTheListAndTheButton()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var a = w.AreaPanel;
		var seen = new HashSet<string>();
		for (int i = 0; i < a.ActionBox.ItemCount; i++)
		{
			a.ActionBox.SelectedIndex = i;
			string button = (string)ToolTip.GetTip(a.ApplyButton)!;
			string list = (string)ToolTip.GetTip(a.ActionBox)!;
			Assert.EndsWith("(Enter). Ctrl+Z undoes it.", button);
			Assert.StartsWith(Tips.Of("area.action"), list);
			// The list says what the button does.
			Assert.Contains(button[..^" (Enter). Ctrl+Z undoes it.".Length], list);
			seen.Add(button);
		}
		// Each action its own words.
		Assert.Equal(a.ActionBox.ItemCount, seen.Count);
	}

	[AvaloniaFact]
	public void ARowsLabelTellsWhatItsControlDoes()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		foreach (var input in new Control[] { w.Tools.SizeSlider, w.PathPanel.WidthSlider, w.AreaPanel.SoftSlider, w.ShapePanel.RadiusSlider, w.PastePanel.GapBox, w.PlacePanel.DensitySlider, w.SelectPanel.XBox })
		{
			var row = (Panel)input.Parent!;
			var label = row.Children.OfType<TextBlock>().First();
			Assert.Equal(ToolTip.GetTip(input), ToolTip.GetTip(label));
		}
	}

	[Fact]
	public void EveryTextIsUsedAndNoneIsEmpty()
	{
		string desktop = Path.Combine(EditorProcess.Fixtures(), "..", "..", "Desktop");
		string code = string.Join("\n", Directory.GetFiles(desktop, "*.cs").Where(f => !f.EndsWith("Tips.cs")).Select(File.ReadAllText));
		// Keys made in code: the area actions, the area's moves, the start page's choices.
		bool Made(string k) => k.StartsWith("areaAct.") || k is "top.west" or "top.north" or "top.south" or "top.east" or "start.game" or "start.server" or "start.offline" || k.StartsWith("overlay.");
		var unused = Tips.Texts.Keys.Where(k => !Made(k) && !code.Contains($"\"{k}\"")).ToList();
		Assert.True(unused.Count == 0, "Tips no control uses: " + string.Join(", ", unused));
		Assert.All(Tips.Texts, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
		Assert.All(Enum.GetValues<AreaPanel.Act>(), act => Assert.False(string.IsNullOrWhiteSpace(Tips.Of($"areaAct.{act}"))));
		Assert.All(ObjectKinds.All, k => Assert.EndsWith(".", Tips.Kind(k)));
		Assert.All(Overlays.All, l => Assert.EndsWith(".", Tips.Overlay(l)));
		Assert.Throws<KeyNotFoundException>(() => Tips.Of("no.such.tip"));
	}
}
