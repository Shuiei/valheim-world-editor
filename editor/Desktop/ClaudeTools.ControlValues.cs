using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The options a dropdown offers, as text.</summary>
    private static List<string> OptionsOf(ComboBox b) =>
        [.. b.Items.Cast<object?>().Select(i => i is ComboBoxItem ci ? TextOf(ci.Content) ?? "" : TextOf(i) ?? i?.ToString() ?? "")];

    /// <summary>A control's value as Claude reads it.</summary>
    internal static object? ValueOf(Control c) => c switch
    {
        ToggleButton t when t is CheckBox or RadioButton || t.IsChecked != null => t.IsChecked,
        ComboBox b => b.SelectedIndex >= 0 && b.SelectedIndex < OptionsOf(b).Count ? OptionsOf(b)[b.SelectedIndex] : null,
        Slider s => Math.Round(s.Value, 3),
        NumericUpDown u => u.Value,
        TextBox t => t.Text is { Length: > 300 } text ? text[..300] + "…" : t.Text,
        _ => null,
    };

    /// <summary>Selects a dropdown's option (Invoke): by its text, its start, or its number; null when done, else why not.</summary>
    private static string? SetDropdown(Ctl c, ComboBox box, string value)
    {
        var options = OptionsOf(box);
        int i = options.FindIndex(o => o.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (i < 0)
        {
            i = options.FindIndex(o => o.StartsWith(value.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        if (i < 0 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 && n < options.Count)
        {
            i = n;
        }
        if (i < 0)
        {
            return $"{c.Id} has no option \"{value}\": {string.Join(", ", options)}.";
        }
        box.SelectedIndex = i;
        return null;
    }
}
