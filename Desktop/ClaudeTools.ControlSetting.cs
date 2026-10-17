using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// Sets a control as the user would (on the window's thread, so its handlers run): a switch on or off, a dropdown's
    /// option, a slider's or number's value, a text, a button pressed. Null when done, else why not.
    /// </summary>
    internal string? Set(Ctl c, string value, bool enter)
    {
        if (Blocked(c) is string why)
        {
            return $"{c.Id} is not Claude's to use: {why}.";
        }
        if (!c.Control.IsEffectivelyEnabled)
        {
            return $"{c.Id} is greyed out now (the editor does not allow it in this state).";
        }
        // Put back as it was, not cleared: a control set while one already is keeps the outer one Claude's.
        bool was = Acting.Value;
        Acting.Value = true;
        try
        {
            return Invoke(c, value, enter);
        }
        finally
        {
            Acting.Value = was;
        }
    }

    /// <summary>Whether a value asks for a press ("click").</summary>
    private static bool IsClick(string value) => value.Trim().Equals("click", StringComparison.OrdinalIgnoreCase);

    /// <summary>A switch's value from its words (true, on, yes, 1, checked, and their opposites), or null when it is none.</summary>
    private static bool? BoolOf(string v) => v.Trim().ToLowerInvariant() switch { "true" or "on" or "yes" or "1" or "checked" => true, "false" or "off" or "no" or "0" or "unchecked" => false, _ => null };

    /// <summary>A number from its text, or null when it is none (NaN and the infinities parse too, but are no value to set).</summary>
    private static double? NumberOf(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d) ? d : null;
    /// <summary>Sets the control itself (Set, with Acting on): null when done, else why not.</summary>
    private static string? Invoke(Ctl c, string value, bool enter)
    {
        switch (c.Control)
        {
            case ToggleButton t when IsClick(value):
                Press(t);
                return null;
            case ToggleButton t:
                return SetToggle(c, t, value);
            case Button b:
                return SetButton(c, b, value);
            case ComboBox box:
                return SetDropdown(c, box, value);
            case Slider s:
                return SetSlider(c, s, value);
            case NumericUpDown u:
                return SetNumber(c, u, value);
            case TextBox t:
                t.Text = value;
                if (enter)
                {
                    t.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
                }
                return null;
            default:
                return $"{c.Id} cannot be set.";
        }
    }
}
