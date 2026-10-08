using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The Place tool's panel, like the web editor's: the mode, a preset, the kinds (with weights), and the
// settings of the mode; the kinds to choose from open in a list beside it.
public sealed class PlacePanel
{
	public Control Card { get; }
	public Control Chooser { get; }
	private readonly PlaceInput _input;
	private PlaceTool T => _input.Tool;
	private readonly Func<WorldScene?> _scene;
	private readonly Func<int, string?> _nameOf;
	private bool _filling;

	internal Dictionary<PlaceTool.Modes, Button> ModeButtons { get; } = new();
	internal Dictionary<PlaceTool.LineShapes, Button> ShapeButtons { get; } = new();
	internal ComboBox PresetBox { get; }
	internal Button SavePresetButton { get; } = new() { Content = "Save as preset…", FontSize = 12 };
	internal Button DeletePresetButton { get; } = new() { Content = "Delete", FontSize = 12, IsEnabled = false };
	internal WrapPanel Favourites { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
	internal WrapPanel Recent { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
	private readonly Control _favBox, _recentBox;
	public PlaceMemory Memory { get; }
	// Asks for a preset's name, and whether to delete one (the window's dialogs; replaced by tests).
	internal Func<Task<string?>> AskName { get; set; } = () => Task.FromResult<string?>(null);
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	internal StackPanel Mix { get; } = new() { Spacing = 3 };
	internal Button KindsButton { get; } = new() { Content = "+ Add kinds", FontSize = 12 };
	internal TextBox Search { get; } = new() { Watermark = "Search kinds (oak, rock, bush…)", FontSize = 12 };
	internal StackPanel List { get; } = new() { Spacing = 1 };
	internal Button PickButton { get; } = new() { Content = "Pick from world", FontSize = 12 };
	internal ComboBox ElevationBox { get; } = new() { ItemsSource = new[] { "On the ground", "Above the ground", "At one height" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal NumericUpDown ElevBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
	internal CheckBox SnapToBox { get; } = new() { Content = "Snap to pieces already there", IsChecked = true, FontSize = 12 };
	internal Button BesideButton { get; } = new() { Content = "Beside", FontSize = 12 };
	internal Button OnTopButton { get; } = new() { Content = "On top", FontSize = 12 };
	internal CheckBox GrowBox { get; } = new() { Content = "Leave saplings and crops room to grow", IsChecked = true, FontSize = 12 };
	internal TextBlock Note { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 190, 90)), TextWrapping = TextWrapping.Wrap };
	internal Slider SizeSlider { get; private set; } = null!;
	internal Slider DensitySlider { get; private set; } = null!;
	internal Slider SpacingSlider { get; private set; } = null!;
	internal Slider ClumpSlider { get; private set; } = null!;
	internal Slider PatchSlider { get; private set; } = null!;
	internal NumericUpDown SizeMinBox { get; } = new() { Value = 80, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };
	internal NumericUpDown SizeMaxBox { get; } = new() { Value = 120, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };
	internal Slider TiltSlider { get; private set; } = null!;
	internal Slider RotationSlider { get; private set; } = null!;
	internal CheckBox RandomYawBox { get; } = new() { Content = "Random facing (off: all face the rotation)", IsChecked = true, FontSize = 12 };
	internal CheckBox SingleBox { get; } = new() { Content = "One at a time, exactly at the cursor", FontSize = 12 };
	internal CheckBox EndToEndBox { get; } = new() { Content = "End to end (snap together, like in game)", FontSize = 12 };
	internal NumericUpDown LayersBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0", FontSize = 12 };
	internal CheckBox LoopBox { get; } = new() { Content = "Close the loop (back to the first point)", FontSize = 12 };
	internal Slider EverySlider { get; private set; } = null!;
	internal Slider WiggleSlider { get; private set; } = null!;
	internal CheckBox AlongBox { get; } = new() { Content = "Follow the line (plus the rotation; replaces random facing)", IsChecked = true, FontSize = 12 };
	internal CheckBox CurveBox { get; } = new() { Content = "Smooth curve through the points", IsChecked = true, FontSize = 12 };
	internal Slider CellSlider { get; private set; } = null!;
	internal Button PlaceButton { get; } = new() { Content = "Place (Enter)", FontSize = 12 };
	internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
	internal Button UndoPointButton { get; } = new() { Content = "Remove last point (Backspace)", FontSize = 12 };
	internal Button NewLayoutButton { get; } = new() { Content = "New layout (R)", FontSize = 12 };
	internal TextBlock Info { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private readonly Control _elevRow, _pieceBox, _attachRow, _brushRows, _scatterRows, _patchRow, _facingRows, _lineBox, _snapRows, _freeLineRows, _loopRow, _layersRow, _gridRow, _shapeRow, _undoRow, _brushHint, _lineHint;
	private readonly TextBlock _lineHintText = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };

	private static readonly IBrush On = new SolidColorBrush(Color.FromRgb(58, 92, 140));

	private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> fmt, Action<float> set)
	{
		var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
		shown.Text = fmt(value);
		shown.FontSize = 12;
		shown.VerticalAlignment = VerticalAlignment.Center;
		shown.Margin = new Thickness(6, 0, 0, 0);
		s.ValueChanged += (_, e) => { shown.Text = fmt(e.NewValue); set((float)e.NewValue); };
		return s;
	}

	private static Control Row(string label, Control input, Control? after = null)
	{
		var g = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*,Auto") };
		g.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
		Grid.SetColumn(input, 1);
		g.Children.Add(input);
		if (after != null)
		{
			Grid.SetColumn(after, 2);
			g.Children.Add(after);
		}
		return g;
	}

	private static Border CardOf(Control child, double width) => new()
	{
		Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
		BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
		BorderThickness = new Thickness(1),
		CornerRadius = new CornerRadius(10),
		Padding = new Thickness(8),
		VerticalAlignment = VerticalAlignment.Top,
		// As tall as the window allows: the scroll bar only when it does not fit, beside the content
		// (it is drawn over it otherwise).
		Child = new ScrollViewer { Content = new StackPanel { Width = width, Spacing = 6, Margin = new Thickness(0, 0, 12, 0), Children = { child } } },
	};

	public PlacePanel(PlaceInput input, Func<WorldScene?> scene, Func<int, string?> nameOf, PlaceMemory? memory = null)
	{
		_input = input;
		_scene = scene;
		_nameOf = nameOf;
		var t = input.Tool;
		Memory = memory ?? PlaceMemory.Load();
		if (Memory.Chosen.Count > 0)
		{
			t.Chosen.Clear();
			t.Chosen.AddRange(Memory.Chosen);
		}
		foreach (var (k, w) in Memory.Weights)
		{
			t.Weights[k] = w;
		}
		t.AutoSnap();
		input.Placed += names => { Memory.NoteRecent(names); RenderChips(); };
		var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
		foreach (var m in Enum.GetValues<PlaceTool.Modes>())
		{
			var b = new Button { Content = m.ToString(), FontSize = 12 };
			b.Click += (_, _) => SetMode(m);
			ModeButtons[m] = b;
			modes.Children.Add(b);
		}
		ToolTip.SetTip(ModeButtons[PlaceTool.Modes.Brush], "Paint under the brush");
		ToolTip.SetTip(ModeButtons[PlaceTool.Modes.Line], "Objects along a line you draw");
		ToolTip.SetTip(ModeButtons[PlaceTool.Modes.Grid], "One object in the middle of each grid cell");
		ToolTip.SetTip(ModeButtons[PlaceTool.Modes.Zone], "Fill a shape you draw freely");
		PresetBox = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
		FillPresets();
		PresetBox.SelectionChanged += (_, _) =>
		{
			DeletePresetButton.IsEnabled = PresetAt(PresetBox.SelectedIndex) is { Own: true };
			if (_filling || PresetAt(PresetBox.SelectedIndex) is not { } chosen)
			{
				return;
			}
			var known = Creatable().Select(c => c.Name).ToHashSet();
			Say(t.Load(chosen.Preset, known.Contains));
			Remember();
			Fill();
			FillList();
		};
		ToolTip.SetTip(SavePresetButton, "Save the chosen kinds, their weights and the Density, Spacing, Size, Tilt and clumping settings under a name");
		SavePresetButton.Click += async (_, _) =>
		{
			if (t.Chosen.Count == 0)
			{
				Say("Tick one or more kinds first.");
				return;
			}
			string? name = (await AskName())?.Trim();
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			var p = new PlaceTool.Preset(name, t.Chosen.ToDictionary(n => n, t.WeightOf), t.Density, t.Spacing, t.SizeMin, t.SizeMax, t.Tilt, t.Clump, t.Patch);
			Memory.Presets = Memory.Presets.Where(x => x.Name != name).Append(p).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
			Memory.Save();
			FillPresets(name);
			Say($"Saved the preset {name}.");
		};
		DeletePresetButton.Click += async (_, _) =>
		{
			if (PresetAt(PresetBox.SelectedIndex) is not { Own: true } p || !await Confirm($"Delete the preset {p.Preset.Name}?"))
			{
				return;
			}
			Memory.Presets.RemoveAll(x => x.Name == p.Preset.Name);
			Memory.Save();
			FillPresets();
		};
		KindsButton.Click += (_, _) => { Chooser!.IsVisible = !Chooser.IsVisible; KindsButton.Content = Chooser.IsVisible ? "Done" : "+ Add kinds"; FillList(); };
		Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) FillList(); };
		PickButton.Click += (_, _) =>
		{
			input.PickOnce = name =>
			{
				if (!Creatable().Any(c => c.Name == name))
				{
					Say($"{name} cannot be placed: the game has no such kind to copy.");
					return;
				}
				t.Chosen.Clear();
				t.Chosen.Add(name);
				Chosen();
				Say($"Placing {name}.");
			};
			Say("Click an object in the view to place its kind.");
		};
		ElevationBox.SelectionChanged += (_, _) => { if (!_filling) { t.Elevation = (PlaceTool.Elevations)Math.Max(0, ElevationBox.SelectedIndex); t.Notify(); } Sync(); };
		ElevBox.ValueChanged += (_, e) => { if (!_filling) { t.Elev = (float)(e.NewValue ?? 0); t.Notify(); } };
		SnapToBox.IsCheckedChanged += (_, _) => { t.SnapTo = SnapToBox.IsChecked == true; t.Notify(); };
		BesideButton.Click += (_, _) => { t.OnTop = false; Sync(); t.Notify(); };
		OnTopButton.Click += (_, _) => { t.OnTop = true; Sync(); t.Notify(); };
		ToolTip.SetTip(BesideButton, "End to end with the piece next to the cursor, at its level");
		ToolTip.SetTip(OnTopButton, "On top of the piece under the cursor");
		GrowBox.IsCheckedChanged += (_, _) => { t.GrowRoom = GrowBox.IsChecked == true; t.Notify(); };
		var sizeV = new TextBlock();
		SizeSlider = Slide(1, 30, 0.5, t.Brush.Radius, sizeV, v => $"{v:0.#} m", v => { t.Brush.Radius = v; t.Notify(); });
		var densV = new TextBlock();
		DensitySlider = Slide(0.2, 20, 0.2, t.Density, densV, v => $"{v:0.0}", v => { t.Density = v; t.Notify(); });
		var spV = new TextBlock();
		SpacingSlider = Slide(0.5, 15, 0.5, t.Spacing, spV, v => $"{v:0.#} m", v => { t.Spacing = v; t.Notify(); });
		var clV = new TextBlock();
		ClumpSlider = Slide(0, 100, 5, t.Clump, clV, v => $"{v:0}%", v => { t.Clump = v; Sync(); t.Notify(); });
		var paV = new TextBlock();
		PatchSlider = Slide(5, 120, 5, t.Patch, paV, v => $"{v:0} m", v => { t.Patch = v; t.Notify(); });
		SizeMinBox.ValueChanged += (_, e) => { t.SizeMin = (float)(e.NewValue ?? 80); t.Notify(); };
		SizeMaxBox.ValueChanged += (_, e) => { t.SizeMax = (float)(e.NewValue ?? 120); t.Notify(); };
		var tiV = new TextBlock();
		TiltSlider = Slide(0, 20, 1, t.Tilt, tiV, v => $"{v:0}°", v => { t.Tilt = v; t.Notify(); });
		var roV = new TextBlock();
		RotationSlider = Slide(-180, 180, 1, t.Rotation, roV, v => $"{v:0}°", v => { if (!_filling) { t.Rotation = v; t.Notify(); } });
		RandomYawBox.IsCheckedChanged += (_, _) => { t.RandomYaw = RandomYawBox.IsChecked == true; t.Notify(); };
		SingleBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.Single = SingleBox.IsChecked == true; t.SingleByHand = true; t.Notify(); } };
		EndToEndBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.EndToEnd = EndToEndBox.IsChecked == true; t.EndToEndByHand = true; Sync(); t.Notify(); } };
		LayersBox.ValueChanged += (_, e) => { t.Layers = (int)(e.NewValue ?? 1); t.Notify(); };
		LoopBox.IsCheckedChanged += (_, _) => { t.Loop = LoopBox.IsChecked == true; t.Notify(); };
		var evV = new TextBlock();
		EverySlider = Slide(0.5, 30, 0.5, t.Every, evV, v => $"{v:0.#} m", v => { t.Every = v; t.Notify(); });
		var wiV = new TextBlock();
		WiggleSlider = Slide(0, 5, 0.25, t.Wiggle, wiV, v => $"{v:0.##} m", v => { t.Wiggle = v; t.Notify(); });
		AlongBox.IsCheckedChanged += (_, _) => { t.Along = AlongBox.IsChecked == true; t.Notify(); };
		CurveBox.IsCheckedChanged += (_, _) => { t.Curve = CurveBox.IsChecked == true; t.Notify(); };
		var ceV = new TextBlock();
		CellSlider = Slide(1, 30, 0.5, t.Cell, ceV, v => $"{v:0.#} m", v => { t.Cell = v; t.Notify(); });
		var shapes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
		foreach (var (ls, label, tip) in new[] { (PlaceTool.LineShapes.Points, "Points", "Click points along the way, or hold and drag to draw freely"),
			(PlaceTool.LineShapes.Circle, "Circle", "Press at the centre and drag out to the size you want"), (PlaceTool.LineShapes.Rect, "Rectangle", "Press at one corner and drag to the opposite corner") })
		{
			var b = new Button { Content = label, FontSize = 12 };
			ToolTip.SetTip(b, tip);
			b.Click += (_, _) => { t.LineShape = ls; t.ClearShape(); Sync(); };
			ShapeButtons[ls] = b;
			shapes.Children.Add(b);
		}
		PlaceButton.Click += (_, _) => input.PlaceShape();
		ClearButton.Click += (_, _) => t.ClearShape();
		UndoPointButton.Click += (_, _) => input.RemoveLastPoint();
		NewLayoutButton.Click += (_, _) => t.NewLayout();
		TextBlock Hint(string s) => new() { Text = s, FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };

		_elevRow = Row("Height", ElevBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
		_attachRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { BesideButton, OnTopButton } };
		_pieceBox = new StackPanel { Spacing = 4, Children = { SnapToBox, _attachRow } };
		_brushRows = new StackPanel { Spacing = 4, Children = { Row("Brush size", SizeSlider, sizeV) } };
		_patchRow = Row("Patch size", PatchSlider, paV);
		_scatterRows = new StackPanel { Spacing = 4, Children = { Row("Density", DensitySlider, densV), Row("Spacing", SpacingSlider, spV), Row("Clumping", ClumpSlider, clV), _patchRow } };
		_facingRows = new StackPanel
		{
			Spacing = 4,
			Children =
			{
				Row("Size %", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SizeMinBox, SizeMaxBox } }),
				Row("Tilt", TiltSlider, tiV),
				Row("Rotation", RotationSlider, roV),
				RandomYawBox,
			},
		};
		_layersRow = Row("Layers", LayersBox);
		_loopRow = LoopBox;
		_snapRows = new StackPanel { Spacing = 4, Children = { EndToEndBox, _layersRow } };
		_freeLineRows = new StackPanel { Spacing = 4, Children = { Row("Every", EverySlider, evV), Row("Wiggle", WiggleSlider, wiV), AlongBox } };
		_lineBox = new StackPanel { Spacing = 4, Children = { shapes, _snapRows, _loopRow, _freeLineRows, CurveBox, _lineHintText } };
		_gridRow = Row("Spacing", CellSlider, ceV);
		_shapeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PlaceButton, ClearButton } };
		_undoRow = UndoPointButton;
		_brushHint = Hint("Density is objects per 100 m². Spacing keeps them apart (also from what is already there). Shift + drag removes the chosen kinds. The Mask applies.");
		_lineHint = Hint("");

		Card = CardOf(new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = "Place", FontSize = 14, FontWeight = FontWeight.SemiBold },
				modes,
				Row("Preset", PresetBox),
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SavePresetButton, DeletePresetButton } },
				new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "KINDS", FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center }, Col(KindsButton, 1) } },
				Mix,
				Note,
				Row("Elevation", ElevationBox),
				_elevRow,
				_pieceBox,
				GrowBox,
				_brushRows,
				_scatterRows,
				_facingRows,
				SingleBox,
				_lineBox,
				_gridRow,
				_shapeRow,
				_undoRow,
				NewLayoutButton,
				Info,
				_brushHint,
			},
		}, 320);
		Chooser = CardOf(new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = "Choose kinds", FontSize = 14, FontWeight = FontWeight.SemiBold },
				Search,
				PickButton,
				(_favBox = new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "FAVOURITES", FontSize = 10, Foreground = Brushes.Gray }, Favourites } }),
				(_recentBox = new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "RECENT", FontSize = 10, Foreground = Brushes.Gray }, Recent } }),
				List,
			},
		}, 240);
		Chooser.IsVisible = false;
		input.Changed += ShowInfo;
		Fill();
		RenderChips();
	}

	private sealed record PresetEntry(PlaceTool.Preset Preset, bool Own);

	private PresetEntry? PresetAt(int i) => i <= 0 ? null : i <= PlaceTool.BuiltIn.Length ? new(PlaceTool.BuiltIn[i - 1], false)
		: i - 1 - PlaceTool.BuiltIn.Length < Memory.Presets.Count ? new(Memory.Presets[i - 1 - PlaceTool.BuiltIn.Length], true) : null;

	private void FillPresets(string? select = null)
	{
		_filling = true;
		PresetBox.ItemsSource = new[] { "Choose a preset…" }.Concat(PlaceTool.BuiltIn.Select(p => p.Name)).Concat(Memory.Presets.Select(p => $"Yours: {p.Name}")).ToList();
		int own = Memory.Presets.FindIndex(p => p.Name == select);
		PresetBox.SelectedIndex = own >= 0 ? 1 + PlaceTool.BuiltIn.Length + own : 0;
		DeletePresetButton.IsEnabled = own >= 0;
		_filling = false;
	}

	// The chosen kinds and weights are remembered for the next time.
	private void Remember()
	{
		Memory.Chosen = T.Chosen.ToList();
		Memory.Weights = new(T.Weights);
		Memory.Save();
	}

	// Favourites and recent kinds as buttons: a click ticks or unticks that kind.
	private void RenderChips()
	{
		var known = Creatable().Select(c => c.Name).ToHashSet();
		foreach (var (panel, list, box) in new[] { (Favourites, Memory.Favourites, _favBox), (Recent, Memory.Recent, _recentBox) })
		{
			panel.Children.Clear();
			foreach (var n in list.Where(n => known.Count == 0 || known.Contains(n)))
			{
				var b = new ToggleButton { Content = n, IsChecked = T.Chosen.Contains(n), FontSize = 11, Padding = new Thickness(6, 2) };
				b.Click += (_, _) =>
				{
					if (T.Chosen.Contains(n)) T.Chosen.Remove(n); else T.Chosen.Add(n);
					Chosen();
				};
				panel.Children.Add(b);
			}
			box.IsVisible = panel.Children.Count > 0;
		}
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	public event Action<string>? Message;
	private void Say(string s) => Message?.Invoke(s);

	// The kinds that can be placed in this world, with their kind of object (for the list's order).
	private List<(string Name, ObjectKind Kind)> _creatable = new();
	internal List<(string Name, ObjectKind Kind)> Creatable()
	{
		if (_creatable.Count == 0 && _scene()?.World is { } w)
		{
			_creatable = w.Creatable.Select(p => _nameOf(p)).Where(n => n != null).Distinct().Select(n => (n!, ObjectKinds.Of(n, false))).ToList();
		}
		return _creatable;
	}

	private static readonly ObjectKind[] Order = { ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables, ObjectKind.Ore, ObjectKind.Other, ObjectKind.Ruins, ObjectKind.Buildings };

	public void FillList()
	{
		if (!Chooser.IsVisible)
		{
			return;
		}
		string q = Search.Text?.Trim() ?? "";
		List.Children.Clear();
		foreach (var k in Order)
		{
			var names = Creatable().Where(c => c.Kind == k && (q == "" || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
			if (names.Count == 0)
			{
				continue;
			}
			List.Children.Add(new TextBlock { Text = ObjectKinds.Label(k).ToUpperInvariant(), FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 2) });
			// Long groups are cut while not searching: the search finds the rest.
			foreach (var n in q == "" ? names.Take(60) : names)
			{
				var box = new CheckBox { Content = n, IsChecked = T.Chosen.Contains(n), FontSize = 12 };
				box.IsCheckedChanged += (_, _) =>
				{
					if (box.IsChecked == true) { if (!T.Chosen.Contains(n)) T.Chosen.Add(n); } else T.Chosen.Remove(n);
					Chosen(fillList: false);
				};
				bool fav = Memory.Favourites.Contains(n);
				var star = new Button { Content = fav ? "★" : "☆", FontSize = 12, Padding = new Thickness(4, 0), Background = Brushes.Transparent };
				ToolTip.SetTip(star, fav ? "Remove from favourites" : "Add to favourites");
				star.Click += (_, _) => { Memory.ToggleFavourite(n); FillList(); RenderChips(); };
				List.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Col(star, 1) } });
			}
			if (q == "" && names.Count > 60)
			{
				List.Children.Add(new TextBlock { Text = $"… {names.Count - 60} more: search for them.", FontSize = 11, Foreground = Brushes.Gray });
			}
		}
	}

	// The chosen kinds changed.
	private void Chosen(bool fillList = true)
	{
		T.AutoSnap();
		_filling = true;
		PresetBox.SelectedIndex = 0;
		_filling = false;
		Fill();
		if (fillList)
		{
			FillList();
		}
		Remember();
		RenderChips();
		T.NewLayout();
	}

	private void RenderMix()
	{
		Mix.Children.Clear();
		var names = T.Chosen.ToList();
		float total = names.Sum(T.WeightOf);
		foreach (var n in names)
		{
			var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,80,36,24") };
			g.Children.Add(new TextBlock { Text = n, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
			var pct = new TextBlock { FontSize = 11, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Text = names.Count > 1 ? $"{T.WeightOf(n) / total * 100:0}%" : "" };
			if (names.Count > 1)
			{
				var w = new Slider { Minimum = 1, Maximum = 10, TickFrequency = 1, IsSnapToTickEnabled = true, Value = T.WeightOf(n) };
				ToolTip.SetTip(w, $"Weight of {n}: how often it is used compared to the other chosen kinds");
				w.ValueChanged += (_, e) => { T.Weights[n] = (int)e.NewValue; RenderMix(); Remember(); T.NewLayout(); };
				g.Children.Add(Col(w, 1));
			}
			g.Children.Add(Col(pct, 2));
			var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) };
			ToolTip.SetTip(x, $"Stop placing {n}");
			x.Click += (_, _) => { T.Chosen.Remove(n); Chosen(); };
			g.Children.Add(Col(x, 3));
			Mix.Children.Add(g);
		}
	}

	private void SetMode(PlaceTool.Modes m)
	{
		T.Mode = m;
		Sync();
		T.Notify();
	}

	// The settings shown follow the mode and what is chosen.
	public void Fill()
	{
		_filling = true;
		var t = T;
		ElevationBox.SelectedIndex = (int)t.Elevation;
		ElevBox.Value = (decimal)t.Elev;
		RotationSlider.Value = t.Rotation;
		SingleBox.IsChecked = t.Single;
		EndToEndBox.IsChecked = t.EndToEnd;
		DensitySlider.Value = t.Density;
		SpacingSlider.Value = t.Spacing;
		ClumpSlider.Value = t.Clump;
		PatchSlider.Value = t.Patch;
		SizeMinBox.Value = (decimal)t.SizeMin;
		SizeMaxBox.Value = (decimal)t.SizeMax;
		TiltSlider.Value = t.Tilt;
		RandomYawBox.IsChecked = t.RandomYaw;
		_filling = false;
		RenderMix();
		Sync();
	}

	private void Sync()
	{
		var t = T;
		var m = t.Mode;
		foreach (var (k, b) in ModeButtons)
		{
			b.Background = k == m ? On : null;
		}
		foreach (var (k, b) in ShapeButtons)
		{
			b.Background = k == t.LineShape ? On : null;
		}
		BesideButton.Background = t.OnTop ? null : On;
		OnTopButton.Background = t.OnTop ? On : null;
		bool line = m == PlaceTool.Modes.Line, endToEnd = line && t.EndToEnd;
		_elevRow.IsVisible = t.Elevation != PlaceTool.Elevations.Ground;
		_pieceBox.IsVisible = t.PiecesChosen;
		_attachRow.IsVisible = m == PlaceTool.Modes.Brush;
		GrowBox.IsVisible = t.GrowMatters;
		_brushRows.IsVisible = m == PlaceTool.Modes.Brush;
		_scatterRows.IsVisible = m is PlaceTool.Modes.Brush or PlaceTool.Modes.Zone;
		_patchRow.IsVisible = t.Clump > 0;
		_facingRows.IsVisible = !endToEnd;
		SingleBox.IsVisible = m == PlaceTool.Modes.Brush;
		_lineBox.IsVisible = line;
		_layersRow.IsVisible = endToEnd;
		_loopRow.IsVisible = line && t.LineShape == PlaceTool.LineShapes.Points;
		_freeLineRows.IsVisible = !endToEnd;
		AlongBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
		CurveBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
		_gridRow.IsVisible = m == PlaceTool.Modes.Grid;
		_shapeRow.IsVisible = m != PlaceTool.Modes.Brush;
		_undoRow.IsVisible = m is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
		_brushHint.IsVisible = m == PlaceTool.Modes.Brush;
		_lineHintText.Text = t.LineShape switch
		{
			PlaceTool.LineShapes.Circle => "Press at the centre and drag out to the size; let go to see it, Enter places it. With End to end the size snaps so whole pieces close the ring.",
			PlaceTool.LineShapes.Rect => "Press at one corner and drag to the opposite one; Enter places it. With End to end the sides snap to whole pieces.",
			_ => "Click points along the route, or hold and drag to draw freely. Then drag a point to move it, drag the line to add a point, Ctrl + click a point to remove it; Backspace removes the last point.",
		};
		var cult = t.NeedCultivated;
		Note.Text = t.Chosen.Count == 0 ? "Nothing to place yet: + Add kinds, or a preset."
			: cult.Count > 0 ? $"{string.Join(", ", cult)} only grow{(cult.Count > 1 ? "" : "s")} on cultivated ground (paint it with Cultivate first)." : "";
		Note.IsVisible = Note.Text != "";
	}

	// What the preview shows.
	private void ShowInfo()
	{
		var t = T;
		_undoRow.IsVisible = t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
		int n = _input.PreviewNow.Count;
		if (t.Chosen.Count == 0)
		{
			Info.Text = "Tick at least one kind to place.";
		}
		else if (t.Mode == PlaceTool.Modes.Brush)
		{
			Info.Text = $"{(t.SnappedTo != null ? $"Snapped {t.SnappedTo}. " : "")}{n} object(s) shown under the cursor. Click places exactly these; drag paints more. R new layout · , . rotate.";
		}
		else
		{
			string where = t.Mode == PlaceTool.Modes.Line ? "along the line" : t.Mode == PlaceTool.Modes.Zone ? "in the zone" : "in the grid";
			string gap = t.Mode == PlaceTool.Modes.Line && t.EndToEnd && t.SnapGap > 0.05f && n > 0 ? $", end to end ({t.SnapGap:0.0} m of the line left at the end: move a point to close it)" : "";
			string turn = t.Mode is PlaceTool.Modes.Zone or PlaceTool.Modes.Grid ? $" · , . turn the {(t.Mode == PlaceTool.Modes.Zone ? "zone" : "grid")}{(t.ShapeTurn != 0 ? $" (now {t.ShapeTurn:0}°)" : "")}" : "";
			Info.Text = $"{n} object(s) {where}{gap}. Enter places them{turn} · R new random choices.";
		}
	}
}
