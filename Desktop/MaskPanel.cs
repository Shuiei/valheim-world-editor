using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Mask's panel (brushes, Path, Shape and Area), like the web editor's: on or off, biomes, height and slope ranges,
/// and paint. An empty box means no limit.
/// </summary>
public sealed class MaskPanel
{
    /// <summary>
    /// The panel's card.
    /// </summary>
    public Control Card { get; }
    /// <summary>
    /// The mask the panel edits.
    /// </summary>
    private readonly Mask _mask;
    /// <summary>
    /// Everything under the on/off box, hidden while the mask is off.
    /// </summary>
    private readonly Control _body;
    /// <summary>
    /// True while <c>Fill</c> sets the boxes from the mask, so they do not write back.
    /// </summary>
    private bool _filling;
    /// <summary>
    /// Turns the mask on or off.
    /// </summary>
    internal CheckBox OnBox { get; } = new CheckBox { Content = "Mask", FontSize = 13, FontWeight = FontWeight.SemiBold }.Tip("mask.on");
    /// <summary>
    /// One toggle per biome, by its biome flag.
    /// </summary>
    internal Dictionary<int, ToggleButton> BiomeButtons { get; } = [];
    /// <summary>
    /// Lowest height in metres; empty for no limit.
    /// </summary>
    internal NumericUpDown HeightMin { get; } = Num(0.5m);
    /// <summary>
    /// Highest height in metres; empty for no limit.
    /// </summary>
    internal NumericUpDown HeightMax { get; } = Num(0.5m);
    /// <summary>
    /// Smallest slope in degrees (0..90); empty for no limit.
    /// </summary>
    internal NumericUpDown SlopeMin { get; } = Num(1m, 0, 90);
    /// <summary>
    /// Largest slope in degrees (0..90); empty for no limit.
    /// </summary>
    internal NumericUpDown SlopeMax { get; } = Num(1m, 0, 90);
    /// <summary>
    /// The paint rule, in the order of <c>Mask.PaintRule</c>.
    /// </summary>
    internal ComboBox PaintBox { get; } = new()
    {
        ItemsSource = new[] { "Any ground", "Only unpainted", "Only painted", "Only dirt", "Only cultivated", "Only paved" },
        SelectedIndex = 0,
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    /// <summary>
    /// The mask's warning about settings that let (almost) nothing through; hidden when there is none.
    /// </summary>
    internal TextBlock WarningText { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 150, 90)), TextWrapping = TextWrapping.Wrap };

    /// <summary>
    /// A number box that starts empty (no limit).
    /// </summary>
    private static NumericUpDown Num(decimal step, decimal min = -1000, decimal max = 2000) =>
        new() { Value = null, Increment = step, Minimum = min, Maximum = max, FormatString = "0.#", FontSize = 12, MinWidth = 80, PlaceholderText = "none" };

    /// <summary>
    /// Builds the card for <paramref name="mask"/> and keeps the two in step both ways.
    /// </summary>
    public MaskPanel(Mask mask)
    {
        _mask = mask;
        var biomes = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        foreach (var (b, name) in Mask.BiomeNames)
        {
            var t = new ToggleButton { Content = name, FontSize = 11, Padding = new Thickness(6, 2) }.Tip("mask.biome");
            t.IsCheckedChanged += (_, _) =>
            {
                if (t.IsChecked == true)
                {
                    mask.Biomes.Add(b);
                }
                else
                {
                    mask.Biomes.Remove(b);
                }

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
        static Control Pair(string label, Control a, Control b, string unit)
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

    /// <summary>
    /// <paramref name="c"/> set in grid column <paramref name="col"/>, for use inside an initializer.
    /// </summary>
    private static Control Column(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }

    /// <summary>
    /// A box's value, or null when it is empty.
    /// </summary>
    private static float? F(NumericUpDown n) => n.Value is decimal d ? (float)d : null;

    /// <summary>
    /// A box's value for a mask limit: null (empty) for no limit.
    /// </summary>
    private static decimal? Box(float? v) => v is float f ? (decimal)f : null;

    /// <summary>
    /// A box changed: the mask takes the panel's settings and the warning is updated.
    /// </summary>
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

    /// <summary>
    /// The mask changed elsewhere (Alt + Shift + click): every box follows, so <c>Changed</c> writes back what the
    /// mask already holds.
    /// </summary>
    private void Fill()
    {
        _filling = true;
        OnBox.IsChecked = _mask.On;
        foreach (var (b, t) in BiomeButtons)
        {
            t.IsChecked = _mask.Biomes.Contains(b);
        }
        HeightMin.Value = Box(_mask.HeightMin);
        HeightMax.Value = Box(_mask.HeightMax);
        SlopeMin.Value = Box(_mask.SlopeMin);
        SlopeMax.Value = Box(_mask.SlopeMax);
        PaintBox.SelectedIndex = (int)_mask.Paint;
        _filling = false;
        Changed();
    }
}
