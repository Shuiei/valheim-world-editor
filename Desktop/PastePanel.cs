using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

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
    internal CheckBox GroundBox { get; } = new() { Content = Lang.T("paste.label.ground"), IsChecked = true, FontSize = 12 };
    /// <summary>Whether the paste puts down the copied objects.</summary>
    internal CheckBox ObjectsBox { get; } = new() { Content = Lang.T("common.objects"), IsChecked = true, FontSize = 12 };
    /// <summary>Whether the paste digs out the ground in its way and takes away trees and rocks.</summary>
    internal CheckBox ClearSiteBox { get; } = new() { Content = Lang.T("paste.label.clearSite"), IsChecked = true, FontSize = 12 };
    /// <summary>How far the paste is moved up or down, in metres.</summary>
    internal NumericUpDown OffsetBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>How many copies to stack, 1 to 50.</summary>
    internal NumericUpDown CopiesBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12 };
    /// <summary>
    /// Which way the copies repeat: along the width, the depth, or upwards (in <c>PasteTool.Along</c>
    /// order).
    /// </summary>
    internal ComboBox AlongBox { get; } = new() { ItemsSource = new[] { Lang.T("paste.along.width"), Lang.T("paste.along.depth"), Lang.T("paste.along.up") }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>The gap between stacked copies, in metres.</summary>
    internal NumericUpDown GapBox { get; } = new() { Value = 0, Increment = 0.25m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Turns the paste by 90 degrees.</summary>
    internal Button TurnButton { get; } = new() { Content = Lang.T("paste.label.turn"), FontSize = 12 };
    /// <summary>Mirrors the paste, or unmirrors it.</summary>
    internal Button MirrorButton { get; } = new() { Content = Lang.T("paste.label.mirror"), FontSize = 12 };
    /// <summary>Leaves the Paste tool (raises <see cref="Done"/>).</summary>
    internal Button DoneButton { get; } = new() { Content = Lang.T("paste.label.done"), FontSize = 12 };
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
        // The rows: label, input and, for lengths, the unit after it.
        static Control Row(string label, Control input, bool metres = false) => Ui.Row(label, input,
            metres ? new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center } : null,
            "70,*,Auto");
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 290,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("paste.label.paste"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Info, GroundBox, ObjectsBox, ClearSiteBox,
                    Row(Lang.T("common.height"), OffsetBox, metres: true),
                    new TextBlock { Text = Lang.T("paste.head.repeat"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 4, 0, 0) },
                    Row(Lang.T("paste.label.copies"), CopiesBox),
                    Row(Lang.T("paste.label.along"), AlongBox),
                    Row(Lang.T("paste.label.gap"), GapBox, metres: true),
                    new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Children = { TurnButton, MirrorButton, DoneButton } },
                    new TextBlock
                    {
                        Text = Lang.T("paste.hint.click"),
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
        if (c == null)
        {
            Info.Text = Lang.T("paste.info.none");
            return;
        }
        string clip = Lang.T("paste.info.clip", c.W, c.H, c.Objects.Count, _paste.Count > 1 ? Lang.T("paste.info.copies", _paste.Count) : "",
            _paste.Turn, _paste.Mirror ? Lang.T("paste.info.mirrored") : "");
        Info.Text = c.Name != null ? Lang.T("paste.info.named", c.Name, clip) : clip;
    }
}
