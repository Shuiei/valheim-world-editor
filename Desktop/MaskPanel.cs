using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The Mask's panel (brushes, Path, Shape and Area), like the web editor's: on or off, biomes, height
// and slope ranges, and paint. An empty box means no limit.
public sealed class MaskPanel
{
	public Control Card { get; }
	private readonly Mask _mask;
	private readonly Control _body;
	private bool _filling;
	internal CheckBox OnBox { get; } = new CheckBox { Content = "Mask", FontSize = 13, FontWeight = FontWeight.SemiBold }.Tip("mask.on");
	internal Dictionary<int, ToggleButton> BiomeButtons { get; } = new();
	internal NumericUpDown HeightMin { get; } = Num(0.5m);
	internal NumericUpDown HeightMax { get; } = Num(0.5m);
	internal NumericUpDown SlopeMin { get; } = Num(1m, 0, 90);
	internal NumericUpDown SlopeMax { get; } = Num(1m, 0, 90);
	internal ComboBox PaintBox { get; } = new()
	{
		ItemsSource = new[] { "Any ground", "Only unpainted", "Only painted", "Only dirt", "Only cultivated", "Only paved" },
		SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch,
	};
	internal TextBlock WarningText { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 150, 90)), TextWrapping = TextWrapping.Wrap };

	private static NumericUpDown Num(decimal step, decimal min = -1000, decimal max = 2000) =>
		new() { Value = null, Increment = step, Minimum = min, Maximum = max, FormatString = "0.#", FontSize = 12, MinWidth = 80, PlaceholderText = "none" };

	public MaskPanel(Mask mask)
	{
		_mask = mask;
		var biomes = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
		foreach (var (b, name) in Mask.BiomeNames)
		{
			var t = new ToggleButton { Content = name, FontSize = 11, Padding = new Thickness(6, 2) }.Tip("mask.biome");
			t.IsCheckedChanged += (_, _) =>
			{
				if (t.IsChecked == true) mask.Biomes.Add(b); else mask.Biomes.Remove(b);
				Changed();
			};
			BiomeButtons[b] = t;
			biomes.Children.Add(t);
		}
		OnBox.IsCheckedChanged += (_, _) => Changed();
		HeightMin.Tip("mask.hmin");
		HeightMax.Tip("mask.hmax");
		SlopeMin.Tip("mask.smin");
		SlopeMax.Tip("mask.smax");
		PaintBox.Tip("mask.paint");
		foreach (var n in new[] { HeightMin, HeightMax, SlopeMin, SlopeMax })
		{
			n.ValueChanged += (_, _) => Changed();
		}
		PaintBox.SelectionChanged += (_, _) => Changed();
		Control Pair(string label, Control a, Control b, string unit)
		{
			// The same columns on every row (the unit too), so the boxes line up.
			var g = new Grid { ColumnDefinitions = new ColumnDefinitions("50,*,*,14"), ColumnSpacing = 4 };
			g.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
			Grid.SetColumn(a, 1);
			Grid.SetColumn(b, 2);
			var u = new TextBlock { Text = unit, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetColumn(u, 3);
			g.Children.Add(a);
			g.Children.Add(b);
			g.Children.Add(u);
			return g;
		}
		_body = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = "Only change ground that matches everything below.", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
				biomes,
				Pair("Height", HeightMin, HeightMax, "m"),
				Pair("Slope", SlopeMin, SlopeMax, "°"),
				new Grid { ColumnDefinitions = new ColumnDefinitions("50,*,14"), ColumnSpacing = 4, Children = { new TextBlock { Text = "Paint", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Column(PaintBox, 1) } },
				WarningText,
				new TextBlock { Text = "No biome selected = all biomes. Leave a box empty for no limit. Alt + Shift + click the ground fills the height range around it (±2 m).", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
			},
		};
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel { Width = 280, Spacing = 6, Children = { OnBox, _body } },
		};
		mask.Changed += Fill;
		Fill();
	}

	private static Control Column(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	private static float? F(NumericUpDown n) => n.Value is decimal d ? (float)d : null;

	private void Changed()
	{
		if (_filling)
		{
			return;
		}
		_mask.On = OnBox.IsChecked == true;
		_mask.HeightMin = F(HeightMin);
		_mask.HeightMax = F(HeightMax);
		_mask.SlopeMin = F(SlopeMin);
		_mask.SlopeMax = F(SlopeMax);
		_mask.Paint = (Mask.PaintRule)Math.Max(0, PaintBox.SelectedIndex);
		_body.IsVisible = _mask.On;
		WarningText.Text = _mask.Warning();
		WarningText.IsVisible = WarningText.Text != "";
	}

	// The mask changed elsewhere (Alt + Shift + click): the boxes follow.
	private void Fill()
	{
		_filling = true;
		OnBox.IsChecked = _mask.On;
		HeightMin.Value = _mask.HeightMin is float a ? (decimal)a : null;
		HeightMax.Value = _mask.HeightMax is float b ? (decimal)b : null;
		_filling = false;
		Changed();
	}
}
