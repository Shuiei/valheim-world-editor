
namespace TerrainEditor.Desktop;

/// <summary>
/// The commands that press and read the controls: keys, clicks, check boxes, lists and text boxes.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'key' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdKey(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        var key = Enum.Parse<Avalonia.Input.Key>(args[0], ignoreCase: true);
        w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
        w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
        return Task.FromResult(State(w));
    }

    /// <summary>The 'click' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdClick(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        var c = Find<Avalonia.Controls.Button>(w, b => string.Equals(TextOf(b), a[1], StringComparison.OrdinalIgnoreCase))
            ?? Find<Avalonia.Controls.Button>(w, b => TextOf(b).StartsWith(a[1] + " ", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no button \"{a[1]}\"");
        if (c is Avalonia.Controls.Primitives.ToggleButton t)
        {
            t.IsChecked = t.IsChecked != true;
        }
        c.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        return Task.FromResult(State(w));
    }

    /// <summary>The 'check' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdCheck(Call call)
    {
        string[] a = call.A;
        MainWindow w = call.W;
        // check <label>|on|off: a check box or switch by its text.
        var (label, state) = Bar(a, "check <label>|on|off");
        var box = Find<Avalonia.Controls.CheckBox>(w, b => string.Equals(TextOf(b).Trim(), label, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no check box \"{label}\"");
        box.IsChecked = state == "on";
        return Task.FromResult(State(w));
    }

    /// <summary>The 'choose' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdChoose(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        // A drop-down or a list box (the Dungeon panel's rooms).
        var box = Find<Avalonia.Controls.Primitives.SelectingItemsControl>(w, b => b is Avalonia.Controls.ComboBox or Avalonia.Controls.ListBox && IndexOf(b, a[1]) >= 0)
            ?? throw new InvalidOperationException($"no list with \"{a[1]}\"");
        box.SelectedIndex = IndexOf(box, a[1]);
        return Task.FromResult(State(w));
    }

    /// <summary>The 'type' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdType(Call call)
    {
        string[] a = call.A;
        MainWindow w = call.W;
        var (hint, text) = Bar(a, "type <hint>|<text>");
        var box = Find<Avalonia.Controls.TextBox>(w, b => (b.PlaceholderText ?? "").Contains(hint, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no text box \"{hint}\"");
        box.Text = text;
        return Task.FromResult(State(w));
    }
}
