using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Area tool's panel, like the web editor's (editor/area.js): the selection's shape and soft edge,
// one action at a time with only its own settings, and Apply (Enter). Ground actions change the ground
// inside; object actions remove, select or replace the objects of the chosen kinds inside; Reset zones
// hands the zones under the selection back to the world generator when saving.
public sealed class AreaPanel
{
	public enum Act { Flatten, Raise, Lower, Smooth, Natural, Restore, Erode, Paint, Remove, Select, Replace, Regrow, Copy, Heightmap, Backup, Reset }

	private static readonly (Act Act, string Label)[] Actions =
	{
		(Act.Flatten, "Flatten"), (Act.Raise, "Raise"), (Act.Lower, "Lower"), (Act.Smooth, "Smooth"), (Act.Natural, "Naturalize"),
		(Act.Restore, "Restore the ground"), (Act.Erode, "Erode"), (Act.Paint, "Paint"),
		(Act.Remove, "Remove objects"), (Act.Select, "Select objects"), (Act.Replace, "Replace objects"), (Act.Regrow, "Regrow nature"), (Act.Copy, "Copy and paste"), (Act.Heightmap, "Heightmap"),
		(Act.Backup, "Restore from a backup"), (Act.Reset, "Reset zones"),
	};

	private static readonly (string Label, BrushTool Tool)[] Paints =
	{
		("Dirt", BrushTool.PaintDirt), ("Cultivated", BrushTool.PaintCultivated), ("Paved", BrushTool.PaintPaved), ("Clear paint", BrushTool.PaintClear),
	};

	public Control Card { get; }
	public AreaTool Area { get; }
	private readonly GlView _view;
	private readonly Func<EditSession?> _session;
	private readonly Func<int, string?> _nameOf;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	public event Action<string>? Message;
	// Select objects: the window switches to the Select tool.
	public event Action? SwitchToSelect;

