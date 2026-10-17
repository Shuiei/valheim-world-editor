using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A switch or toggle set to true or false (Invoke): null when done, else why not.</summary>
    internal static string? SetToggle(Ctl c, ToggleButton t, string value)
    {
        if (BoolOf(value) is not bool on)
        {
            return $"{c.Id} is a {c.Kind}: give true or false (or click).";
        }
        t.IsChecked = on;
        return null;
    }

    /// <summary>A button pressed (Invoke): null when done, else why not.</summary>
    internal static string? SetButton(Ctl c, Button b, string value)
    {
        if (!IsClick(value) && value.Trim().Length > 0)
        {
            return $"{c.Id} is a button: give click.";
        }
        Press(b);
        return null;
    }

    /// <summary>A slider's value, held to its range (Invoke): null when done, else why not.</summary>
    internal static string? SetSlider(Ctl c, Slider s, string value)
    {
        if (NumberOf(value) is not double v)
        {
            return $"{c.Id} is a slider from {s.Minimum} to {s.Maximum}: give a number.";
        }
        s.Value = Math.Clamp(v, s.Minimum, s.Maximum);
        return null;
    }

    /// <summary>A number box's value, held to its range (Invoke): null when done, else why not.</summary>
    internal static string? SetNumber(Ctl c, NumericUpDown u, string value)
    {
        if (NumberOf(value) is not double d)
        {
            return $"{c.Id} takes a number from {u.Minimum} to {u.Maximum}.";
        }
        // Held to the range before it becomes a decimal, which cannot hold every double (1e30 threw).
        u.Value = d >= (double)u.Maximum ? u.Maximum : d <= (double)u.Minimum ? u.Minimum : (decimal)d;
        return null;
    }
}
