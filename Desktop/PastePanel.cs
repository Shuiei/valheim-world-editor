using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Paste tool's panel, like the web editor's: what is pasted, the height, repeats, turn and mirror.
/// </summary>
public sealed class PastePanel
{
    /// <summary>The panel itself, shown over the view while the Paste tool is on.</summary>
    public Control Card { get; }
    /// <summary>The Paste tool the panel drives.</summary>
    private readonly PasteTool _paste;
    /// <summary>What is on the clipboard (size, objects, copies, turn), or how to copy something.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>Whether the paste puts down the copied ground shape and paint.</summary>
    internal CheckBox GroundBox { get; } = new() { Content = "Ground shape and paint", IsChecked = true, FontSize = 12 };
    /// <summary>Whether the paste puts down the copied objects.</summary>
    internal CheckBox ObjectsBox { get; } = new() { Content = "Objects", IsChecked = true, FontSize = 12 };
    /// <summary>Whether the paste digs out the ground in its way and takes away trees and rocks.</summary>
    internal CheckBox ClearSiteBox { get; } = new() { Content = "Clear the site (dig the ground in its way, take away trees and rocks)", IsChecked = true, FontSize = 12 };
    /// <summary>How far the paste is moved up or down, in metres.</summary>
    internal NumericUpDown OffsetBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>How many copies to stack, 1 to 50.</summary>
    internal NumericUpDown CopiesBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12 };
    /// <summary>
    /// Which way the copies repeat: along the width, the depth, or upwards (in <c>PasteTool.Along</c>
    /// order).
    /// </summary>
    internal ComboBox AlongBox { get; } = new() { ItemsSource = new[] { "its width", "its depth", "upwards" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>The gap between stacked copies, in metres.</summary>
    internal NumericUpDown GapBox { get; } = new() { Value = 0, Increment = 0.25m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Turns the paste by 90 degrees.</summary>
    internal Button TurnButton { get; } = new() { Content = "Turn 90° (R)", FontSize = 12 };
    /// <summary>Mirrors the paste, or unmirrors it.</summary>
    internal Button MirrorButton { get; } = new() { Content = "Mirror (F)", FontSize = 12 };
    /// <summary>Leaves the Paste tool (raises <see cref="Done"/>).</summary>
    internal Button DoneButton { get; } = new() { Content = "Done (Esc)", FontSize = 12 };
    /// <summary>The user is done pasting; the window switches back to the Area tool.</summary>
    public event Action? Done;

    /// <summary>
    /// Builds the panel and wires its controls to the Paste tool; it refreshes whenever the tool changes.
    /// </summary>
    public PastePanel(PasteTool paste)
    {
        _paste = paste;
        GroundBox.IsCheckedChanged += (_, _) => paste.Ground = GroundBox.IsChecked == true;
        ObjectsBox.IsCheckedChanged += (_, _) => paste.Objects = ObjectsBox.IsChecked == true;
        ClearSiteBox.IsCheckedChanged += (_, _) => paste.ClearSite = ClearSiteBox.IsChecked == true;
        ClearSiteBox.Tip("paste.clearSite");
        OffsetBox.ValueChanged += (_, e) => { paste.Offset = (float)(e.NewValue ?? 0); paste.Notify(); };
        CopiesBox.ValueChanged += (_, e) => { paste.Copies = (int)(e.NewValue ?? 1); paste.Notify(); };
        AlongBox.SelectionChanged += (_, _) => { paste.Direction = (PasteTool.Along)Math.Max(0, AlongBox.SelectedIndex); paste.Notify(); };
        GapBox.ValueChanged += (_, e) => { paste.Gap = (float)(e.NewValue ?? 0); paste.Notify(); };
        TurnButton.Click += (_, _) => paste.TurnBy(90);
        MirrorButton.Click += (_, _) => { paste.Mirror = !paste.Mirror; paste.Notify(); };
        DoneButton.Click += (_, _) => Done?.Invoke();
        TurnButton.Tip("paste.turn");
        MirrorButton.Tip("paste.mirror");
        DoneButton.Tip("paste.done");
        GroundBox.Tip("paste.ground");
        ObjectsBox.Tip("paste.objects");
        OffsetBox.Tip("paste.offset");
        CopiesBox.Tip("paste.copies");
        AlongBox.Tip("paste.along");
        GapBox.Tip("paste.gap");
        paste.Changed += Refresh;
        static Control Row(string label, Control input, string? unit = null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,Auto") };
            var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Tips.Label(l, input);
            g.Children.Add(l);
            Grid.SetColumn(input, 1);
            g.Children.Add(input);
            if (unit != null)
            {
                var u = new TextBlock { Text = unit, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(u, 2);
                g.Children.Add(u);
            }
            return g;
        }
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 290,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Paste", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Info, GroundBox, ObjectsBox, ClearSiteBox,
                    Row("Height", OffsetBox, " m"),
                    new TextBlock { Text = "REPEAT (STACK)", FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 4, 0, 0) },
                    Row("Copies", CopiesBox),
                    Row("Along", AlongBox),
                    Row("Gap", GapBox, " m"),
                    new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Children = { TurnButton, MirrorButton, DoneButton } },
                    new TextBlock
                    {
                        Text = "Click to place; the copied ground keeps its shape relative to the point you click. Height (or Ctrl + wheel; Shift: 0.1 m) moves the paste up or down: lower a building into a hill, and Clear the site digs it out.",
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        Refresh();
    }

    /// <summary>Updates the info line from the clipboard and the current turn, mirror and copies.</summary>
    public void Refresh()
    {
        var c = _paste.Clip;
        Info.Text = c == null ? "Nothing copied yet: copy in the Area tool (Ctrl+C) or the Select tool (Ctrl+C)."
            : $"{(c.Name != null ? c.Name + ": " : "")}{c.W} × {c.H} m, {c.Objects.Count} object(s){(_paste.Count > 1 ? $", {_paste.Count} copies" : "")} · turned {_paste.Turn:0.#}°{(_paste.Mirror ? ", mirrored" : "")}.";
    }
}
