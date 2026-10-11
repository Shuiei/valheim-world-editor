using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Shape tool's panel, like the web editor's (editor/shapes.js): a click puts a shape into the
/// ground: a mound, cone, mesa, crater, moat or bowl, or any formula. Every preset is a formula too,
/// shown in the box, so it can be changed.
/// </summary>
public sealed class ShapePanel
{
    /// <summary>What a formula can use: metres east and north of the click, distance, radius, ground height.</summary>
    private static readonly string[] FormulaVars = ["x", "z", "d", "r", "h"];

    /// <summary>
    /// The preset shapes, each a formula; the list's last entry (index Presets.Length) is "Formula…", one of the
    /// user's own.
    /// </summary>
    public static readonly (string Key, string Name, string Formula)[] Presets =
    [
        ("mound", "Mound", "h * smooth(1 - d / r)"),
        ("cone", "Cone", "h * max(0, 1 - d / r)"),
        ("mesa", "Mesa", "h * smooth((r - d) / (0.35 * r))"),
        ("crater", "Crater", "d < r ? -h * max(0, 1 - (d / (0.62 * r)) ^ 2) + 0.35 * h * bell((d - 0.72 * r) / (0.16 * r)) * smooth((r - d) / (0.1 * r)) : 0"),
        ("moat", "Moat (ring ditch)", "-h * bell((d - 0.75 * r) / (0.12 * r))"),
        ("bowl", "Bowl", "-h * smooth(1 - (d / r) ^ 2)"),
        ("ridges", "Ridged hill", "h * smooth(1 - d / r) * (0.75 + 0.25 * abs(sin(x / 3 + n(x / 9, z / 9) * 2)))"),
    ];

    /// <summary>The panel itself, shown beside the tool rail while the Shape tool is picked.</summary>
    public Control Card { get; }
    /// <summary>The shape's radius in metres (2..80): only points within it change.</summary>
    public float Radius { get; private set; } = 16;
    /// <summary>The shape's height in metres (the formula's h).</summary>
    public float Height { get; private set; } = 5;
    /// <summary>The compiled formula (null while it has a mistake): metres to add to the ground at a point.</summary>
    public Func<Formula.Env, double>? Function { get; private set; }
    /// <summary>The edit's name for the history, from the preset picked (or "Formula").</summary>
    public string Label => PresetBox.SelectedIndex < Presets.Length ? $"Shape: {Presets[PresetBox.SelectedIndex].Name}" : "Shape: Formula";
    /// <summary>Raised when the radius, height or formula changes.</summary>
    public event Action? Changed;

    /// <summary>The preset list, with "Formula…" last.</summary>
    internal ComboBox PresetBox { get; }
    /// <summary>The radius slider, whole metres.</summary>
    internal Slider RadiusSlider { get; }
    /// <summary>The height box, in metres.</summary>
    internal NumericUpDown HeightBox { get; } = new() { Value = 5, Increment = 0.5m, FormatString = "0.0", FontSize = 12 };
    /// <summary>The formula's text; editing a preset's formula switches the list to "Formula…".</summary>
    internal TextBox FormulaBox { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("monospace"), FontSize = 12, MinHeight = 60 };
    /// <summary>The formula's mistake, empty while it compiles.</summary>
    internal TextBlock ErrorText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(240, 150, 90)), TextWrapping = TextWrapping.Wrap };
    /// <summary>True while a preset writes its formula into the box, so that does not count as the user's change.</summary>
    private bool _settingPreset;

    /// <summary>Builds the panel with the first preset (Mound) picked.</summary>
    public ShapePanel()
    {
        PresetBox = new ComboBox { ItemsSource = Presets.Select(p => p.Name).Append("Formula…").ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        var radiusV = new TextBlock { FontSize = 12, Width = 40, VerticalAlignment = VerticalAlignment.Center, Text = "16 m" };
        RadiusSlider = new Slider { Minimum = 2, Maximum = 80, SmallChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true, Value = 16 };
        RadiusSlider.ValueChanged += (_, e) => { Radius = (float)e.NewValue; radiusV.Text = $"{e.NewValue:0} m"; Changed?.Invoke(); };
        HeightBox.ValueChanged += (_, e) => { Height = (float)(e.NewValue ?? 0); Changed?.Invoke(); };
        PresetBox.SelectionChanged += (_, _) =>
        {
            if (PresetBox.SelectedIndex >= 0 && PresetBox.SelectedIndex < Presets.Length)
            {
                _settingPreset = true;
                FormulaBox.Text = Presets[PresetBox.SelectedIndex].Formula;
                _settingPreset = false;
            }
            Sync();
        };
        // The Text property at once (TextChanged comes later, through the dispatcher).
        FormulaBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty)
            {
                return;
            }
            // Changing a preset's formula makes it a formula of its own.
            int i = PresetBox.SelectedIndex;
            if (!_settingPreset && i >= 0 && i < Presets.Length && FormulaBox.Text != Presets[i].Formula)
            {
                PresetBox.SelectedIndex = Presets.Length;
            }
            Sync();
        };
        FormulaBox.Text = Presets[0].Formula;
        PresetBox.Tip("shape.preset");
        RadiusSlider.Tip("shape.radius");
        HeightBox.Tip("shape.height");
        FormulaBox.Tip("shape.formula");
        static Control Row(string label, Control input, Control? after = null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*,Auto") };
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
                    new TextBlock { Text = "Shape", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Row("Shape", PresetBox),
                    Row("Radius", RadiusSlider, radiusV),
                    Row("Height", HeightBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
                    new TextBlock { Text = "Formula", FontSize = 12, Foreground = Ui.Muted },
                    FormulaBox,
                    ErrorText,
                    new TextBlock
                    {
                        Text = "Click the ground to put the shape there: the formula gives how many metres to add to the ground at each point (negative digs). x and z are metres east and north of the click, d the distance from it, r the radius, h the height, n(x, z) smooth noise from -1 to 1. Functions: smooth, bell, sin, cos, abs, sqrt, min, max, pow, clamp, exp, floor; a ? b : c. Only points within the radius change; the ±8 m limit applies.",
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        Sync();
    }

    /// <summary>Compiles the formula box's text into <c>Function</c>, shows any mistake and raises <c>Changed</c>.</summary>
    private void Sync()
    {
        try
        {
            Function = Formula.Compile(FormulaBox.Text ?? "", FormulaVars);
            ErrorText.Text = "";
        }
        catch (FormatException ex)
        {
            Function = null;
            ErrorText.Text = $"Formula: {ex.Message}.";
        }
        Changed?.Invoke();
    }
}
