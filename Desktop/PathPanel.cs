using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The Path tool's panel, like the web editor's: the action, width and soft edge, each action's own
// values, curve and natural look, Apply and Clear, and how long the line is.
public sealed class PathPanel
{
	public Control Card { get; }
	private readonly PathTool _path;
	private readonly GlView _view;
	private bool _filling;

	internal ComboBox ActionBox { get; }
	internal Slider WidthSlider { get; }
	internal Slider SoftSlider { get; }
	internal NumericUpDown HeightBox { get; } = Num(32, 0.1m);
	internal NumericUpDown AmountBox { get; } = Num(2, 0.1m);
	internal NumericUpDown StartBox { get; } = Num(32, 0.1m);
	internal NumericUpDown EndBox { get; } = Num(32, 0.1m);
	internal NumericUpDown DepthBox { get; } = Num(2, 0.25m);
	internal CheckBox CurveBox { get; } = new() { Content = "Smooth curve through the points", IsChecked = true, FontSize = 12 };
	internal CheckBox NaturalBox { get; } = new() { Content = "Natural look (ragged edges, bumps)", FontSize = 12 };
	internal Button ApplyButton { get; } = new Button { Content = "Apply (Enter)", FontSize = 12 }.Classed("primary");
	internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
	internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
	private readonly Dictionary<Control, PathTool.Action[]> _rows = new();
	private readonly Control _naturalRows;
	// Apply was asked for (the window does it: it has the edit session).
	public event Action? ApplyAsked;

	private static NumericUpDown Num(decimal v, decimal step) => new() { Value = v, Increment = step, FormatString = "0.0#", FontSize = 12 };

	private static Control Row(string label, Control input, Control? after = null)
	{
		var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,46") };
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

	private static Slider Slide(double min, double max, double step, double value, TextBlock shown, string unit, Action<float> set)
	{
		var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
		shown.Text = $"{value:0.#} {unit}";
		shown.FontSize = 12;
		shown.VerticalAlignment = VerticalAlignment.Center;
		shown.Margin = new Thickness(6, 0, 0, 0);
		s.ValueChanged += (_, e) => { shown.Text = $"{e.NewValue:0.#} {unit}"; set((float)e.NewValue); };
		return s;
	}

	private static TextBlock M() => new() { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

	public PathPanel(GlView view, Brush brush)
	{
		_view = view;
		_path = view.Path;
		var actions = Enum.GetValues<PathTool.Action>();
		ActionBox = new ComboBox { ItemsSource = actions.Select(PathTool.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
		ActionBox.SelectionChanged += (_, _) => { _path.Act = actions[Math.Max(0, ActionBox.SelectedIndex)]; ShowRows(); _path.Notify(); };
		var widthV = new TextBlock();
		WidthSlider = Slide(1, 40, 0.5, _path.Width, widthV, "m", v => { _path.Width = v; _path.Notify(); });
		var softV = new TextBlock();
		SoftSlider = Slide(0, 15, 0.5, _path.Soft, softV, "m", v => _path.Soft = v);
		HeightBox.ValueChanged += (_, e) => _path.Height = (float)(e.NewValue ?? 0);
		AmountBox.ValueChanged += (_, e) => _path.Amount = (float)(e.NewValue ?? 0);
		StartBox.ValueChanged += (_, e) => { _path.Start = (float)(e.NewValue ?? 0); if (!_filling) _path.RampEdited = true; };
		EndBox.ValueChanged += (_, e) => { _path.End = (float)(e.NewValue ?? 0); if (!_filling) _path.RampEdited = true; };
		DepthBox.ValueChanged += (_, e) => _path.Depth = (float)(e.NewValue ?? 0);
		DepthBox.Minimum = 0.25m;
		CurveBox.IsCheckedChanged += (_, _) => { _path.Curved = CurveBox.IsChecked == true; _path.Notify(); };
		NaturalBox.IsCheckedChanged += (_, _) => { _path.Natural = NaturalBox.IsChecked == true; ShowRows(); };
		ApplyButton.Click += (_, _) => ApplyAsked?.Invoke();
		ActionBox.Tip("path.action");
		WidthSlider.Tip("path.width");
		SoftSlider.Tip("path.soft");
		HeightBox.Tip("path.height");
		AmountBox.Tip("path.amount");
		StartBox.Tip("path.start");
		EndBox.Tip("path.end");
		DepthBox.Tip("path.depth");
		CurveBox.Tip("path.curve");
		NaturalBox.Tip("path.natural");
		ApplyButton.Tip("path.apply");
		ClearButton.Tip("path.clear");
		ClearButton.Click += (_, _) => _path.Clear();
		var ampV = new TextBlock();
		var sizeV = new TextBlock();
		_naturalRows = new StackPanel
		{
			Spacing = 4,
			Children =
			{
				Row("Bumps", Slide(0.2, 4, 0.1, brush.NoiseAmp, ampV, "m", v => brush.NoiseAmp = v).Tip("natural.amp"), ampV),
				Row("Bump size", Slide(4, 60, 1, brush.NoiseSize, sizeV, "m", v => brush.NoiseSize = v).Tip("natural.size"), sizeV),
			},
		};
		Control R(string label, NumericUpDown box, params PathTool.Action[] for_)
		{
			var r = Row(label, box, M());
			_rows[r] = for_;
			return r;
		}
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
				Width = 300,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Path", FontSize = 14, FontWeight = FontWeight.SemiBold },
					Row("Action", ActionBox),
					Row("Width", WidthSlider, widthV),
					Row("Soft edge", SoftSlider, softV),
					R("Height", HeightBox, PathTool.Action.Flatten),
					R("Amount", AmountBox, PathTool.Action.Raise, PathTool.Action.Lower),
					R("Start", StartBox, PathTool.Action.Ramp),
					R("End", EndBox, PathTool.Action.Ramp),
					R("Depth", DepthBox, PathTool.Action.River),
					CurveBox,
					NaturalBox,
					_naturalRows,
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ClearButton } },
					Info,
					new TextBlock
					{
						Text = "Click points along the route, or hold and drag. Then drag a point to move it, drag the line to add a point, Ctrl + click a point to remove it; Backspace removes the last one. Alt + click picks the height (Alt + Shift: ramp end).",
						FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
		_path.Changed += Refresh;
		ShowRows();
		Refresh();
	}

	private void ShowRows()
	{
		foreach (var (row, for_) in _rows)
		{
			row.IsVisible = for_.Contains(_path.Act);
		}
		_naturalRows.IsVisible = _path.Natural;
	}

	// The line or a value picked with Alt changed: the boxes and the line's length.
	public void Refresh()
	{
		_filling = true;
		var pts = _path.Points;
		// The ramp's ends follow the ground at the line's ends until typed in.
		if (pts.Count > 0 && !_path.RampEdited)
		{
			_path.Start = MathF.Round(_view.GridHeight(pts[0]), 1);
			_path.End = MathF.Round(_view.GridHeight(pts[^1]), 1);
		}
		StartBox.Value = (decimal)_path.Start;
		EndBox.Value = (decimal)_path.End;
		HeightBox.Value = (decimal)_path.Height;
		_filling = false;
		Info.Text = pts.Count > 0 ? $"{pts.Count} point(s), {PathTool.Length(_path.Curve()):0} m long." : "No line yet.";
		_view.LassoChanged();
	}
}
