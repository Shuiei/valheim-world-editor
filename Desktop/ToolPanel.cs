using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
// brushes and the paint brushes. Keys as in the web editor: 1-9 and 0 pick the brushes, E selects,
// Esc goes back to View.
// Which tool is in use: View (look around, click picks), Select, Measure, Shape, Path, Area, Paste (from the
// Area tool, Ctrl+V), Place, or a brush (Tool says which).
public enum ToolMode { View, Select, Measure, Shape, Path, Area, Paste, Place, Brush }

public sealed class ToolPanel
{
	public Brush Brush { get; } = new();
	public BrushTool? Tool { get; private set; }
	public ToolMode Mode { get; private set; }
	public bool SelectMode => Mode == ToolMode.Select;
	// The tool changed (Mode and Tool say which).
	public event Action<BrushTool?>? ToolChanged;

	public Control Rail { get; }
	public Control Options { get; }

	private readonly Dictionary<BrushTool, Button> _buttons = new();
	private readonly Button _viewButton, _selectButton, _measureButton, _shapeButton, _pathButton, _areaButton, _placeButton;
	internal Button SelectButton => _selectButton;
	private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
	private readonly TextBlock _help = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly Control _flattenRows, _naturalRows, _turnRow, _erodeRows, _falloffRow, _stampOnceRows, _stampHeightRow;
	// The stamps in the Shape list after the four shapes: the built-in ones, then loaded pictures.
	internal List<Stamps.Stamp> StampList { get; } = Stamps.BuiltIn.Concat(Stamps.LoadKept()).ToList();
	internal Button LoadStampButton { get; } = new() { Content = "Load stamp…", FontSize = 12 };
	internal Button ForgetStampButton { get; } = new() { Content = "Forget stamp", FontSize = 12 };
	internal CheckBox StampOnceBox { get; } = new() { Content = "Stamp once: a click puts the whole stamp in", FontSize = 12 };
	internal NumericUpDown StampHeightBox { get; } = new() { Value = 4, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
	// Load stamp…: the window picks the picture.
	public event Action? LoadStampAsked;
	public event Action<string>? Message;
	internal Button ThermalButton { get; } = new() { Content = "Thermal", FontSize = 12 };
	internal Button WaterButton { get; } = new() { Content = "Water", FontSize = 12 };
	internal Slider RestSlider { get; }

	// For tests.
	internal Slider SizeSlider { get; }
	internal Slider StrengthSlider { get; }
	internal ComboBox ShapeBox { get; }
	internal ComboBox FalloffBox { get; }
	internal CheckBox TargetFromClickBox { get; }
	internal NumericUpDown TargetBox { get; }
	internal Button ButtonOf(BrushTool t) => _buttons[t];

	// The keys of the web editor's rail.
	public static readonly (BrushTool Tool, string Key)[] Keys =
	{
		(BrushTool.Raise, "1"), (BrushTool.Lower, "2"), (BrushTool.Flatten, "3"), (BrushTool.Smooth, "4"), (BrushTool.Natural, "0"), (BrushTool.Erode, "O"), (BrushTool.Restore, "5"),
		(BrushTool.PaintDirt, "6"), (BrushTool.PaintCultivated, "7"), (BrushTool.PaintPaved, "8"), (BrushTool.PaintClear, "9"),
	};

	private static readonly IBrush On = new SolidColorBrush(Color.FromRgb(58, 92, 140)), Off = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

	private static Border Card(Control child) => new()
	{
		Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
		BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
		BorderThickness = new Thickness(1),
		CornerRadius = new CornerRadius(10),
		Padding = new Thickness(8),
		Child = child,
	};

	private static Control Row(string label, Control input, TextBlock? value = null)
	{
		var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,46") };
		g.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
		Grid.SetColumn(input, 1);
		g.Children.Add(input);
		if (value != null)
		{
			Grid.SetColumn(value, 2);
			value.VerticalAlignment = VerticalAlignment.Center;
			value.FontSize = 12;
			value.Margin = new Thickness(6, 0, 0, 0);
			g.Children.Add(value);
		}
		return g;
	}

	private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> format, Action<double> set)
	{
		var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
		shown.Text = format(value);
		s.ValueChanged += (_, e) => { shown.Text = format(e.NewValue); set(e.NewValue); };
		return s;
	}

