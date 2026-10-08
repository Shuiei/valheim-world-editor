using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The Mountain tool's panel: a preset (peak, ridge, range, mesa, volcano, hills), its size and
// roughness, the direction of ridges and ranges, and the seed that makes its detail. Randomize rolls
// a new mountain of the preset; a click on the ground puts it there.
public sealed class MountainPanel
{
	public Control Card { get; }
	public event Action? Changed;

	internal ComboBox PresetBox { get; }
	internal Slider HeightSlider { get; }
	internal Slider RadiusSlider { get; }
	internal Slider RoughSlider { get; }
	internal Slider TurnSlider { get; }
	internal NumericUpDown SeedBox { get; } = new() { Minimum = 0, Maximum = int.MaxValue, Increment = 1, FormatString = "0", FontSize = 12 };
	internal Button RandomizeButton { get; } = new Button { Content = "Randomize", FontSize = 12 }.Classed("primary");
	internal CheckBox ClearBox { get; } = new() { Content = "Take away the trees and rocks it buries", IsChecked = true, FontSize = 12 };
	internal CheckBox GrowBox { get; } = new() { Content = "Grow the biome's trees and rocks on it", IsChecked = true, FontSize = 12 };
	internal TextBlock HelpText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private readonly Control _turnRow;
	private bool _filling;
	private readonly Random _random = new();

	public Mountain.Preset Preset => Mountain.Presets[Math.Max(0, PresetBox.SelectedIndex)];

	public MountainSpec Spec => new(Preset.Kind, (float)HeightSlider.Value, (float)RadiusSlider.Value, (float)RoughSlider.Value, (float)TurnSlider.Value, (int)(SeedBox.Value ?? 0));

	public bool Clear => ClearBox.IsChecked == true;

	public bool Grow => GrowBox.IsChecked == true;

	private static Grid Row(string label, Control input, Control? after = null)
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

	private Slider Slide(double min, double max, double step, TextBlock shown, Func<double, string> format)
	{
		var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true };
		shown.FontSize = 12;
		shown.VerticalAlignment = VerticalAlignment.Center;
		shown.Margin = new Thickness(6, 0, 0, 0);
		s.ValueChanged += (_, e) =>
		{
			shown.Text = format(e.NewValue);
			if (!_filling)
			{
				Changed?.Invoke();
			}
		};
		return s;
	}

	public MountainPanel()
	{
		PresetBox = new ComboBox { ItemsSource = Mountain.Presets.Select(p => p.Name).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
		TextBlock heightV = new(), radiusV = new(), roughV = new(), turnV = new();
		HeightSlider = Slide(5, 300, 1, heightV, v => $"{v:0} m");
		RadiusSlider = Slide(20, 280, 1, radiusV, v => $"{v:0} m");
		RoughSlider = Slide(0, 1, 0.05, roughV, v => $"{v:0.00}");
		TurnSlider = Slide(0, 179, 1, turnV, v => $"{v:0}°");
		_turnRow = Row("Turn", TurnSlider, turnV);
		PresetBox.SelectionChanged += (_, _) => Randomize();
		RandomizeButton.Click += (_, _) => Randomize();
		SeedBox.ValueChanged += (_, _) =>
		{
			if (!_filling)
			{
				Changed?.Invoke();
			}
		};
		PresetBox.Tip("mountain.preset");
		HeightSlider.Tip("mountain.height");
		RadiusSlider.Tip("mountain.radius");
		RoughSlider.Tip("mountain.rough");
		TurnSlider.Tip("mountain.turn");
		SeedBox.Tip("mountain.seed");
		RandomizeButton.Tip("mountain.randomize");
		ClearBox.Tip("mountain.clear");
		GrowBox.Tip("mountain.grow");
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
					new TextBlock { Text = "Mountain", FontSize = 14, FontWeight = FontWeight.SemiBold },
					Row("Preset", PresetBox),
					HelpText,
					Row("Height", HeightSlider, heightV),
					Row("Radius", RadiusSlider, radiusV),
					Row("Rough", RoughSlider, roughV),
					_turnRow,
					Row("Seed", SeedBox),
					RandomizeButton,
					ClearBox,
					GrowBox,
					new TextBlock
					{
						Text = "Click the ground to put the mountain there (the circle shows how far it reaches); Ctrl+Z takes it back. "
							+ "It goes past the game's ±8 m: saving turns it into ground discs every player's game counts as generated ground, console players too.",
						FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
		Randomize();
	}

	// A new mountain of the chosen preset: its size within the preset's ranges, and a new seed.
	public void Randomize()
	{
		var p = Preset;
		var m = Mountain.Roll(p, _random);
		_filling = true;
		HeightSlider.Value = m.Height;
		RadiusSlider.Value = m.Radius;
		RoughSlider.Value = Math.Round(m.Rough / 0.05) * 0.05;
		TurnSlider.Value = m.Turn;
		SeedBox.Value = m.Seed;
		_filling = false;
		HelpText.Text = p.Help;
		_turnRow.IsVisible = p.Kind is MountainKind.Ridge or MountainKind.Range;
		Changed?.Invoke();
	}
}
