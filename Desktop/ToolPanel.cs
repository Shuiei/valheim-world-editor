using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
// brushes and the paint brushes. Keys as in the web editor: 1-9 and 0 pick the brushes, E selects,
// Esc goes back to View.
public sealed class ToolPanel
{
	public Brush Brush { get; } = new();
	public BrushTool? Tool { get; private set; }
	public bool SelectMode { get; private set; }
	// The tool changed (Tool and SelectMode say which).
	public event Action<BrushTool?>? ToolChanged;

	public Control Rail { get; }
	public Control Options { get; }

	private readonly Dictionary<BrushTool, Button> _buttons = new();
	private readonly Button _viewButton, _selectButton;
	internal Button SelectButton => _selectButton;
	private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
	private readonly TextBlock _help = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly Control _flattenRows, _naturalRows, _turnRow;

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
		(BrushTool.Raise, "1"), (BrushTool.Lower, "2"), (BrushTool.Flatten, "3"), (BrushTool.Smooth, "4"), (BrushTool.Natural, "0"), (BrushTool.Restore, "5"),
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
		ShapeBox = new ComboBox { ItemsSource = new[] { "Circle", "Square", "Ring", "Ragged (noise)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
		FalloffBox = new ComboBox { ItemsSource = new[] { "Smooth", "Linear", "Dome", "Flat top", "Peak", "Sharp edge (pickaxe)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
		var turnV = new TextBlock();
		var turn = Slide(-180, 180, 1, 0, turnV, v => $"{v:0}°", v => Brush.Turn = (float)v);
		_turnRow = Row("Turn", turn, turnV);
		ShapeBox.SelectionChanged += (_, _) => { Brush.Shape = (BrushShape)Math.Max(0, ShapeBox.SelectedIndex); _turnRow.IsVisible = Brush.Shape == BrushShape.Square; };
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
				Row("Falloff", FalloffBox),
				_turnRow,
				_flattenRows,
				_naturalRows,
			},
		});
		Choose(null);
	}

	public void ChooseSelect()
	{
		Choose(null, select: true);
	}

	public void Choose(BrushTool? t, bool select = false)
	{
		Tool = t;
		SelectMode = t == null && select;
		_viewButton.Background = t == null && !select ? On : Off;
		_selectButton.Background = SelectMode ? On : Off;
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

	// The flatten height follows the ground where a stroke started.
	public void ShowTarget(float h) => TargetBox.Value = (decimal)MathF.Round(h, 1);
}
