using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ShapePanel
{
    /// <summary>Builds the panel with the first preset (Mound) picked.</summary>
    public ShapePanel()
    {
        PresetBox = new ComboBox { ItemsSource = Presets.Select(p => NameOf(p.Key)).Append(Lang.T("shape.preset.own")).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
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
        static Control Row(string label, Control input, Control? after = null) => Ui.Row(label, input, after, "60,*,Auto");
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("rail.label.shape"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Row(Lang.T("rail.label.shape"), PresetBox),
                    Row(Lang.T("shape.label.radius"), RadiusSlider, radiusV),
                    Row(Lang.T("common.height"), HeightBox, new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }),
                    new TextBlock { Text = Lang.T("shape.label.formula"), FontSize = 12, Foreground = Ui.Muted },
                    FormulaBox,
                    ErrorText,
                    new TextBlock
                    {
                        Text = Lang.T("shape.hint.click"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        Sync();
    }
}
