
namespace TerrainEditor.Desktop;

/// <summary>
/// The set and where commands: a slider, text box or list beside a label gets its value, or tells where it is.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'set' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdSet(Call call)
    {
        string[] a = call.A;
        MainWindow w = call.W;
        var (label, value) = Bar(a, "set <label>|<value>");
        // A label, not the choice a list shows (the Landform panel's Kind list shows "Formula" above the Formula box).
        var tb = Find<Avalonia.Controls.TextBlock>(w, t => Says(t.Text, label)
            && !Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(t).OfType<Avalonia.Controls.ComboBox>().Any()) ?? throw new InvalidOperationException($"no label \"{label}\"");
        // The row holding the label: the first control after it.
        var row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(tb);
        for (int up = 0; up < 3 && row != null; up++, row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(row))
        {
            var c = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.Control>()
                .FirstOrDefault(c => c is Avalonia.Controls.Slider or Avalonia.Controls.TextBox or Avalonia.Controls.ComboBox or Avalonia.Controls.NumericUpDown && c.IsEffectivelyVisible);
            switch (c)
            {
                case Avalonia.Controls.Slider sl:
                    sl.Value = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    return Task.FromResult(State(w));
                case Avalonia.Controls.NumericUpDown:
                    // Several boxes in the row (Height min, max): in order, comma separated; empty for none.
                    FillRowBoxes<Avalonia.Controls.NumericUpDown>(row, value, (box, v) => box.Value = v.Trim().Length == 0 ? null : decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture));
                    return Task.FromResult(State(w));
                case Avalonia.Controls.TextBox:
                    // Several boxes in the row (Size % from, to): the values in order, comma separated.
                    FillRowBoxes<Avalonia.Controls.TextBox>(row, value, (box, v) => box.Text = v.Trim());
                    return Task.FromResult(State(w));
                case Avalonia.Controls.ComboBox cb when IndexOf(cb, value) >= 0:
                    cb.SelectedIndex = IndexOf(cb, value);
                    return Task.FromResult(State(w));
                default:
                    break;
            }
        }
        throw new InvalidOperationException($"nothing to set beside \"{label}\"");
    }

    /// <summary>
    /// The 'where' command: where the slider, box or list beside a label is, its middle in the window's coordinates (what
    /// a scene points at or clicks on the display, wherever the language shown puts it).
    /// </summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdWhere(Call call)
    {
        MainWindow w = call.W;
        string label = call.A[1];
        var tb = Find<Avalonia.Controls.TextBlock>(w, t => Says(t.Text, label)
            && !Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(t).OfType<Avalonia.Controls.ComboBox>().Any()) ?? throw new InvalidOperationException($"no label \"{label}\"");
        var row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(tb);
        for (int up = 0; up < 3 && row != null; up++, row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(row))
        {
            var c = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.Control>()
                .FirstOrDefault(c => c is Avalonia.Controls.Slider or Avalonia.Controls.TextBox or Avalonia.Controls.ComboBox or Avalonia.Controls.NumericUpDown && c.IsEffectivelyVisible);
            if (c != null && Avalonia.Controls.TopLevel.GetTopLevel(c) is { } top
                && Avalonia.VisualExtensions.TranslatePoint(c, new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), top) is { } at)
            {
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, double> { ["x"] = Math.Round(at.X), ["y"] = Math.Round(at.Y) }));
            }
        }
        throw new InvalidOperationException($"nothing beside \"{label}\"");
    }

    /// <summary>Sets the visible boxes of one row of a type in order from a comma separated list of values.</summary>
    /// <typeparam name="T">The box type.</typeparam>
    /// <param name="row">The row holding the boxes.</param>
    /// <param name="value">The comma separated values; each goes to the box at its position.</param>
    /// <param name="set">Sets one box from its value.</param>
    private static void FillRowBoxes<T>(Avalonia.Visual row, string value, Action<T, string> set) where T : Avalonia.Controls.Control
    {
        var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<T>().Where(b => b.IsEffectivelyVisible).ToList();
        string[] values = value.Split(',');
        for (int k = 0; k < Math.Min(values.Length, boxes.Count); k++)
        {
            set(boxes[k], values[k]);
        }
    }
}
