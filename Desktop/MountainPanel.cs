using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Mountain tool's panel: a preset (peak, ridge, range, mesa, volcano, hills), its size and roughness, the
/// direction of ridges and ranges, and the seed that makes its detail. Randomize rolls a new mountain of the preset; a
/// click on the ground puts it there.
/// </summary>
public sealed class MountainPanel
{
    /// <summary>
    /// The panel's card, shown while the Mountain tool is on.
    /// </summary>
    public Control Card { get; }
    /// <summary>
    /// Raised when any setting changes (not while Randomize fills them), so the preview can follow.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The preset list; choosing one rolls a new mountain of it.
    /// </summary>
    internal ComboBox PresetBox { get; }
    /// <summary>
    /// Height in metres, 5..300.
    /// </summary>
    internal Slider HeightSlider { get; }
    /// <summary>
    /// Radius in metres, 20..280.
    /// </summary>
    internal Slider RadiusSlider { get; }
    /// <summary>
    /// Roughness, 0..1 in steps of 0.05.
    /// </summary>
    internal Slider RoughSlider { get; }
    /// <summary>
    /// Direction of ridges and ranges in degrees, 0..179; its row shows only for those kinds.
    /// </summary>
    internal Slider TurnSlider { get; }
    /// <summary>
    /// The seed of the mountain's detail.
    /// </summary>
    internal NumericUpDown SeedBox { get; } = new() { Minimum = 0, Maximum = int.MaxValue, Increment = 1, FormatString = "0", FontSize = 12 };
    /// <summary>
    /// Rolls a new mountain of the chosen preset.
    /// </summary>
    internal Button RandomizeButton { get; } = new Button { Content = Lang.T("common.randomize"), FontSize = 12 }.Classed("primary");
    /// <summary>
    /// Take away the trees and rocks the mountain buries.
    /// </summary>
    internal CheckBox ClearBox { get; } = new() { Content = Lang.T("mountain.label.clear"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Grow the biome's trees and rocks on the new mountain.
    /// </summary>
    internal CheckBox GrowBox { get; } = new() { Content = Lang.T("mountain.label.grow"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// The chosen preset's help line.
    /// </summary>
    internal TextBlock HelpText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The Turn row, hidden for kinds without a direction.
    /// </summary>
    private readonly Control _turnRow;
    /// <summary>
    /// True while Randomize sets the controls, so they raise one Changed at the end instead of one each.
    /// </summary>
    private bool _filling;
    /// <summary>
    /// Where Randomize's sizes and seeds come from.
    /// </summary>
    private readonly Random _random = new();
    /// <summary>
    /// The largest radius that fits in the open area (with its ragged outline), clicked in its middle.
    /// </summary>
    private float _maxRadius = 280;

    /// <summary>
    /// The open area changed: Randomize keeps mountains that fit in it.
    /// </summary>
    /// <param name="gridSize">The open area's width, in ground points.</param>
    public void FitTo(int gridSize)
    {
        _maxRadius = MathF.Max(20, ((gridSize - 1) / 2f - 2) / 1.15f);
    }

    /// <summary>
    /// The chosen preset (the first when none is chosen).
    /// </summary>
    public Mountain.Preset Preset => Mountain.Presets[Math.Max(0, PresetBox.SelectedIndex)];

    /// <summary>
    /// The mountain the controls describe now.
    /// </summary>
    public MountainSpec Spec => new(Preset.Kind, (float)HeightSlider.Value, (float)RadiusSlider.Value, (float)RoughSlider.Value, (float)TurnSlider.Value, (int)(SeedBox.Value ?? 0));

    /// <summary>
    /// Whether to take away the trees and rocks the mountain buries.
    /// </summary>
    public bool Clear => ClearBox.IsChecked == true;

    /// <summary>
    /// Whether to grow the biome's trees and rocks on it.
    /// </summary>
    public bool Grow => GrowBox.IsChecked == true;

    /// <summary>
    /// A slider snapping to <paramref name="step"/> that writes its value into <paramref name="shown"/> and raises
    /// Changed.
    /// </summary>
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

    /// <summary>
    /// Builds the card and rolls a first mountain.
    /// </summary>
    public MountainPanel()
    {
        PresetBox = new ComboBox { ItemsSource = Mountain.Presets.Select(p => p.Shown).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock heightV = new(), radiusV = new(), roughV = new(), turnV = new();
        HeightSlider = Slide(5, 300, 1, heightV, v => $"{v:0} m");
        RadiusSlider = Slide(20, 280, 1, radiusV, v => $"{v:0} m");
        RoughSlider = Slide(0, 1, 0.05, roughV, v => $"{v:0.00}");
        TurnSlider = Slide(0, 179, 1, turnV, v => $"{v:0}°");
        _turnRow = Ui.Row(Lang.T("brush.label.turn"), TurnSlider, turnV);
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
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("rail.label.mountain"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Ui.Row(Lang.T("common.preset"), PresetBox),
                    HelpText,
                    Ui.Row(Lang.T("common.height"), HeightSlider, heightV),
                    Ui.Row(Lang.T("shape.label.radius"), RadiusSlider, radiusV),
                    Ui.Row(Lang.T("mountain.label.rough"), RoughSlider, roughV),
                    _turnRow,
                    Ui.Row(Lang.T("common.seed"), SeedBox),
                    RandomizeButton,
                    ClearBox,
                    GrowBox,
                    new TextBlock
                    {
                        Text = Lang.T("mountain.hint.click"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        Randomize();
    }

    /// <summary>
    /// A new mountain of the chosen preset: its size within the preset's ranges, and a new seed. Scaled down (height
    /// too) when it would not fit the open area.
    /// </summary>
    public void Randomize()
    {
        var p = Preset;
        var m = Mountain.Roll(p, _random);
        // Smaller as a whole (height too, so the slopes stay as gentle) when it would not fit.
        if (m.Radius > _maxRadius)
        {
            float k = _maxRadius / m.Radius;
            m = m with { Radius = MathF.Floor(_maxRadius), Height = MathF.Max(5, MathF.Round(m.Height * k)) };
        }
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