	internal Button BoxButton { get; } = new() { Content = "Box", FontSize = 12 };
	internal Button PolyButton { get; } = new() { Content = "Polygon", FontSize = 12 };
	internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
	internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal Slider SoftSlider { get; }
	internal ComboBox ActionBox { get; }
	internal NumericUpDown HeightBox { get; } = new() { Value = 35, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
	internal Button AverageButton { get; } = new() { Content = "avg", FontSize = 11, Padding = new Thickness(6, 2) };
	internal NumericUpDown AmountBox { get; } = new() { Value = 2, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
	internal ComboBox PaintBox { get; } = new() { ItemsSource = Paints.Select(p => p.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal TextBlock VolumeText { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal Dictionary<ObjectKind, ToggleButton> KindButtons { get; } = new();
	internal ComboBox FromBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal ComboBox ToBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
	// Eyedropper: the "with" kind picked from the world (the window takes the next click on an object).
	internal Button ToPickButton { get; } = new() { Content = "pick", FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(4, 0, 0, 0) };
	public event Action<string, Action<int>>? PickKindAsked;
	internal CheckBox KeepBuildingsBox { get; } = new() { Content = "Keep my buildings", IsChecked = true, FontSize = 12 };
	internal CheckBox ResetGroundBox { get; } = new() { Content = "Reset ground edits too", IsChecked = true, FontSize = 12 };
	internal Button UnresetButton { get; } = new() { Content = "Cancel reset", FontSize = 12 };
	internal Button ApplyButton { get; } = new Button { FontSize = 12 }.Classed("primary");
	private readonly Dictionary<Control, Act[]> _rows = new();
	private List<int> _creatable = new();

	public Act Current => Actions[Math.Max(0, ActionBox.SelectedIndex)].Act;

	internal void Choose(Act a) => ActionBox.SelectedIndex = Array.FindIndex(Actions, x => x.Act == a);

	internal ComboBox BackupBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal CheckBox BackupGroundBox { get; } = new() { Content = "Ground (height and paint)", IsChecked = true, FontSize = 12 };
	internal CheckBox BackupObjectsBox { get; } = new() { Content = "Objects of the kinds chosen above", IsChecked = true, FontSize = 12 };
	internal TextBlock BackupInfo { get; } = new()
	{
		FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
		Text = "Puts the selection back as it was in a backup: the editor's backups and the game's own are listed. Choose Your buildings above to bring buildings back too."
	};
	private List<string> _backupPaths = new();
	// Asks for another backup folder (the window's folder picker).
	internal Func<Task<string?>> PickFolder { get; set; } = () => Task.FromResult<string?>(null);
	private (string Path, WorldSave World, EditStore Edits)? _backup;

	// Copy and paste: the window copies, pastes and keeps blueprints.
	internal Button CopyButton { get; } = new() { Content = "Copy (Ctrl+C)", FontSize = 12 };
	internal Button PasteButton { get; } = new() { Content = "Paste (Ctrl+V)", FontSize = 12 };
	internal Button SaveBlueprintButton { get; } = new() { Content = "Save blueprint…", FontSize = 12 };
	internal Button LibraryButton { get; } = new() { Content = "Blueprints…", FontSize = 12 };
	internal TextBlock ClipInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	public event Action? CopyAsked, PasteAsked, SaveBlueprintAsked, LibraryAsked;

	// Heightmap: export the area, or import a picture into the selection (or the whole area).
	internal Button ExportButton { get; } = new() { Content = "Export the area", FontSize = 12 };
	internal Button ImportButton { get; } = new() { Content = "Import…", FontSize = 12 };
	internal NumericUpDown LowestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };
	internal NumericUpDown HighestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };
	internal Button PutButton { get; } = new Button { Content = "Put it into the ground", FontSize = 12 }.Classed("primary");
	internal Button CancelPictureButton { get; } = new() { Content = "Cancel", FontSize = 12 };
	internal TextBlock PictureInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private Control _pictureBox = null!;
	internal Func<Task<string?>> PickPicture { get; set; } = () => Task.FromResult<string?>(null);
	private (float[] Values, int X0, int Z0, int W, int H, List<(int G, float W)> Cells)? _picture;

	public AreaPanel(GlView view, Func<EditSession?> session, Func<int, string?> nameOf)
	{
		_view = view;
		_session = session;
		_nameOf = nameOf;
		Area = view.Area;
		ActionBox = new ComboBox { ItemsSource = Actions.Select(a => a.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 500 };
		var softV = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
		SoftSlider = new Slider { Minimum = 0, Maximum = 20, SmallChange = 0.5, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = Area.Soft };
		softV.Text = $"{Area.Soft:0.#} m";
		SoftSlider.ValueChanged += (_, e) => { Area.Soft = (float)e.NewValue; softV.Text = $"{e.NewValue:0.#} m"; Refresh(); };
		BoxButton.Click += (_, _) => { Area.Box = true; Area.Clear(); ShowShape(); };
		PolyButton.Click += (_, _) => { Area.Box = false; Area.Clear(); ShowShape(); };
		ClearButton.Click += (_, _) => { Area.Clear(); Message?.Invoke("Selection cleared."); };
		ActionBox.SelectionChanged += (_, _) => { ShowRows(); RefreshVolume(); if (Current == Act.Backup) FillBackups(); };
		BackupBox.SelectionChanged += async (_, _) =>
		{
			// The last entry: another folder.
			if (BackupBox.ItemCount > 0 && BackupBox.SelectedIndex == _backupPaths.Count)
			{
				if (await PickFolder() is string path)
				{
					_backupPaths.Add(path);
					FillBackups(path);
				}
				else
				{
					BackupBox.SelectedIndex = -1;
				}
			}
		};
		HeightBox.ValueChanged += (_, _) => Refresh();
		AmountBox.ValueChanged += (_, _) => Refresh();
		AverageButton.Click += (_, _) =>
		{
			if (_session()?.Ground is { } g && Area.Average(g) is float avg)
			{
				HeightBox.Value = (decimal)MathF.Round(avg, 1);
			}
		};
		AverageButton.Tip("area.avg");
		ActionBox.Tip("area.action");
		BoxButton.Tip("area.box");
		PolyButton.Tip("area.poly");
		ClearButton.Tip("area.clear");
		SoftSlider.Tip("area.soft");
		HeightBox.Tip("area.height");
		AmountBox.Tip("area.amount");
		PaintBox.Tip("area.paint");
		FromBox.Tip("area.from");
		ToBox.Tip("area.to");
		KeepBuildingsBox.Tip("area.keepBuildings");
		ResetGroundBox.Tip("area.resetGround");
		UnresetButton.Tip("area.unreset");
		BackupBox.Tip("area.backup");
		BackupGroundBox.Tip("area.backupGround");
		BackupObjectsBox.Tip("area.backupObjects");
		CopyButton.Tip("area.copy");
		PasteButton.Tip("area.paste");
		SaveBlueprintButton.Tip("area.saveBlueprint");
		LibraryButton.Tip("area.library");
		LowestBox.Tip("area.lowest");
		HighestBox.Tip("area.highest");
		PutButton.Tip("area.put");
		CancelPictureButton.Tip("area.cancelPicture");
		var kinds = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
		foreach (var k in ObjectKinds.All)
		{
			var b = new ToggleButton { Content = ObjectKinds.Label(k), IsChecked = Area.Kinds.Contains(k), FontSize = 11, Padding = new Thickness(6, 2) };
			ToolTip.SetTip(b, $"{Tips.Kind(k)} {Tips.Of("area.kind")}");
			b.IsCheckedChanged += (_, _) => { if (b.IsChecked == true) Area.Kinds.Add(k); else Area.Kinds.Remove(k); };
			KindButtons[k] = b;
			kinds.Children.Add(b);
		}
		ApplyButton.Click += async (_, _) => await Apply();
		CopyButton.Click += (_, _) => CopyAsked?.Invoke();
		PasteButton.Click += (_, _) => PasteAsked?.Invoke();
		SaveBlueprintButton.Click += (_, _) => SaveBlueprintAsked?.Invoke();
		LibraryButton.Click += (_, _) => LibraryAsked?.Invoke();
		ClipInfo.Text = "Copies the ground (shape and paint) and the shown objects inside the selection.";
		ExportButton.Click += (_, _) => Message?.Invoke(ExportHeightmap() ?? "");
		ExportButton.Tip("area.export");
		ImportButton.Click += async (_, _) => { if (await PickPicture() is string path) LoadPicture(path); };
		ImportButton.Tip("area.import");
		CancelPictureButton.Click += (_, _) => { _picture = null; _pictureBox.IsVisible = false; };
		PutButton.Click += (_, _) => PutPicture();
		UnresetButton.Click += (_, _) => CancelReset();
		ToPickButton.Tip("area.pick");
		ToPickButton.Click += (_, _) => PickKindAsked?.Invoke("Replace", UsePicked);
		Area.Changed += Refresh;

		Control Row(string label, Control input, Control? after = null)
		{
			var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,Auto") };
			var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
			Tips.Label(l, input);
			g.Children.Add(l);
			Grid.SetColumn(input, 1);
			g.Children.Add(input);
			if (after != null)
			{
				Grid.SetColumn(after, 2);
				g.Children.Add(after);
			}
			return g;
		}
		Control For(Control c, params Act[] acts)
		{
			_rows[c] = acts;
			return c;
		}
		TextBlock Help(string t) => new() { Text = t, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Width = 310,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Area", FontSize = 14, FontWeight = FontWeight.SemiBold },
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { BoxButton, PolyButton, ClearButton } },
					Info,
					Row("Soft edge", SoftSlider, softV),
					Row("Action", ActionBox),
					For(Row("Height", HeightBox, AverageButton), Act.Flatten),
					For(Row("Amount", AmountBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }), Act.Raise, Act.Lower),
					For(Row("Paint", PaintBox), Act.Paint),
					For(VolumeText, Act.Flatten, Act.Raise, Act.Lower),
					For(Help("Evens out bumps inside the selection."), Act.Smooth),
					For(Help("Natural-looking bumps, with the Bumps and Size of the Naturalize brush."), Act.Natural),
					For(Help("Back to the ground the game generated, paint removed."), Act.Restore),
					For(Help("Weathers the ground inside: slopes settle and rain cuts gullies (rest angle from the Erode brush)."), Act.Erode),
					For(kinds, Act.Remove, Act.Select, Act.Replace, Act.Regrow, Act.Backup),
					For(Row("Replace", FromBox), Act.Replace),
					For(Row("with", ToBox, ToPickButton), Act.Replace),
					For(Help("Puts back what the game grows here: its own trees, rocks, bushes and pickables for the biome, by its vegetation rules, for the kinds chosen above."), Act.Regrow),
					For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { CopyButton, PasteButton } }, Act.Copy),
					For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SaveBlueprintButton, LibraryButton } }, Act.Copy),
					For(ClipInfo, Act.Copy),
					For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ExportButton, ImportButton } }, Act.Heightmap),
					For(_pictureBox = new StackPanel
					{
						Spacing = 4,
						IsVisible = false,
						Children =
						{
							PictureInfo,
							Row("Lowest", LowestBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
							Row("Highest", HighestBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
							new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PutButton, CancelPictureButton } },
						},
					}, Act.Heightmap),
					For(Help("Black is the lowest height, white the highest; north is at the top of the picture, one pixel per metre when exported. An import fits the picture to the selection (or the whole area) and works within the game's ±8 m."), Act.Heightmap),
					For(Row("Backup", BackupBox), Act.Backup),
					For(BackupGroundBox, Act.Backup),
					For(BackupObjectsBox, Act.Backup),
					For(BackupInfo, Act.Backup),
					For(KeepBuildingsBox, Act.Reset),
					For(ResetGroundBox, Act.Reset),
					For(Help("On save, the zones under the selection lose their trees, rocks, ruins and dungeon entrances, and the game generates them again the next time someone goes there."), Act.Reset),
					For(UnresetButton, Act.Reset),
					ApplyButton,
				},
			},
		};
		ShowShape();
		ShowRows();
	}


	private void ShowShape()
	{
		BoxButton.Classes.Set("on", Area.Box);
		PolyButton.Classes.Set("on", !(Area.Box));
		Refresh();
	}

	private void ShowRows()
	{
		var act = Current;
		foreach (var (row, acts) in _rows)
		{
			row.IsVisible = acts.Contains(act);
		}
		ApplyButton.IsVisible = act is not (Act.Heightmap or Act.Copy);
		// The action's own explanation, on the list and on its button.
		string what = Tips.Of($"areaAct.{act}");
		ToolTip.SetTip(ActionBox, $"{Tips.Of("area.action")} {what}");
		ToolTip.SetTip(ApplyButton, $"{what} (Enter). Ctrl+Z undoes it.");
		ApplyButton.Content = act switch
		{
			Act.Remove => "Remove the objects (Enter)",
			Act.Select => "Select the objects (Enter)",
			Act.Replace => "Replace (Enter)",
			Act.Reset => "Reset zones… (Enter)",
			Act.Regrow => "Regrow nature (Enter)",
			Act.Backup => "Restore the selection (Enter)",
			Act.Heightmap or Act.Copy => "",
			_ => $"{Actions.First(a => a.Act == act).Label} (Enter)",
		};
		Refresh();
	}

	private static float Value(NumericUpDown b) => (float)(b.Value ?? 0);

	// The things inside the selection (indices), not deleted, of shown kinds (or added in this session).
	// includeHidden: also kinds switched off in View (a backup restore compares with everything there).
	internal List<int> ThingsInside(List<Vector2> poly, bool chosenKindsOnly, bool includeHidden = false)
	{
		var out_ = new List<int>();
		if (_view.Scene is not { } s)
		{
			return out_;
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		lock (s.Things)
		{
			for (int i = 0; i < s.Things.Count; i++)
			{
				var t = s.Things[i];
				if (t.Gone || !AreaTool.Inside(poly, t.Position.X - ox, t.Position.Z - oz))
				{
					continue;
				}
				var k = KindOf(t);
				if ((includeHidden || t.Id < 0 || _view.IsShown(k)) && (!chosenKindsOnly || Area.Kinds.Contains(k) && MaskAt(t.Position.X - ox, t.Position.Z - oz)))
				{
					out_.Add(i);
				}
			}
		}
		return out_;
	}

	// Whether the Mask lets the ground at a grid point through (the object actions pick only there).
	private bool MaskAt(float gx, float gz)
	{
		if (_session() is not { } s || s.MaskNow() is not { } m)
		{
			return true;
		}
		int x = Math.Clamp((int)MathF.Round(gx), 0, s.Ground.W - 1), z = Math.Clamp((int)MathF.Round(gz), 0, s.Ground.H - 1);
		return m(z * s.Ground.W + x) > 0;
	}

	private ObjectKind KindOf(WorldScene.Thing t) => ObjectKinds.Of(_nameOf(t.Prefab), t.Piece, t.Tamed);
	private string NameOf(int prefab) => _nameOf(prefab) ?? prefab.ToString();

	// The selection changed: its size, what is inside, the lists and the volumes.
	// Cut and fill: what Flatten, Raise or Lower would move, and how much the ground inside was raised
	// and dug since generated (again after every change of the ground, as the web editor did).
	public void RefreshVolume()
	{
		var g = _session()?.Ground;
		VolumeText.Text = g != null ? Area.Volume(g, Ground(Current) ?? AreaTool.GroundAction.Flatten, Value(HeightBox), Value(AmountBox), _session()?.MaskNow()) : "";
	}

	public void Refresh()
	{
		var poly = Area.Polygon();
		RefreshVolume();
		if (poly == null)
		{
			Info.Text = Area.Box ? "Drag on the ground to select a box." : "Click points around the area; double-click or Enter closes it. Backspace removes a point, Esc clears.";
			FromBox.ItemsSource = Array.Empty<string>();
			foreach (var (k, b) in KindButtons)
			{
				b.Content = ObjectKinds.Label(k);
			}
			_view.LassoChanged();
			return;
		}
		var inside = ThingsInside(poly, chosenKindsOnly: false);
		var s = _view.Scene!;
		Info.Text = $"{AreaTool.Area(poly):0} m² selected · {inside.Count} shown object(s) inside. Click outside to start a new selection.";
		var byKind = inside.GroupBy(i => KindOf(s.Things[i])).ToDictionary(x => x.Key, x => x.Count());
		foreach (var (k, b) in KindButtons)
		{
			b.Content = $"{ObjectKinds.Label(k)} {(_view.IsShown(k) ? byKind.GetValueOrDefault(k).ToString() : "hidden")}";
		}
		var keep = FromBox.SelectedItem as string;
		var names = inside.GroupBy(i => NameOf(s.Things[i].Prefab)).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} ({x.Count()})").ToList();
		FromBox.ItemsSource = names;
		FromBox.SelectedIndex = keep != null && names.Contains(keep) ? names.IndexOf(keep) : names.Count > 0 ? 0 : -1;
		FillCreatable();
		_view.LassoChanged();
	}

	// The kinds the world can make, for the "with" list (once: they are the game's).
	private void FillCreatable()
	{
		if (_creatable.Count == 0 && _view.Scene?.World is { } w)
		{
			_creatable = w.Creatable.Where(p => _nameOf(p) != null).OrderBy(p => _nameOf(p), StringComparer.OrdinalIgnoreCase).ToList();
			ToBox.ItemsSource = _creatable.Select(p => _nameOf(p)!).ToList();
		}
	}

	private static AreaTool.GroundAction? Ground(Act a) => a switch
	{
		Act.Flatten => AreaTool.GroundAction.Flatten,
		Act.Raise => AreaTool.GroundAction.Raise,
		Act.Lower => AreaTool.GroundAction.Lower,
		Act.Smooth => AreaTool.GroundAction.Smooth,
		Act.Natural => AreaTool.GroundAction.Natural,
		Act.Restore => AreaTool.GroundAction.Restore,
		Act.Erode => AreaTool.GroundAction.Erode,
		Act.Paint => AreaTool.GroundAction.Paint,
		_ => null,
	};

	// The action (Apply, Enter).
	public async Task Apply()
	{
		if (_session() is not { } session)
		{
			return;
		}
		var poly = Area.Polygon();
		if (poly == null)
		{
			Message?.Invoke("Select an area first.");
			return;
		}
		var act = Current;
		if (Ground(act) is { } ga)
		{
			var paint = Brush.PaintOf(Paints[Math.Max(0, PaintBox.SelectedIndex)].Tool)!;
			string name = act == Act.Paint ? $"Paint {Paints[Math.Max(0, PaintBox.SelectedIndex)].Label.ToLowerInvariant()}" : AreaTool.Label(ga);
			var touched = session.EditGround($"Area: {name}", g => Area.Apply(g, session.Brush, ga, Value(HeightBox), Value(AmountBox), paint, session.MaskNow()));
			Message?.Invoke($"{name} applied to {touched.Count} point(s).");
			Refresh();
			return;
		}
		switch (act)
		{
			case Act.Remove:
			{
				var list = ThingsInside(poly, chosenKindsOnly: true);
				if (list.Count == 0)
				{
					Message?.Invoke("No objects of the chosen kinds inside the selection.");
					return;
				}
				session.Commit($"Area: removed {list.Count} object(s)", null, list, Array.Empty<(NewObject, bool)>());
				Message?.Invoke($"Removed {list.Count} object(s). Ctrl+Z brings them back.");
				break;
			}
			case Act.Select:
				_view.Select(ThingsInside(poly, chosenKindsOnly: true));
				SwitchToSelect?.Invoke();
				break;
			case Act.Replace:
			{
				var s = _view.Scene!;
				string? from = (FromBox.SelectedItem as string)?.Split(" (")[0];
				int to = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
				if (from == null || to == 0)
				{
					Message?.Invoke("Choose what to replace, and with what.");
					return;
				}
				var list = ThingsInside(poly, chosenKindsOnly: false).Where(i => NameOf(s.Things[i].Prefab) == from).ToList();
				Replace(session, list, to);
				break;
			}
			case Act.Reset:
				await ResetZones(session);
				break;
			case Act.Regrow:
				await RegrowNature(session, poly);
				break;
			case Act.Backup:
				await RestoreBackup(session, poly);
				break;
		}
		Refresh();
	}

	// The eyedropper's kind into the "with" list, when the game can make it.
	internal void UsePicked(int prefab)
	{
		FillCreatable();
		int i = _creatable.IndexOf(prefab);
		if (i < 0)
		{
			Message?.Invoke($"{NameOf(prefab)} cannot be placed: the game has no such kind to copy.");
			return;
		}
		ToBox.SelectedIndex = i;
		Message?.Invoke($"Replace with {NameOf(prefab)}.");
	}

	// Replaces things with objects of another kind, where they are and turned as they are.
	public void Replace(EditSession session, IReadOnlyList<int> things, int prefab)
	{
		var s = session.Scene;
		bool piece = PieceCatalog.Get(prefab)?.Tool != null;
		var adds = things.Select(i => s.Things[i]).Select(t => (new NewObject(0, prefab, t.Position, t.Rotation, 0), piece)).ToList();
		session.Commit($"Replaced {things.Count} with {NameOf(prefab)}", null, things, adds);
		Message?.Invoke($"Replaced {things.Count} object(s) with {NameOf(prefab)}.");
	}

	// ---- Heightmaps: the area's ground as a 16-bit picture (with its lowest and highest heights), and a
	// picture back into the ground, fitted to the selection or the whole area.
	// Where Export writes (null: the heightmaps folder in the app's data folder). Tests use their own.
	internal string? HeightmapFolder { get; set; }

	internal string? ExportHeightmap(string? folder = null)
	{
		if (_view.Scene is not { World: { } world } s)
		{
			return null;
		}
		int x1 = s.X0 + s.Size - 1, z1 = s.Z0 + s.Size - 1;
		byte[] png = Heightmaps.Encode(s.W, s.H, s.Heights, $"{world.Name} zones {s.X0},{s.Z0} to {x1},{z1}", out float min, out float max);
		string dir = folder ?? HeightmapFolder ?? Path.Combine(AppSettings.DataDir, "heightmaps");
		Directory.CreateDirectory(dir);
		string safe = string.Concat(world.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
		string path = Path.Combine(dir, $"{safe}_{s.X0}_{s.Z0}_{x1}_{z1}.png");
		File.WriteAllBytes(path, png);
		return $"Heightmap written to {path} ({s.W} × {s.H}, {min:0.0} to {max:0.0} m).";
	}

	internal void LoadPicture(string path)
	{
		if (_session() is not { } session)
		{
			return;
		}
		var g = session.Ground;
		// Where it goes: the selection's box, or the whole area (as far as the Mask lets it).
		List<(int G, float W)> cells;
		int x0, z0, w, h;
		var mask = session.MaskNow();
		if (Area.WeightsIn(g.W, g.H, mask) is { } a)
		{
			(cells, x0, z0, w, h) = (a.Cells, a.X0, a.Z0, a.X1 - a.X0 + 1, a.Z1 - a.Z0 + 1);
		}
		else
		{
			cells = Enumerable.Range(0, g.W * g.H).Select(p => (p, mask?.Invoke(p) ?? 1f)).Where(c => c.Item2 > 0).ToList();
			(x0, z0, w, h) = (0, 0, g.W, g.H);
		}
		Heightmaps.Picture pic;
		try
		{
			pic = Heightmaps.Read(File.ReadAllBytes(path), w, h);
		}
		catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
		{
			Message?.Invoke($"That picture cannot be used: {ex.Message}");
			return;
		}
		_picture = (pic.Values, x0, z0, w, h, cells);
		// The heights written in the picture by the editor, else the range of the ground it covers now.
		float lo = cells.Count > 0 ? cells.Min(c => g.HeightOf(c.G)) : 0, hi = cells.Count > 0 ? cells.Max(c => g.HeightOf(c.G)) : 0;
		LowestBox.Value = (decimal)(pic.Min is float mn ? MathF.Round(mn, 3) : MathF.Round(lo, 1));
		HighestBox.Value = (decimal)(pic.Max is float mx ? MathF.Round(mx, 3) : MathF.Round(hi, 1));
		PictureInfo.Text = $"Picture {pic.Width} × {pic.Height} fitted to {w} × {h} m ({(Area.Polygon() != null ? "the selection" : "the whole area")}){(pic.Min != null ? ", with the heights it was exported with" : "")}.";
		_pictureBox.IsVisible = true;
	}

	internal void PutPicture()
	{
		if (_picture is not { } p || _session() is not { } session)
		{
			return;
		}
		float lo = Value(LowestBox), hi = Value(HighestBox);
		int clamped = 0;
		var touched = session.EditGround("Heightmap import", g =>
		{
			var list = new List<int>();
			foreach (var (pt, w) in p.Cells)
			{
				int gx = pt % g.W, gz = pt / g.W, ix = gx - p.X0, iz = gz - p.Z0;
				if (g.Locked(gx, gz) || ix < 0 || iz < 0 || ix >= p.W || iz >= p.H)
				{
					continue;
				}
				float want = lo + p.Values[iz * p.W + ix] * (hi - lo), h = g.HeightOf(pt);
				g.SetHeight(pt, h + (want - h) * w);
				if (w > 0.999f && MathF.Abs(g.HeightOf(pt) - want) > 0.05f)
				{
					clamped++;
				}
				list.Add(pt);
			}
			return (list, (0, 0, g.W - 1, g.H - 1));
		});
		_picture = null;
		_pictureBox.IsVisible = false;
		Message?.Invoke(clamped > 0 ? $"Imported, but {clamped} point(s) could not reach their height: the game keeps the ground within ±8 m of the original (red points)."
			: $"Imported the heightmap into {touched.Count} point(s). Ctrl+Z undoes it.");
	}

	// ---- Regrow nature: the game's own vegetation for the zones under the selection (by its rules, on
	// the ground as it is now), kept inside the selection, for the chosen kinds, and not where something
	// already stands (a tree still there is not doubled) or near buildings.
	private async Task RegrowNature(EditSession session, List<Vector2> poly)
	{
		var s = session.Scene;
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		var adds = await Growth.Spots(session, poly.Min(p => p.X) + ox, poly.Min(p => p.Y) + oz, poly.Max(p => p.X) + ox, poly.Max(p => p.Y) + oz,
			o => AreaTool.Inside(poly, o.X - ox, o.Z - oz) && Area.Kinds.Contains(ObjectKinds.Of(o.Name, false)) && MaskAt(o.X - ox, o.Z - oz), _nameOf, m => Message?.Invoke(m));
		if (adds == null)
		{
			return;
		}
		if (adds.Count == 0)
		{
			Message?.Invoke("Nothing to regrow: the game grows none of the chosen kinds here, or it is all still standing.");
			return;
		}
		Growth.Commit(session, $"Area: regrew {adds.Count} object(s)", adds);
		var names = adds.Select(o => o.Name).Distinct().ToList();
		Message?.Invoke($"Regrew {adds.Count} object(s): {string.Join(", ", names.Take(6))}{(names.Count > 6 ? "…" : "")}. Ctrl+Z takes them back; Save writes them.");
	}

	// ---- Restore from a backup (WorldEdit's //restore): the ground and objects inside the selection as
	// they were in a backup of this world. Objects unchanged since then are left alone.
	private void FillBackups(string? select = null)
	{
		var s = _view.Scene;
		var keep = select ?? (BackupBox.SelectedIndex >= 0 && BackupBox.SelectedIndex < _backupPaths.Count ? _backupPaths[BackupBox.SelectedIndex] : null);
		// A world without a folder (live from a game) has no backups of its own; folders chosen by hand stay.
		var found = s?.World != null && !string.IsNullOrEmpty(s.World.Directory) && Directory.Exists(s.World.Directory) ? Backups.Find(s.World.Directory) : new List<Backups.Info>();
		var extra = _backupPaths.Where(p => !found.Any(f => f.Path == p)).ToList();
		_backupPaths = found.Select(b => b.Path).Concat(extra).ToList();
		// The list keeps the selected text across a new source: with "Another folder…" still selected it
		// would ask for a folder again.
		BackupBox.SelectedIndex = -1;
		BackupBox.ItemsSource = found.Select(b => $"{(b.Kind == "game" ? "Game" : "Editor")} · {b.Date:g}").Concat(extra).Append("Another folder…").ToList();
		BackupBox.SelectedIndex = keep != null ? _backupPaths.IndexOf(keep) : -1;
	}

	// A backup's world (read once; the last one is kept, and only ever used for a world of its seed: a
	// folder chosen by hand stays in the list when another world is opened).
	internal async Task<(WorldSave World, EditStore Edits)?> OpenBackup(string path, WorldSave current)
	{
		if (_backup is { } b && b.Path == path && b.World.Seed == current.Seed && b.World.SeedName == current.SeedName)
		{
			return (b.World, b.Edits);
		}
		BackupInfo.Text = "Reading the backup…";
		try
		{
			var w = await Task.Run(() => WorldSave.Load(path));
			if (w.Seed != current.Seed || w.SeedName != current.SeedName)
			{
				BackupInfo.Text = $"That is another world (seed {w.SeedName}, this one is {current.SeedName}).";
				return null;
			}
			var edits = new EditStore(w);
			_backup = (path, w, edits);
			BackupInfo.Text = $"Backup: save #{w.SaveNumber} of {w.Name}, {w.ObjectCount:N0} objects ({Directory.GetLastWriteTime(path):g}).";
			return (w, edits);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			BackupInfo.Text = $"That folder is not a world save that can be read: {ex.Message}";
			return null;
		}
	}

	internal async Task RestoreBackup(EditSession session, List<Vector2> poly, string? path = null)
	{
		var s = session.Scene;
		path ??= BackupBox.SelectedIndex >= 0 && BackupBox.SelectedIndex < _backupPaths.Count ? _backupPaths[BackupBox.SelectedIndex] : null;
		if (path == null)
		{
			Message?.Invoke("Choose a backup first.");
			return;
		}
		if (await OpenBackup(path, s.World) is not var (bw, be))
		{
			Message?.Invoke(BackupInfo.Text ?? "");
			return;
		}
		int groundPoints = 0;
		Func<Ground, (List<int>, (int, int, int, int))>? ground = BackupGroundBox.IsChecked != true ? null : g =>
		{
			// The backup's ground edits over the block.
			var bk = new Ground(g.W, g.H, g.X0, g.Z0, g.Size);
			Array.Copy(g.Base, bk.Base, g.Base.Length);
			bk.TakeEdits(be);
			var touched = new List<int>();
			if (Area.WeightsIn(g.W, g.H, session.MaskNow()) is not { } a)
			{
				return (touched, (0, 0, 0, 0));
			}
			foreach (var (p, w) in a.Cells)
			{
				if (g.Locked(p % g.W, p / g.W))
				{
					continue;
				}
				bool same = g.Mod[p] == bk.Mod[p] && g.PMod[p] == bk.PMod[p] && MathF.Abs(g.HeightOf(p) - bk.HeightOf(p)) < 1e-4f && MathF.Abs(g.Original(p) - bk.Original(p)) < 1e-4f
					&& (g.PMod[p] == 0 || Enumerable.Range(0, 4).All(c => MathF.Abs(g.Paint[p * 4 + c] - bk.Paint[p * 4 + c]) < 1e-4f));
				if (same)
				{
					continue;
				}
				if (w > 0.999f)
				{
					// Inside the soft edge: exactly as in the backup.
					g.Mod[p] = bk.Mod[p];
					g.Level[p] = bk.Level[p];
					g.Smooth[p] = bk.Smooth[p];
					g.PMod[p] = bk.PMod[p];
					Array.Copy(bk.Paint, p * 4, g.Paint, p * 4, 4);
					// The backup's original ground (its own ground discs) as a lift on this one's.
					g.Lift[p] = bk.Original(p) - g.Base[p];
				}
				else
				{
					float h = g.HeightOf(p);
					g.SetHeight(p, h + (bk.HeightOf(p) - h) * w);
					if (bk.PMod[p] != 0 || g.PMod[p] != 0)
					{
						if (g.PMod[p] == 0)
						{
							g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
							g.Paint[p * 4 + 3] = 1;
							g.PMod[p] = 1;
						}
						for (int c = 0; c < 4; c++)
						{
							float target = bk.PMod[p] != 0 ? bk.Paint[p * 4 + c] : c == 3 ? 1 : 0;
							g.Paint[p * 4 + c] += (target - g.Paint[p * 4 + c]) * w;
						}
					}
				}
				touched.Add(p);
			}
			groundPoints = touched.Count;
			return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1));
		};
		var remove = new List<int>();
		var adds = new List<(NewObject, bool)>();
		if (BackupObjectsBox.IsChecked == true)
		{
			float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
			// Runestones are left as they are on both sides (a location proxy cannot be copied back).
			var wanted = WorldScene.ReadThings(bw, s.X0, s.Z0, s.Size, new HashSet<int>())
				.Where(o => !o.Runestone && AreaTool.Inside(poly, o.Position.X - ox, o.Position.Z - oz) && Area.Kinds.Contains(KindOf(o))).ToList();
			var current = ThingsInside(poly, chosenKindsOnly: true, includeHidden: true).Where(i => !s.Things[i].Runestone).ToList();
			// Unchanged since the backup (same kind, place and facing): kept as they are.
			bool Same(WorldScene.Thing r, WorldScene.Thing o) => r.Prefab == o.Prefab && MathF.Abs(r.Position.X - o.Position.X) < 0.01f
				&& MathF.Abs(r.Position.Y - o.Position.Y) < 0.01f && MathF.Abs(r.Position.Z - o.Position.Z) < 0.01f
				&& MathF.Abs(((r.Rotation.Y - o.Rotation.Y) % 360 + 540) % 360 - 180) < 0.6f;
			var kept = new HashSet<int>();
			foreach (var o in wanted)
			{
				int r = current.FirstOrDefault(i => !kept.Contains(i) && Same(s.Things[i], o), -1);
				if (r >= 0)
				{
					kept.Add(r);
					continue;
				}
				var raw = bw.ObjectBytes(o.Id);
				var z = ZdoData.Parse(raw);
				adds.Add((new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, raw), o.Piece));
			}
			remove = current.Where(i => !kept.Contains(i)).ToList();
		}
		if (ground == null && remove.Count == 0 && adds.Count == 0)
		{
			Message?.Invoke("Nothing to restore: the selection is as it was in the backup.");
			return;
		}
		var added = session.Commit("Restored from backup", ground, remove, adds);
		if (groundPoints == 0 && remove.Count == 0 && added.Count == 0)
		{
			Message?.Invoke("Nothing to restore: the selection is as it was in the backup.");
			return;
		}
		Message?.Invoke($"Restored from the backup: {(groundPoints > 0 ? "the ground, " : "")}{added.Count} object(s) brought back, {remove.Count} removed. Ctrl+Z undoes it.");
	}

	private async Task ResetZones(EditSession session)
	{
		var zones = Area.ZonesUnder(session.Ground);
		if (zones.Count == 0)
		{
			Message?.Invoke("Select an area first.");
			return;
		}
		bool keep = KeepBuildingsBox.IsChecked == true, ground = ResetGroundBox.IsChecked == true;
		if (!await Confirm($"Reset {zones.Count} zone(s) ({string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"))}) when you save?\n\n"
			+ $"• Every tree, rock, ruin and dungeon entrance in them is removed{(keep ? " (your buildings stay)" : ", your buildings too")}.\n"
			+ (ground ? "• Their ground edits are undone.\n" : "") + "• Valheim generates the zones again the next time a player goes there."))
		{
			return;
		}
		var marked = session.Resets.Select(r => (r.X, r.Z)).ToHashSet();
		var resets = zones.Select(z => (new ZoneReset(z.X, z.Z, keep, ground), marked.Contains(z), true)).ToArray();
		Func<Ground, (List<int>, (int, int, int, int))>? clearGround = !ground ? null : g =>
		{
			var touched = new List<int>();
			foreach (var (zx, zz) in zones)
			{
				int gx0 = (zx - g.X0) * 64, gz0 = (zz - g.Z0) * 64;
				for (int k = 0; k <= 64; k++)
				{
					for (int l = 0; l <= 64; l++)
					{
						int gx = gx0 + l, gz = gz0 + k, p = gz * g.W + gx;
						if (gx < g.W && gz < g.H && (g.Mod[p] != 0 || g.PMod[p] != 0 || g.Lift[p] != 0))
						{
							g.Mod[p] = g.PMod[p] = 0;
							g.Level[p] = g.Smooth[p] = g.Lift[p] = 0;
							touched.Add(p);
						}
					}
				}
			}
			return (touched, (0, 0, g.W - 1, g.H - 1));
		};
		session.Commit($"Reset {zones.Count} zone(s)", clearGround, Array.Empty<int>(), Array.Empty<(NewObject, bool)>(), resets);
		Message?.Invoke($"{zones.Count} zone(s) marked for reset (red outline). Save to apply; Ctrl+Z cancels.");
	}

	private void CancelReset()
	{
		if (_session() is not { } session)
		{
			return;
		}
		var marked = session.Resets.ToDictionary(r => (r.X, r.Z));
		var zones = Area.ZonesUnder(session.Ground).Where(marked.ContainsKey).ToList();
		if (zones.Count == 0)
		{
			Message?.Invoke("No zone marked for reset under the selection.");
			return;
		}
		session.Commit($"Cancelled reset of {zones.Count} zone(s)", null, Array.Empty<int>(), Array.Empty<(NewObject, bool)>(),
			zones.Select(z => (marked[z], true, false)).ToArray());
		Message?.Invoke($"Reset cancelled for {zones.Count} zone(s).");
	}
}