	public ToolPanel()
	{
		var rail = new StackPanel { Spacing = 2 };
		Button Make(string label, string key)
		{
			var b = new Button
			{
				Content = new Grid
				{
					ColumnDefinitions = new ColumnDefinitions("*,Auto"),
					Children = { new TextBlock { Text = label, FontSize = 12 }, new TextBlock { Text = key, FontSize = 10, Foreground = Brushes.Gray, [Grid.ColumnProperty] = 1, Margin = new Thickness(8, 0, 0, 0) } },
				},
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Padding = new Thickness(8, 4),
				Background = Off,
			};
			rail.Children.Add(b);
			return b;
		}
		_viewButton = Make("View", "Esc");
		ToolTip.SetTip(_viewButton, "Look around and click objects to select them.");
		_viewButton.Click += (_, _) => Choose(null);
		_selectButton = Make("Select", "E");
		ToolTip.SetTip(_selectButton, "Select (E): click objects, or drag on the ground around them; then move, turn, lift or delete them.");
		_selectButton.Click += (_, _) => ChooseSelect();
		_measureButton = Make("Measure", "M");
		ToolTip.SetTip(_measureButton, "Measure (M): click two points to see the distance, height difference and slope.");
		_measureButton.Click += (_, _) => ChooseMode(ToolMode.Measure);
		_shapeButton = Make("Shape", "G");
		ToolTip.SetTip(_shapeButton, "Shape (G): click to put a mound, cone, mesa, crater, moat or bowl into the ground, or any shape you write as a formula.");
		_shapeButton.Click += (_, _) => ChooseMode(ToolMode.Shape);
		_pathButton = Make("Path", "P");
		ToolTip.SetTip(_pathButton, "Path (P): draw a line, then flatten, ramp, raise, lower, smooth, dig a river or paint along it.");
		_pathButton.Click += (_, _) => ChooseMode(ToolMode.Path);
		_areaButton = Make("Area", "B");
		ToolTip.SetTip(_areaButton, "Area (B): select a box or polygon, then change the ground, remove, select or replace objects, or reset its zones.");
		_areaButton.Click += (_, _) => ChooseMode(ToolMode.Area);
		_placeButton = Make("Place", "T");
		ToolTip.SetTip(_placeButton, "Place (T): paint trees, rocks or bushes with a brush, or put walls, fences and other pieces along lines, circles, rectangles, grids and zones.");
		_placeButton.Click += (_, _) => ChooseMode(ToolMode.Place);
		rail.Children.Add(new TextBlock { Text = "SCULPT", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 6, 0, 0) });
		foreach (var (t, key) in Keys)
		{
			if (t == BrushTool.PaintDirt)
			{
				rail.Children.Add(new TextBlock { Text = "PAINT", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 6, 0, 0) });
			}
			var b = Make(Brush.Label(t), key);
			ToolTip.SetTip(b, $"{Brush.Label(t)} ({key}): {Brush.Help(t)}");
			b.Click += (_, _) => Choose(t);
			_buttons[t] = b;
		}
		Rail = Card(new StackPanel { Width = 118, Children = { rail } });

		// The options: size and strength, the brush shape, and each tool's own.
		var sizeV = new TextBlock();
		SizeSlider = Slide(1, 30, 0.5, Brush.Radius, sizeV, v => $"{v:0.#} m", v => Brush.Radius = (float)v);
		var strengthV = new TextBlock();
		StrengthSlider = Slide(0.05, 1, 0.05, Brush.Strength, strengthV, v => $"{v:0.00}", v => Brush.Strength = (float)v);
		ShapeBox = new ComboBox { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12, MaxDropDownHeight = 400 };
		FillShapes();
		FalloffBox = new ComboBox { ItemsSource = new[] { "Smooth", "Linear", "Dome", "Flat top", "Peak", "Sharp edge (pickaxe)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
		var turnV = new TextBlock();
		var turn = Slide(-180, 180, 1, 0, turnV, v => $"{v:0}°", v => Brush.Turn = (float)v);
		_turnRow = Row("Turn", turn, turnV);
		ShapeBox.SelectionChanged += (_, _) => ShapeChosen();
		ToolTip.SetTip(LoadStampButton, "Use a grayscale picture as the brush shape: white works fully, black not at all");
		ToolTip.SetTip(ForgetStampButton, "Forget the loaded picture chosen as Shape");
		LoadStampButton.Click += (_, _) => LoadStampAsked?.Invoke();
		ForgetStampButton.Click += (_, _) =>
		{
			int i = ShapeBox.SelectedIndex - 4;
			if (i < 0 || i >= StampList.Count || !StampList[i].Loaded)
			{
				Message?.Invoke("Choose a loaded picture as Shape first (built-in stamps stay).");
				return;
			}
			StampList.RemoveAt(i);
			Stamps.SaveKept(StampList.Where(s => s.Loaded));
			FillShapes();
			ShapeBox.SelectedIndex = 0;
		};
		StampOnceBox.IsCheckedChanged += (_, _) => { Brush.StampOnce = StampOnceBox.IsChecked == true; SyncStamp(); };
		StampHeightBox.ValueChanged += (_, e) => Brush.StampHeight = (float)(e.NewValue ?? 0);
		_stampHeightRow = Row("Height", StampHeightBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
		_stampOnceRows = new StackPanel { Spacing = 4, Children = { StampOnceBox, _stampHeightRow } };
		FalloffBox.SelectionChanged += (_, _) => Brush.Falloff = (Falloff)Math.Max(0, FalloffBox.SelectedIndex);
		_turnRow.IsVisible = false;

		TargetFromClickBox = new CheckBox { Content = "Level to the height where the stroke starts", IsChecked = Brush.TargetFromClick, FontSize = 12 };
		TargetBox = new NumericUpDown { Value = (decimal)Brush.Target, Increment = 0.1m, FormatString = "0.0", FontSize = 12, IsEnabled = !Brush.TargetFromClick };
		TargetFromClickBox.IsCheckedChanged += (_, _) => { Brush.TargetFromClick = TargetFromClickBox.IsChecked == true; TargetBox.IsEnabled = !Brush.TargetFromClick; };
		TargetBox.ValueChanged += (_, e) => Brush.Target = (float)(e.NewValue ?? 0);
		_flattenRows = new StackPanel { Spacing = 4, Children = { TargetFromClickBox, Row("Height (m)", TargetBox) } };

		var ampV = new TextBlock();
		var noiseV = new TextBlock();
		_naturalRows = new StackPanel
		{
			Spacing = 4,
			Children =
			{
				Row("Bumps", Slide(0.2, 4, 0.1, Brush.NoiseAmp, ampV, v => $"{v:0.0} m", v => Brush.NoiseAmp = (float)v), ampV),
				Row("Bump size", Slide(4, 60, 1, Brush.NoiseSize, noiseV, v => $"{v:0} m", v => Brush.NoiseSize = (float)v), noiseV),
			},
		};

		var restV = new TextBlock();
		RestSlider = Slide(10, 60, 1, Brush.RestAngle, restV, v => $"{v:0}°", v => Brush.RestAngle = (float)v);
		var restRow = Row("Rest angle", RestSlider, restV);
		void Mode(bool water)
		{
			Brush.ErodeWater = water;
			ThermalButton.Background = water ? Off : On;
			WaterButton.Background = water ? On : Off;
			restRow.IsVisible = !water;
		}
		ThermalButton.Click += (_, _) => Mode(false);
		WaterButton.Click += (_, _) => Mode(true);
		ToolTip.SetTip(ThermalButton, "Steep ground slides down to its resting angle");
		ToolTip.SetTip(WaterButton, "Rain runs downhill, cutting gullies and filling hollows");
		_erodeRows = new StackPanel { Spacing = 4, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ThermalButton, WaterButton } }, restRow } };
		Mode(false);
		Options = Card(new StackPanel
		{
			Width = 280,
			Spacing = 6,
			Children =
			{
				_title, _help,
				Row("Size", SizeSlider, sizeV),
				Row("Strength", StrengthSlider, strengthV),
				Row("Shape", ShapeBox),
				(_falloffRow = Row("Falloff", FalloffBox)),
				_turnRow,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { LoadStampButton, ForgetStampButton } },
				_stampOnceRows,
				_flattenRows,
				_naturalRows,
				_erodeRows,
			},
		});
		Choose(null);
	}

	public void ChooseSelect() => ChooseMode(ToolMode.Select);

	public void ChooseMode(ToolMode mode) => Choose(null, mode);

	// A brush, or (null) the mode's tool.
	public void Choose(BrushTool? t, ToolMode mode = ToolMode.View)
	{
		Tool = t;
		Mode = t != null ? ToolMode.Brush : mode;
		_viewButton.Background = Mode == ToolMode.View ? On : Off;
		_selectButton.Background = Mode == ToolMode.Select ? On : Off;
		_measureButton.Background = Mode == ToolMode.Measure ? On : Off;
		_shapeButton.Background = Mode == ToolMode.Shape ? On : Off;
		_pathButton.Background = Mode == ToolMode.Path ? On : Off;
		_areaButton.Background = Mode is ToolMode.Area or ToolMode.Paste ? On : Off;
		_placeButton.Background = Mode == ToolMode.Place ? On : Off;
		foreach (var (k, b) in _buttons)
		{
			b.Background = k == t ? On : Off;
		}
		Options.IsVisible = t != null;
		if (t is BrushTool tool)
		{
			_title.Text = Brush.Label(tool);
			_help.Text = Brush.Help(tool) + " Hold the left button and move; Ctrl+Z undoes.";
			_flattenRows.IsVisible = tool == BrushTool.Flatten;
			_naturalRows.IsVisible = tool == BrushTool.Natural;
			_erodeRows.IsVisible = tool == BrushTool.Erode;
			SyncStamp();
		}
		ToolChanged?.Invoke(t);
	}

	// The rail's keys; true when the key was one.
	public bool Key(string key)
	{
		if (key == "Escape")
		{
			Choose(null);
			return true;
		}
		if (key == "E")
		{
			ChooseSelect();
			return true;
		}
		if (key == "T")
		{
			ChooseMode(ToolMode.Place);
			return true;
		}
		if (key == "B")
		{
			ChooseMode(ToolMode.Area);
			return true;
		}
		if (key == "P")
		{
			ChooseMode(ToolMode.Path);
			return true;
		}
		if (key == "G")
		{
			ChooseMode(ToolMode.Shape);
			return true;
		}
		if (key == "M")
		{
			ChooseMode(ToolMode.Measure);
			return true;
		}
		foreach (var (t, k) in Keys)
		{
			if (k == key)
			{
				Choose(t);
				return true;
			}
		}
		return false;
	}

	private void FillShapes() => ShapeBox.ItemsSource = new[] { "Circle", "Square", "Ring", "Ragged (noise)" }.Concat(StampList.Select(s => $"Stamp: {s.Label}")).ToList();

	private void ShapeChosen()
	{
		int i = Math.Max(0, ShapeBox.SelectedIndex);
		if (i >= 4 && i - 4 < StampList.Count)
		{
			Brush.Shape = BrushShape.Stamp;
			Brush.StampData = StampList[i - 4].Data;
			Brush.StampLabel = StampList[i - 4].Label;
		}
		else
		{
			Brush.Shape = (BrushShape)Math.Min(i, 3);
		}
		SyncStamp();
	}

	// A stamp: no falloff (the picture has its own), it turns, and Raise and Lower can stamp it once.
	private void SyncStamp()
	{
		bool stamp = Brush.Shape == BrushShape.Stamp;
		_turnRow.IsVisible = Brush.Shape is BrushShape.Square or BrushShape.Stamp;
		_falloffRow.IsVisible = !stamp;
		int i = ShapeBox.SelectedIndex - 4;
		ForgetStampButton.IsVisible = i >= 0 && i < StampList.Count && StampList[i].Loaded;
		_stampOnceRows.IsVisible = stamp && Tool is BrushTool.Raise or BrushTool.Lower;
		_stampHeightRow.IsVisible = Brush.StampOnce;
	}

	// A picture loaded as a stamp: chosen as the Shape, and kept for next time.
	public void AddStamp(string label, float[] data)
	{
		var st = new Stamps.Stamp($"pic:{DateTime.Now.Ticks}", label, data, Loaded: true);
		StampList.Add(st);
		Stamps.SaveKept(StampList.Where(s => s.Loaded));
		FillShapes();
		ShapeBox.SelectedIndex = 4 + StampList.Count - 1;
		Message?.Invoke($"Loaded the stamp “{label}”: white parts work fully, black parts not at all.");
	}

	// The flatten height follows the ground where a stroke started.
	public void ShowTarget(float h) => TargetBox.Value = (decimal)MathF.Round(h, 1);

	// Alt + click with a brush: level to that height (Flatten, not from the stroke's start).
	public void FlattenTo(float h)
	{
		TargetFromClickBox.IsChecked = false;
		Brush.Target = MathF.Round(h, 1);
		ShowTarget(h);
		Choose(BrushTool.Flatten);
	}
}
